using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Foundation.Metadata;
using SlappyHub.Models;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace SlappyHub.Services.Notifications;

public class WindowsNotificationConnector
{
	private bool _started;
	private SettingsStore _settingsStore;
	private CancellationTokenSource? _cts;
	private Task? _loopTask;
	private int _pending;
	private readonly SemaphoreSlim _wake = new(0);
	
	private bool _captureChannelMessage;
	private string? _captureWorkspace;
	
	private readonly HashSet<uint> _seenIds = new();
	
	public event EventHandler<NotificationEvent>? OnMessage;
	public event EventHandler<bool>? OnConnectionChanged;
	public bool IsStarted  => _started;
	public WindowsNotificationConnector(SettingsStore settingsStore)
	{
		_started = false;
		_settingsStore = settingsStore;
		_settingsStore.Changed += (_, _) =>
		{
			Debug.WriteLine("WindowsNotificationConnector: Settings changed");
		};
	}

	public async Task Start(bool captureChannelMessage, string? captureWorkspace)
	{
		if(_started)
			return;
		_started = true;
		_cts = new CancellationTokenSource();
		_captureChannelMessage = captureChannelMessage;
		_captureWorkspace = captureWorkspace;
		
		if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
		{
			Stop();
			throw new Exception("通知の取得に対応していません");
		}
		UserNotificationListener listener = UserNotificationListener.Current;
		var access = await listener.RequestAccessAsync();
		if (access != UserNotificationListenerAccessStatus.Allowed)
		{
			Stop();
			throw new Exception("通知に接続できませんでした。");
		}
		await PrimeSeenSetAsync(listener, _cts!.Token);
		_loopTask = NofiticationWatcher(listener, _cts!.Token);
		_started = true;
		OnConnectionChanged?.Invoke(this, true);
	}
	
	public void Stop()
	{
		CancellationTokenSource? cts;
		Task? loop;
		if (!_started) return;
		_started = false;

		cts = _cts;
		loop = _loopTask;

		_cts = null;
		_loopTask = null;
		_captureWorkspace = null;
		try { cts?.Cancel(); } catch { /* ignore */ }
		OnConnectionChanged?.Invoke(this, false);
	}
	
	public void Wake()
	{
		if (Interlocked.Exchange(ref _pending, 1) == 0)
			_wake.Release();
	}

	private async Task PrimeSeenSetAsync(UserNotificationListener listener, CancellationToken ct)
	{
		try
		{
			var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(ct);
			foreach (var n in notifications)
			{
				if (!_seenIds.Contains(n.Id))
					_seenIds.Add(n.Id);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Debug.WriteLine($"WindowsNotificationConnector: PrimeSeenSetAsync failed: {ex}");
		}
	}
	
	private async Task NofiticationWatcher(UserNotificationListener listener, CancellationToken ct)
	{
		try
		{
			while (!ct.IsCancellationRequested)
			{
				bool signaled = await _wake.WaitAsync(10000, ct).ConfigureAwait(false);
				if(signaled)
					await Task.Delay(2000);
				Interlocked.Exchange(ref _pending, 0);
				IReadOnlyList<UserNotification> list;
				try
				{
					list = await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(ct);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"WindowsNotificationConnector: GetNotificationsAsync failed: {ex}");
					continue;
				}

				var currentIds = new HashSet<uint>();
				foreach (var n in list)
				{
					currentIds.Add(n.Id);
					if (_seenIds.Contains(n.Id))
						continue; // 既知
					var evt = TryBuildEvent(n);
					if (evt is null) continue;

					try
					{
						OnMessage?.Invoke(this, evt);
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"WindowsNotificationConnector: Router.Route failed: {ex}");
					}
				}

				_seenIds.Clear();
				_seenIds.UnionWith(currentIds);

				while (_wake.CurrentCount > 0 && _wake.Wait(0))
				{
				}
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Debug.WriteLine($"WindowsNotificationConnector: loop crashed: {ex}");
		}
	}

	private NotificationEvent? TryBuildEvent(UserNotification n)
	{
		string appName = "(unknown)";
		try
		{
			appName = n.AppInfo?.DisplayInfo?.DisplayName ?? "(unknown)";
		}
		catch { /* ignore */ }

		string? title = null;
		string? body = null;

		try
		{
			NotificationBinding toastBinding = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
			if (toastBinding != null)
			{
				IReadOnlyList<AdaptiveNotificationText> textElements = toastBinding.GetTextElements();
				title = textElements.FirstOrDefault()?.Text;
				body = string.Join("\n", textElements.Skip(1).Select(t => t.Text));
			}
		}
		catch
		{
			// ignore parse errors
		}
		
		bool isDirectMessage =
			title.Length > 0 &&
			!title.StartsWith("#"); // チャンネル名は #xxx

		string sender;
		if (isDirectMessage)
			sender = title;
		else
		{
			var seg = body.Split(":");
			sender = seg[0].Trim();
			body = string.Join("", seg.Skip(1));
		}

		NotificationEvent? ev = null;
		if (appName.Contains("slack", StringComparison.OrdinalIgnoreCase))
		{
			if (isDirectMessage)
			{
				ev = new NotificationEvent("notify", "[DM]", sender, body);
			}
			else
			{
				if (_captureChannelMessage)
				{
					if(title.StartsWith("#"))
						title = title.Substring(1).Trim();
					ev = new NotificationEvent("notify", title, sender, body);
				}
			}
		}
		return ev;
	}

}