using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SlappyHub.Models;

namespace SlappyHub.Services;

static class Win32
{
	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

	public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
		IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

	[DllImport("user32.dll")]
	public static extern IntPtr SetWinEventHook(
		uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
		WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

	[DllImport("user32.dll")]
	public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}

public class SlackAppWatcher
{
	private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
	private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
	private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;

	private readonly Win32.WinEventDelegate _proc; // GC防止
	private IntPtr _hook;

	private bool _started = false;
	private string _workspaceName = "";
	private string _channel = "";
	private string _sender = "";

	public event EventHandler<SlackViewChangeEvent>? OnChangeView;

	public SlackAppWatcher()
	{
		_proc = WinEventProc;
		_hook = Win32.SetWinEventHook(
			EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_NAMECHANGE,
			IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
		if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
	}

	public void Start()
	{
		if (_started) return;
		var hWnd = GetForegroundSlackWindow();
		if (hWnd != IntPtr.Zero)
		{
			var info = GetSlackViewInfo(hWnd);
			if (info != null)
			{
				_workspaceName = info.WorkspaceName;
				_channel = info.Channel;
				_sender = info.Sender;
			}
		}

		_started = true;
	}

	private void WinEventProc(IntPtr hWinEventHook, uint eventType,
		IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
	{
		if (!_started) return;
		if (eventType != EVENT_SYSTEM_FOREGROUND && eventType != EVENT_OBJECT_NAMECHANGE) return;
		if (hWnd == IntPtr.Zero) return;
		if (eventType == EVENT_OBJECT_NAMECHANGE)
		{
			hWnd = GetForegroundSlackWindow();
			if (hWnd == IntPtr.Zero) return;
			
		}
		var processName = GetProcessNameFromHWnd(hWnd);
		if (processName != null && string.Equals(processName, "slack", StringComparison.OrdinalIgnoreCase))
		{
			var info = GetSlackViewInfo(hWnd);
			if (info != null)
			{
				if (_workspaceName != info.WorkspaceName || _channel != info.Channel || _sender != info.Sender)
				{
					_workspaceName = info.WorkspaceName ?? "";
					_channel = info.Channel ?? "";
					_sender = info.Sender ?? "";
					
					_workspaceName = info.WorkspaceName;
					_channel = info.Channel;
					_sender = info.Sender;
					OnChangeView?.Invoke(this,
						new SlackViewChangeEvent(true, info.WorkspaceName, info.Channel, info.Sender));
				}
			}
		}
	}

	private string GetWindowTitle(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero) return "";
		var sb = new StringBuilder(512);
		Win32.GetWindowText(hWnd, sb, sb.Capacity);
		return sb.ToString();
	}

	private IntPtr GetForegroundSlackWindow()
	{
		var hwnd = Win32.GetForegroundWindow();
		if (hwnd == IntPtr.Zero) return IntPtr.Zero;

		var process = GetProcessNameFromHWnd(hwnd);
		if (process == null || !string.Equals(process, "slack", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
		return hwnd;
	}


	private string? GetProcessNameFromHWnd(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero) return null;

		Win32.GetWindowThreadProcessId(hWnd, out uint pid);
		if (pid == 0) return null;
		try
		{
			using var p = Process.GetProcessById((int)pid);
			return p.ProcessName;
		}
		catch
		{
			return null;
		}
	}

	private SlackViewInfo? GetSlackViewInfo(IntPtr hWnd)
	{
		var workspaceName = "";
		var channelName = "";
		var sender = "";

		var title = GetWindowTitle(hWnd);

		// 例: "general - MyWorkspace - Slack"
		if (!title.EndsWith(" - Slack", StringComparison.OrdinalIgnoreCase))
			return null;

		var parts = title.Split(" - ");
		if (parts.Length < 3)
			return null;

		channelName = parts[0].Trim();
		workspaceName = parts[1].Trim();
		if (channelName.EndsWith("（DM）"))
		{
			sender = channelName.Substring(0, channelName.Length - 4).Trim();
			if (sender.StartsWith("!"))
			{
				sender = sender.Substring(1).Trim();
			}

			channelName = "[DM]";
		}
		else if (channelName.EndsWith("（チャンネル）"))
		{
			channelName = channelName.Substring(0, channelName.Length - 7).Trim();
			if (channelName.StartsWith("*"))
			{
				channelName = channelName.Substring(1).Trim();
			}
		}

		return new SlackViewInfo(workspaceName, channelName, sender);
	}

	public SlackViewInfo? GetSlackViewInfo()
	{
		var hWnd = GetForegroundSlackWindow();
		if (hWnd == IntPtr.Zero)
			return null;
		return GetSlackViewInfo(hWnd);
	}
}