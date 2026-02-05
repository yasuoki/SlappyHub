namespace SlappyHub;

public class UsbDeviceInfo
{
    public string Port;
    public string Model;
    public string Version;
    public string Description => $"{Port}: {Model}/{Version}";
    
    public UsbDeviceInfo(string port, string model, string version)
    {
        Port = port;
        Model = model;
        Version = version;
    }
}