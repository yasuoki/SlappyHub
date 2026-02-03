using System.Diagnostics;
using SlappyHub.Models;

namespace SlappyHub.Services;

public class SlappyBellController
{
	private bool _isStarted = false;
	private AppSettings _settings;
	private SlappyDevice? _slappyDevice;
	private SlackAppWatcher _slackAppWatcher;
	private List<List<string>> _senderFilter = new ();
	private List<List<string>> _textFilter = new();
	public event EventHandler<SlappyDevice>? SlappyDeviceConnected; 
	public event EventHandler SlappyDeviceDisconnected; 

	public SlappyBellController(SettingsStore settingsStore, SlackAppWatcher slackAppWatcher, NotificationRouter router, UsbWatcher usbWatcher)
	{
		_settings = settingsStore.Settings;
		_slackAppWatcher = slackAppWatcher;
		settingsStore.Changed += async (sender, settings) =>
		{
			await ApllySettings(settings);
		};
		usbWatcher.Added += async (sender, e) =>
		{
			if (_slappyDevice == null)
			{
				_slappyDevice =  await SlappyDevice.Connect(e);
				await ApllySettings(_settings);
				SlappyDeviceConnected?.Invoke(this, _slappyDevice);
			}
		};
		usbWatcher.Removed += (sender, e) =>
		{
			if (_slappyDevice != null && _slappyDevice.Info.Port == e.Port)
			{
				_slappyDevice.Disconnect();
				_slappyDevice = null;
				SlappyDeviceDisconnected?.Invoke(this, EventArgs.Empty);
			}
		};
		router.OnMessage += async (sender, e) =>
		{
			if(!_isStarted || _slappyDevice == null)
				return;
			if (e.Channel == "[DM]")
				await OnDirectMessage(e);
			else
				await OnChannelMessage(e);
		};
		
		router.OnChangeView += async (sender, e) =>
		{
			if(!_isStarted || _slappyDevice == null)
				return;
			await OnSlackViewChange(e);
		};
	}
	
	public void Start()
	{
		_isStarted = true;
		ApllySettings(_settings);
	}

	private async Task ApllySettings(AppSettings settings)
	{
		_settings = settings;
		if (_slappyDevice != null)
		{
			if(settings.Mute)
				await _slappyDevice.SetVolume(0);
			else
				await _slappyDevice.SetVolume(settings.Volume);
			if(!string.IsNullOrEmpty(settings.WiFiSsid) && !string.IsNullOrEmpty(settings.WiFiPassword))
				await _slappyDevice.ConnectWiFi(settings.WiFiSsid, SettingsStore.UnprotectString(settings.WiFiPassword));

			_senderFilter.Clear();
			_textFilter.Clear();
			for(var i = 0; i < settings.NotifySettings.Length; i++)
			{
				var slot = settings.NotifySettings[i];
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
	}
	
	private async Task OnDirectMessage(NotificationEvent e)
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
				if (!string.IsNullOrEmpty(slot.LedPattern))
				{
					await _slappyDevice.LedOn(i, slot.LedPattern);
				}

				if (!_slackAppWatcher.IsSourceAppForeground(e.Source))
				{
					if (!string.IsNullOrEmpty(slot.Sound))
					{
						await _slappyDevice.Play(slot.Sound);
					}
				}
			}
		}
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
				if (!string.IsNullOrEmpty(slot.LedPattern))
				{
					await _slappyDevice.LedOn(i, slot.LedPattern);
				}

				if (!_slackAppWatcher.IsSourceAppForeground(e.Source))
				{
					if (!string.IsNullOrEmpty(slot.Sound))
					{
						await _slappyDevice.Play(slot.Sound);
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
				await _slappyDevice.LedOff(i);
			}
		}
	}
}
