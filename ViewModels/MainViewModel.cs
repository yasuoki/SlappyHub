using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;
using SlappyHub.Common;
using SlappyHub.Services;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;

namespace SlappyHub.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
	private SettingsStore _settingsStore;
	private SlappyDevice? _slappyDevice;

	public event PropertyChangedEventHandler? PropertyChanged;
	public event EventHandler<bool>? SlackSettingsRequested;
	public event EventHandler<bool>? NotifySettingsRequested;
	public SlackSettingsViewModel SlackSettings { get; }
	public NotifySettingsViewModel NotifySettings { get; }

	private bool _isSlackConnected;
	private bool _isWinNotificationConnected;
	public bool IsSlackConnected {
		get
		{
			if(_isSlackConnected && _isWinNotificationConnected)
				return true;
			var settings = _settingsStore.Settings;
			if (settings.ChannelSource == ChannelSourceMode.Socket)
			{
				if (!_isSlackConnected)
					return false;
				if(settings.EnableDirectMessage && !_isWinNotificationConnected)
					return false;
				return true;
			}
			if(settings.ChannelSource == ChannelSourceMode.WindowsNotify)
			{
				return _isWinNotificationConnected;
			}
			return false;
		}
	}

	public string SlackConnectionText
	{
		get
		{
			if(IsSlackConnected)
				return "接続済み";
			else
				return "未接続";
		}
	}

	private int _soundVolume;
	public string SoundVolumeText => SoundVolume.ToString();

	public int SoundVolume
	{
		get => _soundVolume;
		set
		{
			_soundVolume = Math.Clamp(value, 0, 100);
			OnPropertyChanged(nameof(SoundVolumeText));
		}
	}

	private bool _soundMute;

	public bool SoundMute
	{
		get => _soundMute;
		set
		{
			_soundMute = value;
			OnPropertyChanged(nameof(SoundMute));
		}
	}

	public string _wifiSsid;

	public string WifiSSID
	{
		get => _wifiSsid;
		set
		{
			_wifiSsid = value;
			OnPropertyChanged(nameof(WifiSSID));
		}
	}

	public string _wifiPassword;

	public string WifiPassword
	{
		get => _wifiPassword;
		set
		{
			_wifiPassword = value;
			OnPropertyChanged(nameof(WifiPassword));
		}
	}

	private string _wifiStatusTextText = "未接続";

	public string WifiStatusText
	{
		get
		{
			switch ((ReceiveMessage.ResultCode)_wifiStatusCode)
			{
				case ReceiveMessage.ResultCode.WifiConnected:
					return "接続";
				case ReceiveMessage.ResultCode.WifiSsidNotFound:
					return "SSIDが見つかりません";
				case ReceiveMessage.ResultCode.WifiAuthFail:
					return "認証エラー";
				case ReceiveMessage.ResultCode.WifiDisconnected:
					return "切断";
				default:
					return "未接続";
			}
		}
	}

	public bool IsWifiConnected => _wifiStatusCode == (int)ReceiveMessage.ResultCode.WifiConnected;

	private int _wifiStatusCode = (int)ReceiveMessage.ResultCode.WifiDisconnected;

	public int WifiStatusCode
	{
		get => _wifiStatusCode;
		set
		{
			if (_wifiStatusCode != value)
			{
				_wifiStatusCode = value;
				OnPropertyChanged(nameof(WifiStatusCode));
				OnPropertyChanged(nameof(WifiStatusText));
				OnPropertyChanged(nameof(IsWifiConnected));
			}
		}
	}

	private UsbDeviceInfo? _connectSlappyBell;

	public string CurrentComPort
	{
		get { return _connectSlappyBell != null ? _connectSlappyBell.Port : "未接続"; }
	}

	public string ConnectSlappyBell
	{
		get { return _connectSlappyBell != null ? _connectSlappyBell.Description : "未接続"; }
	}

	public bool IsSlappyBellConnected
	{
		get { return _connectSlappyBell != null; }
	}

	public string Slot0Channel => GetSlotBindChannel(0);
	public string Slot1Channel => GetSlotBindChannel(1);
	public string Slot2Channel => GetSlotBindChannel(2);
	public string Slot3Channel => GetSlotBindChannel(3);
	public string Slot4Channel => GetSlotBindChannel(4);
	public string Slot5Channel => GetSlotBindChannel(5);

	public ICommand OpenSlackSettingsCommand { get; }
	public ICommand OpenNotifySettingsCommand { get; }
	public ICommand ConnectWiFiCommand { get; }

	public MainViewModel(SettingsStore settingsStore,
		SlappyBellController slappyBellController, SlackSettingsViewModel slackSettings,
		NotifySettingsViewModel notifySettings, NotificationRouter router, UsbWatcher usbWatcher)
	{
		_settingsStore = settingsStore;
		SoundVolume = settingsStore.Settings.Volume;
		SoundMute = settingsStore.Settings.Mute;
		WifiSSID = settingsStore.Settings.WifiSsid ?? "";
		WifiPassword = settingsStore.Settings.WifiPassword ?? "";

		SlackSettings = slackSettings;
		NotifySettings = notifySettings;

		OpenSlackSettingsCommand = new RelayCommand(() =>
		{
			SlackSettings.LoadFrom(settingsStore.Settings);
			SlackSettingsRequested?.Invoke(this, true);
		});
		OpenNotifySettingsCommand = new RelayCommand(p =>
		{
			int slot;
			if (p is int i) slot = i;
			else if (p is string s && int.TryParse(s, out var j)) slot = j;
			else return;
			NotifySettings.LoadFrom(settingsStore.Settings, slot);
			NotifySettingsRequested?.Invoke(this, true);
		});

		ConnectWiFiCommand = new AsyncRelayCommand(ConnectWiFiAsync);

		slackSettings.RequestClose += (sender, ok) =>
		{
			if (ok)
			{
				_settingsStore.Update(s => SlackSettings.ApplyTo(s));
			}

			SlackSettingsRequested?.Invoke(this, false);
		};
		notifySettings.RequestClose += (sender, ok) =>
		{
			if (ok)
			{
				_settingsStore.Update(s => NotifySettings.ApplyTo(s));
			}

			NotifySettingsRequested?.Invoke(this, false);
		};
		usbWatcher.Added += (sender, e) =>
		{
			if (_connectSlappyBell == null)
			{
				_connectSlappyBell = e;
				OnPropertyChanged(nameof(CurrentComPort));
				OnPropertyChanged(nameof(IsSlappyBellConnected));
				OnPropertyChanged(nameof(ConnectSlappyBell));
			}
		};
		usbWatcher.Removed += (sender, e) =>
		{
			if (_connectSlappyBell.Port == e.Port)
			{
				_connectSlappyBell = null;
				OnPropertyChanged(nameof(CurrentComPort));
				OnPropertyChanged(nameof(IsSlappyBellConnected));
				OnPropertyChanged(nameof(ConnectSlappyBell));
			}
		};
		slappyBellController.SlappyDeviceConnected += async (sender, e) =>
		{
			_slappyDevice = e;
			var r = await _slappyDevice.WiFiStatus();
			if (r.Code == ReceiveMessage.ResultCode.Success)
			{
				Debug.WriteLine($"WifiStatus: {r.Message}");
			}

			_slappyDevice.OnWifiStatusChanged += (o, i) =>
			{
				switch ((ReceiveMessage.ResultCode)i)
				{
					case ReceiveMessage.ResultCode.WifiConnected:
					case ReceiveMessage.ResultCode.WifiSsidNotFound:
					case ReceiveMessage.ResultCode.WifiAuthFail:
					case ReceiveMessage.ResultCode.WifiDisconnected:
						WifiStatusCode = i;
						break;
				}
			};
		};
		slappyBellController.SlappyDeviceDisconnected += (sender, e) =>
		{
			_slappyDevice = null;
			OnPropertyChanged(nameof(IsSlappyBellConnected));
		};
		
		router.OnNotifySourceChange += (_, e) =>
		{
			if (e.ChannelSource == ChannelSourceMode.Socket)
				_isSlackConnected = e.IsConnected;
			else if(e.ChannelSource == ChannelSourceMode.WindowsNotify)
				_isWinNotificationConnected = e.IsConnected;
			OnPropertyChanged(nameof(IsSlackConnected));
			OnPropertyChanged(nameof(SlackConnectionText));
		};
	}

	public void CommitSoundSettings()
	{
		if (_settingsStore.Settings.Volume == SoundVolume &&
		    _settingsStore.Settings.Mute == SoundMute)
		{
			return;
		}

		_settingsStore.Update(s =>
		{
			return s with
			{
				Volume = _soundVolume,
				Mute = _soundMute
			};
		});
	}

	private async Task ConnectWiFiAsync()
	{
		string? error = null;
		if (_slappyDevice == null)
		{
			error = "SlappyBellが接続していません";
		}

		if (error == null && string.IsNullOrEmpty(WifiSSID))
		{
			error = "SSIDを入力してください";
		}

		if (error == null && string.IsNullOrEmpty(WifiPassword))
		{
			error = "パスワードを入力してください";
		}

		if (error != null)
		{
			System.Windows.MessageBox.Show(error, "Wi-Fi接続エラー");
			return;
		}

		var r = await _slappyDevice!.ConnectWifi(WifiSSID, SettingsStore.UnprotectString(WifiPassword));
		if (r.Code != ReceiveMessage.ResultCode.Success)
			MessageBox.Show($"Wi-Fi接続に失敗しました。{r.Message}", "Wi-Fi接続エラー");
	}

	public void CommitWiFiSettings()
	{
		if (_settingsStore.Settings.WifiSsid == WifiSSID &&
		    _settingsStore.Settings.WifiPassword == WifiPassword)
		{
			return;
		}

		_settingsStore.Update(s =>
		{
			return s with
			{
				WifiSsid = _wifiSsid,
				WifiPassword = _wifiPassword
			};
		});
	}

	private string GetSlotBindChannel(int slot)
	{
		var ch = _settingsStore.Settings.NotifySettings[slot].Channel;
		if (string.IsNullOrEmpty(ch))
			return "未構成";
		if (ch == "[DM]")
			return "ダイレクトメッセージ";
		return ch;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	/*protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}*/
}