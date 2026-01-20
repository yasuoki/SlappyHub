using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SlappyHub;

internal class Location
{
    public string FilePath { get; }
    public int Line { get; }
}

internal class SlappyDeviceException : Exception
{
	private Location? _location;
    internal SlappyDeviceException(string message, Location? location=null) : base(message)
    {
		_location = location;
    }
    public string? FilePath => _location?.FilePath;
    public int? Line => _location?.Line;
}

public class SlappyDevice
{
    internal UsbDeviceInfo Info { init; get; }
    internal UsbDevicePort Port { init; get; }
    
    public event EventHandler<int>? OnWifiStatusChanged;


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

    protected SlappyDevice(UsbDeviceInfo portInfo)
    {
        Info = portInfo;
        Port = new UsbDevicePort(portInfo.Port);
        Port.OnNotify += (sender, args) =>
        {
            Debug.WriteLine($"Notify: {args.Code} {args.Message}");
            switch (args.Code)
            {
                case ReceiveMessage.ResultCode.WifiConnected:
                case ReceiveMessage.ResultCode.WifiSsidNotFound:
                case ReceiveMessage.ResultCode.WifiAuthFail:
                case ReceiveMessage.ResultCode.WifiDisconnected:
                    OnWifiStatusChanged?.Invoke(this, (int)args.Code);
                    break;
            }
        };
    }
    
    internal static async Task<UsbDeviceInfo?> DetectDevice(UsbDevicePort port)
    {
        var model = "";
        var version = "";
        try
        {
            await port.ConnectAsync();
            await port.SendRawAsync("\n");
            var ret = await port.WaitForLineAsync(
                line =>Regex.IsMatch(line, "Yonabe Factory */ *SlappyBell"),
                TimeSpan.FromSeconds(3));
            var segs = ret.Split('/');
            if (segs.Length == 3)
            {
                var _maker = segs[0].Trim();
                var _model = segs[1].Trim();
                var _ver = segs[2].Trim();
                if (_maker == "Yonabe Factory")
                {
                    if (_model == "SlappyBell")
                    {
                        model = _model;
                        version = _ver;
                    }
                }
            }

            if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(version))
            {
                return null;
            }
        }
        catch (Exception e)
        {
            port.Disconnect();
            Debug.WriteLine(e.Message);
            return null;
        }
        finally
        {
            port.Disconnect();
        }
        return new UsbDeviceInfo()
        {
            Port = port.PortAddress,
            Model = model,
            Version = version
        };
    }

    internal static async Task<SlappyDevice>  Connect(UsbDeviceInfo portInfo)
    {
        var device = new SlappyDevice(portInfo);
        
        await device.Port.ConnectAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        return device;
    }

    internal void Disconnect()
    {
        Port.Disconnect();
    }

    private async Task<ReceiveMessage> SendCommandAsync(string cmd, TimeSpan? timeout = null)
    {
        ReceiveMessage ret;
        try
        {
            ret = await Port.SendReceiveAsync(cmd + "\n", timeout);
            if(ret.Code != ReceiveMessage.ResultCode.Success) 
                Debug.WriteLine($"SlappyDevice: receive response {ret.Type.ToString()} {ret.Code} <{ret.Message}>\n<{ret.Body}>");
        }
        catch (Exception e)
        {
            ret = new ReceiveMessage(ReceiveMessage.ResultCode.Error, e.Message);
            Debug.WriteLine($"SlappyDevice: receive response {ret.Type.ToString()} {ret.Code} <{ret.Message}>\n<{ret.Body}>");
        }
        return ret;
    }
    public async Task<ReceiveMessage> WiFiStatus()
    {
        return await SendCommandAsync("wifi");
    }
    public async Task<ReceiveMessage> ConnectWifi(string ssid, string passwd)
    {
        var cmd = $"wifi \"{ssid}\" \"{passwd}\"";
        return await SendCommandAsync(cmd);
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
            var r = await Port.SendReceiveAsync(cmd + "\n", data, progress, TimeSpan.FromMilliseconds(100));
            Debug.WriteLine($"receive response '{r.Type.ToString()} {r.Code} {r.Message}'");
            return r;
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