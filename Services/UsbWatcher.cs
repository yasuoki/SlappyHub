using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;

namespace SlappyHub.Services;

public class UsbWatcher: IDisposable
{
    private readonly List<UsbDeviceInfo> _usbDevices = new List<UsbDeviceInfo>();
    private ManagementEventWatcher? _instanceCreationEventWatcher;
    private ManagementEventWatcher? _instanceDeletionEventWatcher;

    public event EventHandler<UsbDeviceInfo>? Added;
    public event EventHandler<UsbDeviceInfo>? Removed;

    public List<UsbDeviceInfo> USBDevices => _usbDevices;
    public UsbWatcher()
    {
    }

    public void Start()
    {
        ListDevice();
        if (_instanceCreationEventWatcher == null || _instanceDeletionEventWatcher == null)
        {
            _instanceCreationEventWatcher = new ManagementEventWatcher();
            _instanceDeletionEventWatcher = new ManagementEventWatcher();
            _instanceCreationEventWatcher.Query =
                new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"); // high CPU load
            _instanceDeletionEventWatcher.Query =
                new WqlEventQuery(
                    "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"); // high CPU load
            _instanceCreationEventWatcher.EventArrived += OnUsbAdded;
            _instanceDeletionEventWatcher.EventArrived += OnUsbRemoved;
            _instanceCreationEventWatcher.Start();
            _instanceDeletionEventWatcher.Start();
        }
    }

    public void Stop()
    {
        var d = _instanceDeletionEventWatcher;
        var c = _instanceCreationEventWatcher;
        if (d != null && c != null)
        {
            _instanceDeletionEventWatcher = null;
            _instanceCreationEventWatcher = null;
            c.Stop();
            c.Dispose();
            d.Stop();
            d.Dispose();
        }
    }
    private async Task<UsbDeviceInfo?> DetectSlappyBellDevice(string devName)
    {
        var match = Regex.Match(devName, "COM[0-9]+");
        if (match.Success)
        {
            string portName = match.Value;
            using var port = new UsbDevicePort(portName);
            return await SlappyDevice.DetectDevice(port);
        }
        return null;
    }

    private async void ListDevice()
    {
        using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
        using(var collection = searcher.Get())
        {
            foreach (var device in collection)
            {
                string devName = (string)device.GetPropertyValue("Name");
                string guid = (string)device.GetPropertyValue("ClassGuid");

                if (!string.IsNullOrEmpty(devName) && devName.Contains("USB") && devName.Contains("COM"))
                {
                    var devinfo = await DetectSlappyBellDevice(devName);
                    if (devinfo != null)
                    {
                        _usbDevices.Add(devinfo);
                        Added?.Invoke(device, devinfo);
                    }
                }
            }
        }
    }

    private async void OnUsbAdded(object sender, EventArrivedEventArgs e)
    {
        var device = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        
        string devName = (string)device.GetPropertyValue("Name");
        if (!string.IsNullOrEmpty(devName) && devName.Contains("USB") && devName.Contains("COM"))
        {
            var devinfo = await DetectSlappyBellDevice(devName);
            if (devinfo != null)
            {
                _usbDevices.Add(devinfo);
                Added?.Invoke(device, devinfo);
            }
        }
    }

    private void OnUsbRemoved(object sender, EventArrivedEventArgs e)
    {
        var device = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        
        string devName = (string)device.GetPropertyValue("Name");

        if (!string.IsNullOrEmpty(devName) && devName.Contains("USB") && devName.Contains("COM"))
        {
            var match = Regex.Match(devName, "COM[0-9]+");
            if (match.Success)
            {
                var port = match.Value;
                var pos = _usbDevices.FindIndex((n) => n.Port == port);
                if (pos >= 0)
                {
                    var devInfo = _usbDevices[pos];
                    _usbDevices.RemoveAt(pos);
                    Removed?.Invoke(device, devInfo);
                }
            }
        }
    }
    
    public void Dispose()
    {
        _instanceCreationEventWatcher?.Dispose();
        _instanceDeletionEventWatcher?.Dispose();
    }
}

