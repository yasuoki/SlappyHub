using System.Collections.ObjectModel;
using System.Diagnostics;
using SlappyHub.Models;

namespace SlappyHub.Services;

public class SlappyBellController
{
	private bool _isStarted = false;
	private AppSettings _settings;
	private SlappyDevice _slappyDevice;
	private SlackAppWatcher _slackAppWatcher;
	private List<List<string>> _senderFilter = new ();
	private List<List<string>> _textFilter = new();
	public event EventHandler<SlappyDevice>? SlappyDeviceConnected; 
	public event EventHandler<SlappyDevice>? SlappyDeviceDisconnected; 

	public SlappyBellController(SettingsStore settingsStore, SlackAppWatcher slackAppWatcher, NotificationRouter router, BleWatcher bleWatcher, UsbWatcher usbWatcher, PowerWatcher powerWatcher)
	{
		_slappyDevice = new SlappyDevice();
		_settings = settingsStore.Settings;
		_slackAppWatcher = slackAppWatcher;
		settingsStore.Changed += async (sender, chg) =>
		{
			await OnChangeSettings(chg);
		};
			
		router.OnMessage += async (sender, e) =>
		{
			if(!_isStarted || _slappyDevice == null)
				return;
			await OnChannelMessage(e);
		};
		
		router.OnChangeView += async (sender, e) =>
		{
			if(!_isStarted || _slappyDevice == null)
				return;
			await OnSlackViewChange(e);
		};
	}
	
	public async Task Start()
	{
		_isStarted = true;
		await ApllySettings(_settings, null);
	}

	public bool IsConnected() => _slappyDevice.IsConnected;
	public IDevicePort? ConnectedDevicePort => _slappyDevice?.Driver?.Port;
	public async Task Connect(DeviceInfo device)
	{
		Debug.WriteLine($"Connect: {device.Transport} {device.Address}");
		IDevicePort port;
		if (device.Transport == "USB")
		{
			port = new UsbDevicePort(device);
		}
		else if (device.Transport == "BLE")
		{
			port = new BleDevicePort(device);
		}
		else
		{
			throw new Exception($"Unknown transport: {device.Transport}");
		}
		port.Disconected += OnDevicePortDisconnected;
		var driver = new PortDriver(port);
		await _slappyDevice.Attach(driver);
		await ApllySettings(_settings);
		SlappyDeviceConnected?.Invoke(this, _slappyDevice);
	}

	public async Task Disconnect()
	{
		if (!_slappyDevice.IsConnected)
			return;
		SlappyDeviceDisconnected?.Invoke(this, _slappyDevice);
		await _slappyDevice.Detouch();
	}
	
	private void OnDevicePortDisconnected(IDevicePort? port, object _)
	{
		if (port != null)
		{
			Debug.WriteLine($"SlappyBellController::OnDevicePortDisconnected: {port.Address}");
			if (_slappyDevice.Driver?.Port == port)
			{
				SlappyDeviceDisconnected?.Invoke(this, _slappyDevice);
			}
		}
		//Disconnect();
	}
	

	private async Task ApllySettings(AppSettings settings, AppSettings? oldSettings=null)
	{
		var updateVolume = true;
		var updateWifi = true;
		
		if (oldSettings != null)
		{
			updateVolume = settings.Mute != oldSettings.Mute || settings.Volume != oldSettings.Volume;
			updateWifi = false;
			if (settings.WiFiPassword != null && oldSettings.WiFiPassword != null &&
			    settings.WiFiPassword != oldSettings.WiFiPassword)
			{
				updateWifi = SettingsStore.UnprotectString(settings.WiFiPassword) !=
				             SettingsStore.UnprotectString(oldSettings.WiFiPassword);
			}
			else if (settings.WiFiPassword == null && oldSettings.WiFiPassword != null)
			{
				updateWifi = true;
			}
			else if (settings.WiFiPassword != null && oldSettings.WiFiPassword == null)
			{
				updateWifi = true;
			}
		}
		
		_settings = settings;
		if (updateVolume)
		{
			if(settings.Mute)
				await _slappyDevice.SetVolume(0);
			else
				await _slappyDevice.SetVolume(settings.Volume);
		}

		if (updateWifi)
		{
			if (!string.IsNullOrEmpty(settings.WiFiSsid) && !string.IsNullOrEmpty(settings.WiFiPassword))
			{
				await _slappyDevice.ConnectWiFi(settings.WiFiSsid,
					SettingsStore.UnprotectString(settings.WiFiPassword));
			}
		}
		_senderFilter.Clear();
		_textFilter.Clear();
		for(var i = 0; i < _settings.NotifySettings.Length; i++)
		{
			var slot = _settings.NotifySettings[i];
			_senderFilter.Add(new());
			if(!string.IsNullOrEmpty(slot.SenderFilter))
			{
				var list = slot.SenderFilter.Split(',').ToList();
				foreach(var s in list) _senderFilter[i].Add(s.Trim());
			}

			_textFilter.Add(new());
			if(!string.IsNullOrEmpty(slot.TextFilter))
			{
				var list = slot.TextFilter.Split(',').ToList();
				foreach(var s in list) _textFilter[i].Add(s.Trim());
			}
		}
		
	}

	private async Task OnChangeSettings(SettingsChangedEventArgs chg)
	{
		await ApllySettings(chg.NewSettings, chg.OldSettings);
	}

	private async Task OnChannelMessage(NotificationEvent e)
	{
		var n = _settings.NotifySettings;
		for(int i = 0; i < n.Length; i++)
		{
			var slot = n[i];
			var senderFilter = _senderFilter[i];
			var textFilter = _textFilter[i];
			if (e.Channel.Equals(slot.Channel, StringComparison.OrdinalIgnoreCase))
			{
				if(senderFilter.Contains(e.Sender))
					return;
				foreach (var filter in textFilter)
				{
					if (e.Text.Contains(filter))
						return;
				}
				var ledPattern = e.LedPattern;
				if (string.IsNullOrEmpty(ledPattern))
					ledPattern = slot.LedPattern;
				if (!string.IsNullOrEmpty(ledPattern))
				{
					if(_slappyDevice != null)
						await _slappyDevice.LedOn(i, ledPattern);
				}

				if (!_slackAppWatcher.IsSourceAppForeground(e.Source))
				{
					var sound = e.Sound;
					if(string.IsNullOrEmpty(sound))
						sound = slot.Sound;
					if (!string.IsNullOrEmpty(sound))
					{
						if(_slappyDevice != null)
							await _slappyDevice.Play(sound);
					}
				}
			}
		}
	}

	private async Task OnSlackViewChange(ViewChangeEvent e)
	{
		var n = _settings.NotifySettings;
		for(int i = 0; i < n.Length; i++)
		{
			var slot = n[i];
			if (e.Channel.Equals(slot.Channel, StringComparison.OrdinalIgnoreCase))
			{
				if(_slappyDevice != null)
					await _slappyDevice.LedOff(i);
			}
		}
	}
}
