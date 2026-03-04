using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using SlappyHub.Models;

namespace SlappyHub;

public class SlappyDevice
{
    public PortDriver? Driver => _driver;
    
    public string? Maker => _maker;
    public string? Model => _model;
    public string? Version => _version;
    public string? Description => _description;
    public bool IsConnected => _driver != null && _driver.Port.IsConnected;

    private PortDriver? _driver;
    private string? _maker;
    private string? _model;
    private string? _version;
    private string? _description;
    private readonly object _rxLock = new();
    private readonly object _lineWaitLock = new();
    private readonly ConcurrentQueue<TaskCompletionSource<ReceiveMessage>> _pendingResponses = new();
    private readonly List<(Func<string, bool> pred, TaskCompletionSource<string> tcs)> _lineWaiters = new();
    
    public event EventHandler<int>? OnWiFiStatusChanged;
    private UInt16 crc16(string buff)
    {
        UInt16 result = 0;
        foreach (var c in buff)
        {
            result ^= c;
            for (var j = 0; j < 8; ++j)
            {
                if ((result & 0x01) != 0)
                    result = (ushort)((result >> 1) ^ 0xA001);
                else
                    result >>= 1;
            }
        }

        return result;
    }
    private void FeedLine(string line)
    {
        List<TaskCompletionSource<string>>? hit = null;

        lock (_lineWaitLock)
        {
            for (int i = _lineWaiters.Count - 1; i >= 0; i--)
            {
                var (pred, tcs) = _lineWaiters[i];
                if (pred(line))
                {
                    _lineWaiters.RemoveAt(i);
                    hit ??= new List<TaskCompletionSource<string>>();
                    hit.Add(tcs);
                }
            }
        }

        if (hit != null)
        {
            foreach (var tcs in hit)
                tcs.TrySetResult(line);
        }
    }

    public SlappyDevice()
    {
    }

    private async Task DetectDevice(PortDriver driver)
    {
        Debug.WriteLine($"DetectDevice: {driver.Port.Address}");
        string? maker = null;
        string? model = null;
        string? version = null;
        try
        {
            await driver.ConnectAsync();
            await Task.Delay(500);
            await driver.SendRawAsync("\n");
            var ret = await driver.WaitForLineAsync(
                line => Regex.IsMatch(line, "Yonabe Factory */ *SlappyBell"),
                TimeSpan.FromSeconds(3));
            var segs = ret.Split('/');
            if (segs.Length == 3)
            {
                var _maker = segs[0].Trim();
                var _model = segs[1].Trim();
                var _ver = segs[2].Trim();
                if (_maker.Contains("Yonabe Factory"))
                {
                    if (_model.Contains("SlappyBell"))
                    {
                        maker = _maker;
                        model = _model;
                        version = _ver;
                    }
                }
            }
            if (string.IsNullOrEmpty(maker) || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(version))
            {
                throw new Exception("未知のデバイスです");
            }
            
            var verSeg = version.Split('.');
            if (verSeg.Length != 3)
            {
                throw new Exception($"version {version} は非対応のフォーマットです");
            }
            int seg0 = int.Parse(verSeg[0].Trim()); 
            int seg1 = int.Parse(verSeg[1].Trim());
            if (seg0 != 1 || seg1 != 2)
            {
                throw new Exception($"version {version} は非対応のバージョンです");
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"DetectDevice exception {e.Message}");
            driver.Disconnect();
            throw new Exception($"{driver.Port.PortType}:{driver.Port.Address}へ接続できません\r\n{e.Message}");
        }
        _maker = maker;
        _model = model;
        _version = version;
        _description = $"{driver.Port.PortType}: {model}/{version}";
        _driver = driver;
    }

    public async Task Attach(PortDriver driver)
    {
        if (_driver != driver)
        {
            await Detouch();
        }
        await DetectDevice(driver);
        _driver!.OnNotify += onNotify;
    }

    public async Task Detouch()
    {
        if (_driver != null)
        {
            _driver.OnNotify -= onNotify;
            if (_driver.Port.IsConnected)
            {
                await Bye();
                _driver.Disconnect();
            }
            _driver = null;
        }
    }

