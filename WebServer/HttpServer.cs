using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SlappyHub.WebServer;

public class HttpServer
{
    private HttpListener _listener;
    private bool _isRunning;
    private HttpServerOption _option;

    public HttpServer(HttpServerOption option)
    {
        _listener = new HttpListener();
        _option = option;
    }

    public void Run()
    {
        int port = _option.Port;
        //OpenFirewallPort(port);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _isRunning = true;
        Task.Run(() => HandleRequests());
    }

    private void OpenFirewallPort(int port)
    {
        if (IsPortOpen(port))
            return;
        AddFirewallRule(port);
    }
    private bool IsPortOpen(int port)
    {
        string command = $"netsh advfirewall firewall show rule name=all | findstr \"{port}\"";
        bool isOpen = false;

        Process process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = "/C " + command;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (output.Contains(port.ToString()))
        {
            isOpen = true;
        }
        return isOpen;
    }
    private void AddFirewallRule(int port)
    {
        var sb = new StringBuilder();
        sb.Append($"netsh advfirewall firewall add rule name=SappyHubConnect_Port{port} dir=in action=allow protocol=TCP localport={port}");
        Process process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = "/C " + sb.ToString();
        process.StartInfo.Verb = "RunAs";
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = true;
        process.Start();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Failed to add firewall rule for port {port}. code = {process.ExitCode}.");
        }
    }
    
    private async void ListenTask()
    {
        IAsyncResult result = _listener.BeginGetContext(new AsyncCallback(ListenerCallback),_listener);
        Console.WriteLine("Waiting for request to be processed asyncronously.");
        result.AsyncWaitHandle.WaitOne();
        Console.WriteLine("Request processed asyncronously.");
        _listener.Close();  
    }

    private async void ListenerCallback(IAsyncResult result)
    {
        HttpListener? listener = (HttpListener) result.AsyncState;
        if (listener != null)
        {
            HttpListenerContext context = listener.EndGetContext(result);
            var path = context.Request.Url.PathAndQuery;
            if (path.StartsWith("/ws"))
            {
                if (context.Request.IsWebSocketRequest)
                {
                    await HandleWebSocketRequest(context);
                    return;
                }
            }
            await HandleHttpRequest(context);
        }
    }

    private async void HandleRequests()
    {
        while (_isRunning)
        {
            var context = await _listener.GetContextAsync();
            var path = context.Request.Url.PathAndQuery;
            if (path.StartsWith("/ws"))
            {
                if (context.Request.IsWebSocketRequest)
                {
                    await HandleWebSocketRequest(context);
                    return;
                }
            }
            await HandleHttpRequest(context);
        }
    }
    
    private async Task HandleHttpRequest(HttpListenerContext context)
    {
        var reqPath = context.Request.Url.PathAndQuery;
        var seg = reqPath.Split("?");
        var path = seg[0];
        if(string.IsNullOrEmpty(path) || path == "/")
            path = "/index.html";
        else if (!path.StartsWith("/"))
            path = "/" + path;

        var isMobileSafari = false;
        var iOSVersion = "";
        
        var ua = context.Request.UserAgent;
        if (ua != null)
        {
            // AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.7
            if (ua.Contains("AppleWebKit"))
            {
                isMobileSafari = true;
                var mv = Regex.Match(ua, " Version/[0-9.]+");
                if (mv.Success)
                {
                    iOSVersion = mv.Value.Substring(9);
                }
            }
        }

        var addr = context.Request.RemoteEndPoint.Address;
        var addrText = addr.ToString();

        byte[] body = null;
        if (path == "/index.html")
        {
            var html = 
                "<!-- DOCTYPE : DOCTYPE declaration -->\n" +
                "<!DOCTYPE html>\n"+
                "<html>\n" +
                "<head>\n" +
                "<title>Slappy Master Server</title>\n" +
                "<meta charset=\"UTF-8\">\n" +
                "</head>\n"+
                "<body>\n"+
                "</body>\n"+
                "</html>";
            body = Encoding.UTF8.GetBytes(html);
        }
        else if (path == "/result.json")
        {
            body = Encoding.UTF8.GetBytes(_option.JsonText);            
        }
        else
        {
            var localPath = Path.GetFullPath(Path.Combine(_option.StaticFilePath, path.Substring(1)));
            if (File.Exists(localPath))
            {
                /*
                if (path == "test.html")
                {
                    var cookie = new Cookie("RemoteConsole", "0123456789")
                    {
                        Domain = "192.168.56.201",
                        Expires = new DateTime(2024, 12, 31)
                    };
                    response.SetCookie(cookie);
                    Console.WriteLine("Set Cookie.");
                }

                */
                body = File.ReadAllBytes(localPath);
            }
        }

        var response = context.Response;
        if (body != null)
        {
            var type = "application/octet-stream";
            _option.ContentTypes.TryGetValue(Path.GetExtension(path), out type);
            response.ContentType = type;
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, 0, body.Length);
            response.OutputStream.Close();
        }
        else
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        response.Close();
    }

    private async Task HandleWebSocketRequest(HttpListenerContext context)
    {
        WebSocketContext webSocketContext = null;
        try
        {
            webSocketContext = await context.AcceptWebSocketAsync(null);
            WebSocket webSocket = webSocketContext.WebSocket;

            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var echoMessage = Encoding.UTF8.GetBytes("Echo: " + message);
                    await webSocket.SendAsync(new ArraySegment<byte>(echoMessage), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("WebSocket Error: " + ex.Message);
        }
        finally
        {
            if (webSocketContext?.WebSocket != null)
            {
                webSocketContext.WebSocket.Dispose();
            }
        }
    }
}