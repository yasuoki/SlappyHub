using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SlackNet;
using SlackNet.Events;
using SlappyHub.Models;

namespace SlappyHub.Services.Slack;

static class Win32
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}


public class SlackConnector : IEventHandler<MessageEvent>
{
    private ISlackApiClient? _slack;
    private ISlackSocketModeClient? _socketModeClient;
    public event EventHandler<bool>? OnConnectionChanged;

    public event EventHandler<NotificationEvent>? OnMessage;
    
    public bool IsStarted => _socketModeClient != null;

    public SlackConnector() 
    {
    }

    public async Task Start(string appToken, string botToken)
    {
        if (IsStarted)
            return;
        var client = new SlackServiceBuilder()
            .UseApiToken(botToken)
            .UseAppLevelToken(appToken)
            .RegisterEventHandler<MessageEvent>(ctx =>
            {
                _slack = ctx.ServiceProvider.GetApiClient();
                return this;
            })
            .GetSocketModeClient();
        await client.Connect();
        _socketModeClient = client;
        OnConnectionChanged?.Invoke(this, true);
    }

    public void Stop()
    {
         DisconnectService();
    }

    public void DisconnectService()
    {
        if (_socketModeClient != null)
        {
            _socketModeClient.Disconnect();
            _socketModeClient.Dispose();
            _socketModeClient = null;
            _slack = null;
            OnConnectionChanged?.Invoke(this, false);
        }
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if(_slack == null) return;
        var userName = "";
        try
        {
            var userInfo = await _slack.Users.Info(slackEvent.User);
            userName = userInfo.RealName;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        var channelName = "";
        try
        {
            var info = await _slack.Conversations.Info(slackEvent.Channel);
            channelName = info.Name;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        OnMessage?.Invoke(this, new NotificationEvent("socket", channelName, userName, slackEvent.Text));
    }
    
    
    public string GetForegroundWindowTitle()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "";

        var sb = new StringBuilder(512);
        Win32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
    
    public bool TryGetSlackChannelFromTitle(
        out string channelName,
        out string workspaceName)
    {
        channelName = "";
        workspaceName = "";

        var title = GetForegroundWindowTitle();
        // 例: "general - MyWorkspace - Slack"
        if (!title.EndsWith(" - Slack", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = title.Split(" - ");
        if (parts.Length < 3)
            return false;

        channelName = parts[0].Trim();
        workspaceName = parts[1].Trim();
        return true;
    }
}
