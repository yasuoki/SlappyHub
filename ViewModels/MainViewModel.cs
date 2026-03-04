using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SlappyHub.Common;
using SlappyHub.Models;
using SlappyHub.Services;
using Application = System.Windows.Application;


namespace SlappyHub.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
	private SettingsStore _settingsStore;
	private SlappyBellController _slappyBellController;
	private BleWatcher _bleWatcher;
	public event PropertyChangedEventHandler? PropertyChanged;
	public event EventHandler<bool>? SlackSettingsRequested;
	public event EventHandler<bool>? NotifySettingsRequested;
	public SlackSettingsViewModel SlackSettings { get; }
	public NotifySettingsViewModel NotifySettings { get; }

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	//-----------------------------------------
	// Slack Connection Control
	//-----------------------------------------
	private bool _isSlackConnected;
	private bool _isWinNotificationConnected;
	public bool IsSlackConnected {
		get
		{
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
	
	//-----------------------------------------
	// SlappyBell Device Control
	//-----------------------------------------
	private ObservableCollection<DeviceInfo> _devicePorts = new();
	public ObservableCollection<DeviceInfo> DevicePorts => _devicePorts;

	private SlappyDevice? _slappyDevice;
	public bool IsSlappyBellConnected => _slappyDevice != null && _slappyDevice.IsConnected;
	public bool IsSlappyBellConnecting => _connectingDeviceAddress != null;

	public string? ConnectedDeviceAddress
	{
		get
		{
			if (_slappyDevice == null || _slappyDevice.Driver == null || !_slappyDevice.Driver.Port.IsConnected)
				return null;
			return _slappyDevice.Driver.Port.Address;
		}
	}
	public string ConnectedDeviceText
	{
		get
		{
			string? info = null;
			if (_slappyDevice != null && _slappyDevice.IsConnected)
				info = _slappyDevice.Description;
			return info ?? "未接続";
		}
	}

	private bool _isDeviceSelectorOpened = false;
	public void DeviceSelectorOpen()
	{
		_isDeviceSelectorOpened = true;
		_bleWatcher.Start();
	}
	public void DeviceSelectorClose()
	{
		_isDeviceSelectorOpened = false;
		var port = _slappyDevice?.Driver?.Port;
		if(port == null || port is BleDevicePort)
			_bleWatcher.Stop();
		CommitSlappyBellConnection();
	}
	
	private string? _connectingDeviceAddress;
	public string? ConnectingDeviceAddress
	{
		get { return _connectingDeviceAddress; }
		set 
		{
			if (_connectingDeviceAddress == value) return;
			_connectingDeviceAddress = value;
			OnPropertyChanged(nameof(ConnectingDeviceAddress));
			OnPropertyChanged(nameof(IsSlappyBellConnecting));
		}
	}

	public async Task ConnectToSlappyBell(DeviceInfo device)
	{
		Debug.WriteLine($"MainViewModel.Connect: {device.Transport} {device.Address}");
		if (device.Address == ConnectedDeviceAddress)
			return;
		if(device.Transport == "BLE")
			_bleWatcher.Stop();
		try
		{
			ConnectingDeviceAddress = null;
			await _slappyBellController.Disconnect();
			ConnectingDeviceAddress = device.Address;
			await _slappyBellController.Connect(device);
		}
		catch (Exception e)
		{
			ConnectingDeviceAddress = null;
			MessageBox.Show($"Failed to connect to device {device.Description}\r\n{e.Message}");
		}
	}
	public void CommitSlappyBellConnection()
	{
		var newAddress = ConnectedDeviceAddress;
		if(ConnectingDeviceAddress != null)
			newAddress = ConnectingDeviceAddress;

		if (newAddress == null || _settingsStore.Settings.SlappyBellAddress == newAddress)
		{
			return;
		}

		_settingsStore.Update(s =>
		{
			return s with
			{
				SlappyBellAddress = newAddress
			};
		});
	}

	//-----------------------------------------
	// Sound settings
	//-----------------------------------------
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
	//-----------------------------------------
	// WiFi Control
	//-----------------------------------------
	public string _wifiSsid;
	public string WiFiSSID
	{
		get => _wifiSsid;
		set
		{
			_wifiSsid = value;
			OnPropertyChanged(nameof(WiFiSSID));
		}
	}
	public string _wifiPassword;

	public string WiFiPassword
	{
		get => _wifiPassword;
		set
		{
			_wifiPassword = value;
			OnPropertyChanged(nameof(WiFiPassword));
		}
	}

	public string WiFiButtonText => WiFiStatusText == "接続" ? "切断" : "接続";
	public string WiFiStatusText
	{
		get
		{
			switch ((ReceiveMessage.ResultCode)_wifiStatusCode)
			{
				case ReceiveMessage.ResultCode.WiFiConnected:
					return "接続";
				case ReceiveMessage.ResultCode.WiFiSsidNotFound:
					return "SSIDが見つかりません";
				case ReceiveMessage.ResultCode.WiFiAuthFail:
					return "認証エラー";
				case ReceiveMessage.ResultCode.WiFiDisconnected:
					return "切断";
				default:
					return "未接続";
			}
		}
	}
	public bool IsWiFiConnected => _slappyDevice != null && _wifiStatusCode == (int)ReceiveMessage.ResultCode.WiFiConnected;
	private int _wifiStatusCode = (int)ReceiveMessage.ResultCode.WiFiDisconnected;
	public int WiFiStatusCode
	{
		get => _wifiStatusCode;
		set
		{
			if (_wifiStatusCode != value)
			{
				_wifiStatusCode = value;
				OnPropertyChanged(nameof(WiFiStatusCode));
				OnPropertyChanged(nameof(WiFiStatusText));
				OnPropertyChanged(nameof(IsWiFiConnected));
				OnPropertyChanged(nameof(WiFiButtonText));
			}
		}
	}
	private async Task WiFiActionAsync()
	{
		if (IsWiFiConnected)
		{
			await DisconnectWiFiAsync();
		}
		else
		{
			await ConnectWiFiAsync();
		}
	}
	private async Task ConnectWiFiAsync() {
		string? error = null;
		if (_slappyDevice == null)
		{
			error = "SlappyBellが接続していません";
		}

		if (error == null && string.IsNullOrEmpty(WiFiSSID))
		{
			error = "SSIDを入力してください";
		}

		if (error == null && string.IsNullOrEmpty(WiFiPassword))
		{
			error = "パスワードを入力してください";
		}

		if (error != null)
		{
			System.Windows.MessageBox.Show(error, "Wi-Fi接続エラー");
			return;
		}

		var r = await _slappyDevice!.ConnectWiFi(WiFiSSID, SettingsStore.UnprotectString(WiFiPassword));
		if (r.Code != ReceiveMessage.ResultCode.Success)
			MessageBox.Show($"Wi-Fi接続に失敗しました。{r.Message}", "Wi-Fi接続エラー");
	}
	private async Task DisconnectWiFiAsync()
	{
		if (_slappyDevice == null)
			return;

		var r = await _slappyDevice.DisconnectWiFi();
		WiFiSSID = "";
		WiFiPassword = "";
		WiFiStatusCode = (int)ReceiveMessage.ResultCode.WiFiDisconnected;
		OnPropertyChanged(nameof(WiFiStatusCode));
		OnPropertyChanged(nameof(WiFiSSID));
		OnPropertyChanged(nameof(WiFiPassword));
	}
	public void CommitWiFiSettings()
	{
		if (_settingsStore.Settings.WiFiSsid == WiFiSSID &&
		    _settingsStore.Settings.WiFiPassword == WiFiPassword)
		{
			return;
		}

		_settingsStore.Update(s =>
		{
			return s with
			{
				WiFiSsid = _wifiSsid,
				WiFiPassword = _wifiPassword
			};
		});
	}
	//-----------------------------------------
	// Slot Settings
	//-----------------------------------------
	private string GetSlotBindChannel(int slot)
	{
		var ch = _settingsStore.Settings.NotifySettings[slot].Channel;
		if (string.IsNullOrEmpty(ch))
			return "未構成";
		if (ch == "[DM]")
			return "ダイレクトメッセージ";
		return ch;
	}

	public string Slot0Channel => GetSlotBindChannel(0);
	public string Slot1Channel => GetSlotBindChannel(1);
	public string Slot2Channel => GetSlotBindChannel(2);
	public string Slot3Channel => GetSlotBindChannel(3);
	public string Slot4Channel => GetSlotBindChannel(4);
	public string Slot5Channel => GetSlotBindChannel(5);

	public ICommand OpenSlackSettingsCommand { get; }
	public ICommand OpenNotifySettingsCommand { get; }
	public ICommand WiFiActionCommand { get; }

	public MainViewModel(SettingsStore settingsStore,
		SlappyBellController slappyBellController, SlackSettingsViewModel slackSettings,
		NotifySettingsViewModel notifySettings, NotificationRouter router, UsbWatcher usbWatcher, BleWatcher bleWatcher)
	{
		_wifiSsid = "";
		_wifiPassword = "";
		_settingsStore = settingsStore;
		_slappyBellController = slappyBellController;
		_bleWatcher = bleWatcher;
		SoundVolume = settingsStore.Settings.Volume;
		SoundMute = settingsStore.Settings.Mute;
		WiFiSSID = settingsStore.Settings.WiFiSsid ?? "";
		WiFiPassword = settingsStore.Settings.WiFiPassword ?? "";

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

		WiFiActionCommand = new AsyncRelayCommand(WiFiActionAsync);

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
		usbWatcher.Added += async (sender, port) =>
		{
			if (_settingsStore.Settings.SlappyBellAddress == port.Address)
			{
				await ConnectToSlappyBell(port);
			}
			if (_devicePorts.FirstOrDefault(n => n.Address == port.Address) != null)
				return;
			Application.Current.Dispatcher.Invoke(() =>
			{
				_devicePorts.Add(port);				
			});
		};
		usbWatcher.Removed += (sender, port) =>
		{
			if (port.Address == _slappyDevice?.Driver?.Port.Address)
				return;
			Application.Current.Dispatcher.Invoke(() =>
			{
				_devicePorts.Remove(port);
			});
		};
		bleWatcher.Added += async (sender, port) =>
		{
			if (_settingsStore.Settings.SlappyBellAddress == port.Address)
			{
				await ConnectToSlappyBell(port);
			}
			if (_devicePorts.FirstOrDefault(n => n.Address == port.Address) != null)
				return;
			Application.Current.Dispatcher.Invoke(() =>
			{
				_devicePorts.Add(port);
			});

		};
		bleWatcher.Removed += (sender, port) =>
		{
			if (port.Address == _slappyDevice?.Driver?.Port.Address)
				return;
			Application.Current.Dispatcher.Invoke(() =>
			{
				_devicePorts.Remove(port);
			});
		};
		
		slappyBellController.SlappyDeviceConnected += async (sender, device) =>
		{
			_slappyDevice = device;
			ConnectingDeviceAddress = null;
			OnPropertyChanged(nameof(IsSlappyBellConnected));
			OnPropertyChanged(nameof(IsWiFiConnected));
			OnPropertyChanged(nameof(ConnectedDeviceAddress));
			OnPropertyChanged(nameof(ConnectedDeviceText));
			OnPropertyChanged(nameof(DevicePorts));

			_slappyDevice.OnWiFiStatusChanged += (o, i) =>
			{
				switch ((ReceiveMessage.ResultCode)i)
				{
					case ReceiveMessage.ResultCode.WiFiConnected:
					case ReceiveMessage.ResultCode.WiFiSsidNotFound:
					case ReceiveMessage.ResultCode.WiFiAuthFail:
					case ReceiveMessage.ResultCode.WiFiDisconnected:
						WiFiStatusCode = i;
						break;
				}
			};
			if (_slappyDevice?.Driver?.Port is BleDevicePort && !_isDeviceSelectorOpened)
			{
				_bleWatcher.Stop();
			} else if (_isDeviceSelectorOpened)
			{
				_bleWatcher.Start();
			}
		};
		slappyBellController.SlappyDeviceDisconnected += (sender, e) =>
		{
			_slappyDevice = null;
			ConnectingDeviceAddress = null;
			WiFiStatusCode = (int)ReceiveMessage.ResultCode.WiFiDisconnected;
			OnPropertyChanged(nameof(IsSlappyBellConnected));
			OnPropertyChanged(nameof(ConnectedDeviceAddress));
			OnPropertyChanged(nameof(ConnectedDeviceText));
			OnPropertyChanged(nameof(DevicePorts));
			_bleWatcher.Start();
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
}