    private void onNotify(object? sebder, ReceiveMessage message)
    {
        Debug.WriteLine($"Notify: {message.Code} {message.Message}");
        switch (message.Code)
        {
            case ReceiveMessage.ResultCode.WiFiConnected:
            case ReceiveMessage.ResultCode.WiFiSsidNotFound:
            case ReceiveMessage.ResultCode.WiFiAuthFail:
            case ReceiveMessage.ResultCode.WiFiDisconnected:
                OnWiFiStatusChanged?.Invoke(this, (int)message.Code);
                break;
        }
    }
    
    private async Task<ReceiveMessage> SendCommandAsync(string cmd, TimeSpan? timeout = null)
    {
        ReceiveMessage ret;
        try
        {
            if (_driver == null)
            {
                ret = new ReceiveMessage(ReceiveMessage.ResultCode.Error, "Device not attached");
                Debug.WriteLine($"{DateTime.Now} SlappyDevice: device not attached");
            }
            else
            {
                ret = await _driver.SendReceiveAsync(cmd + "\n", timeout);
                if (ret.Code != ReceiveMessage.ResultCode.Success)
                    Debug.WriteLine(
                        $"{DateTime.Now} SlappyDevice: receive response {ret.Type.ToString()} {ret.Code} <{ret.Message}>\n<{ret.Body}>");
            }
        }
        catch (Exception e)
        {
            ret = new ReceiveMessage(ReceiveMessage.ResultCode.Error, e.Message);
            Debug.WriteLine($"{DateTime.Now} SlappyDevice: receive response {ret.Type.ToString()} {ret.Code} <{ret.Message}>\n<{ret.Body}>");
        }
        return ret;
    }
    public async Task<ReceiveMessage> Bye()
    {
        return await SendCommandAsync("bye");
    }
    public async Task<ReceiveMessage> WiFiStatus()
    {
        return await SendCommandAsync("wifi");
    }
    public async Task<ReceiveMessage> ConnectWiFi(string ssid, string passwd)
    {
        var cmd = $"wifi \"{ssid}\" \"{passwd}\"";
        return await SendCommandAsync(cmd);
    }
    public async Task<ReceiveMessage> DisconnectWiFi()
    {
        var cmd = $"wifi off";
        return await SendCommandAsync(cmd,TimeSpan.FromSeconds(10));
    }
    
    public async Task<ReceiveMessage> LedOn(int slot, string colorPattern)
    {
        var cmd = $"led-on {slot} {colorPattern}";
        return await SendCommandAsync(cmd);
    }
    
    public async Task<ReceiveMessage> LedOff(int slot)
    {
        var cmd = $"led-off {slot}";
        return await SendCommandAsync(cmd);
    }
    
    public async Task<ReceiveMessage> Play(string mp3)
    {
        var cmd = $"play {mp3}";
        return await SendCommandAsync(cmd);
    }
    
    public async Task<ReceiveMessage> Stop()
    {
        return await SendCommandAsync("stop");
    }
    
    public async Task<ReceiveMessage> SetVolume(int volume)
    {
        var cmd = $"volume {volume}";
        return await SendCommandAsync(cmd);
    }

    public async Task<ReceiveMessage> Upload(string name, byte[] data, Progress<TransferProgress>? progress = null)
    {
        var cmd = $"upload \"{name}\" {data.Length}";
        Debug.WriteLine($"send {cmd}");
        try
        {
            ReceiveMessage ret;
            if (_driver == null)
            {
                ret = new ReceiveMessage(ReceiveMessage.ResultCode.Error, "Device not attached");
                Debug.WriteLine("SlappyDevice: device not attached");
            }
            else
            {
                ret = await _driver.SendReceiveAsync(cmd + "\n", data, progress);
                Debug.WriteLine($"receive response '{ret.Type.ToString()} {ret.Code} {ret.Message}'");
            }
            return ret;
        }
        catch (Exception e)
        {
            var r =  new ReceiveMessage(ReceiveMessage.ResultCode.Error, e.Message);            
            Debug.WriteLine($"receive response '{r.Type.ToString()} {r.Code} {r.Message}'");
            return r;
        }
    }
    
    public async Task<ReceiveMessage> Remove(string mp3)
    {
        var cmd = $"remove {mp3}";
        return await SendCommandAsync(cmd);
    }
    
    public async Task<ReceiveMessage> List(TimeSpan? timeout = null)
    {
        var cmd = $"list";
        return await SendCommandAsync(cmd, timeout);
    }
}