using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;

namespace SlappyHub.Services;

public class MessageSourceController
{
	private SettingsStore _settingsStore;
	private SlackConnector _slackConnector;
	private WindowsNotificationConnector _windowsNotificationConnector;
	private AppSettings _settings;
	
	public MessageSourceController(SettingsStore settingsStore, SlackConnector slackConnector,
		WindowsNotificationConnector windowsNotificationConnector)
	{
		_settingsStore = settingsStore;
		_slackConnector = slackConnector;
		_windowsNotificationConnector = windowsNotificationConnector;
		_settings = settingsStore.Settings;

		_settingsStore.Changed += async (sender,newSettings) =>
		{
			var ooldSettings = _settings;
			_settings = newSettings;
			if (ooldSettings.ChannelSource != newSettings.ChannelSource)
			{
				if (ooldSettings.ChannelSource == ChannelSourceMode.Socket)
				{
					_slackConnector.Stop();
				}

				if (ooldSettings.ChannelSource == ChannelSourceMode.WindowsNotify)
				{
					if (!ooldSettings.EnableDirectMessage)
						_windowsNotificationConnector.Stop();
				}

				if (newSettings.ChannelSource == ChannelSourceMode.Socket)
				{
					await _slackConnector.Start(newSettings.SlackAppToken, newSettings.SlackBotToken);
				}

				if (newSettings.ChannelSource == ChannelSourceMode.WindowsNotify || newSettings.EnableDirectMessage)
				{
					await _windowsNotificationConnector.Start(
						newSettings.ChannelSource == ChannelSourceMode.WindowsNotify, 
						newSettings.EnableDirectMessage,
						newSettings.CaptureWorkspace);
				}
			}
		};
	}

	public async Task Start()
	{
		string? error = null;
		if (_settings.ChannelSource == ChannelSourceMode.Socket)
		{
			try
			{
				await _slackConnector.Start(_settings.SlackAppToken, _settings.SlackBotToken);
			}
			catch (Exception e)
			{
				error = $"Slack Serviceへ接続できませんでした。\r\n{e.Message}";
			}
		}
		if (_settings.ChannelSource == ChannelSourceMode.WindowsNotify || _settings.EnableDirectMessage)
		{
			try
			{
				await _windowsNotificationConnector.Start(
					_settings.ChannelSource == ChannelSourceMode.WindowsNotify,
					_settings.EnableDirectMessage,
					_settings.CaptureWorkspace);
			}
			catch (Exception e)
			{
				error = $"通知に接続できませんでした。\r\n{e.Message}";
			}
		}

		if (error != null)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				MessageBox.Show(
					Application.Current.MainWindow,
					error,
					"接続エラー",
					MessageBoxButton.OK,
				MessageBoxImage.Error);
			});
		}
	}
}