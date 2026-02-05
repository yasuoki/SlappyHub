using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SlackNet;
using SlappyHub.Models;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;

namespace SlappyHub.Services;

public sealed class NotificationRouter
{
	private bool _started = false;
	private bool _watchDirectMessages = false;
	private List<string> _watchChannels = new List<string>();
	public event EventHandler<NotificationEvent>? OnMessage;
	public event EventHandler<ViewChangeEvent>? OnChangeView;
	public event EventHandler<SourceChangeEvent>? OnNotifySourceChange;

	public NotificationRouter(SettingsStore settingsStore, SlackAppWatcher slackAppWatcher, SlackConnector slackConnector, WindowsNotificationConnector windowsNotificationSource)
	{
		settingsStore.Changed += (_, newSettings) =>
		{
			UpdateWatchTarget(newSettings);
		};
		UpdateWatchTarget(settingsStore.Settings);
		slackConnector.OnMessage += (_, e) => RouteNotify(e);
		slackConnector.OnConnectionChanged += (_, e) => RouteSlackConenctionChanged(e);
		windowsNotificationSource.OnMessage += (_, e) => RouteNotify(e);
		windowsNotificationSource.OnConnectionChanged += (_, e) => RouteWindowNotificationConnectionChanged(e);
		slackAppWatcher.OnChangeView += (_, e) => RouteView(e);
	}
	
	private void UpdateWatchTarget(AppSettings settings)
	{
		var watchChannels = new List<string>();
		foreach (var slot in settings.NotifySettings)
		{
			if (!string.IsNullOrEmpty(slot.Channel))
			{
				if(!slot.Channel.Equals("[DM]", StringComparison.OrdinalIgnoreCase))
				{
					watchChannels.Add(slot.Channel);
				}
			}
		}
		_watchDirectMessages = settings.EnableDirectMessage || settings.ChannelSource == ChannelSourceMode.WindowsNotify;
		_watchChannels = watchChannels;
	}

	public void Start()
	{
		_started = true;
	}

	private void RouteNotify(NotificationEvent e)
	{
		if (_started && _watchChannels.Contains(e.Channel) )
			OnMessage?.Invoke(this, e);
	}

	private void RouteSlackConenctionChanged(bool connected)
	{
		if(_started)
			OnNotifySourceChange?.Invoke(this, new SourceChangeEvent(ChannelSourceMode.Socket, connected));
	}

	private void RouteWindowNotificationConnectionChanged(bool connected)
	{
		if(_started)
			OnNotifySourceChange?.Invoke(this, new SourceChangeEvent(ChannelSourceMode.WindowsNotify, connected));
	}

	private void RouteView(ViewChangeEvent e)
	{
		if(_started)
			OnChangeView?.Invoke(this, e);
	}
}
