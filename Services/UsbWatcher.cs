using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SlappyHub.Models;

namespace SlappyHub.Services;

using System;
using System.Runtime.InteropServices;

internal static class SetupApi
{
    // =========================================================
    // DEVPROPKEY
    // =========================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // DEVPKEY_Device_BusReportedDeviceDesc
    public static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc =
        new DEVPROPKEY
        {
            fmtid = new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"),
            pid = 4
        };
    public static readonly DEVPROPKEY DEVPKEY_Device_Parent =
        new DEVPROPKEY
        {
            fmtid = new Guid("4340A6C5-93FA-4706-972C-7B648008A5A7"),
            pid = 8
        };
    // =========================================================
    // SP_DEVINFO_DATA
    // =========================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public int DevInst;
        public IntPtr Reserved;
    }

    // =========================================================
    // SetupAPI functions
    // =========================================================

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(
        IntPtr ClassGuid,
        IntPtr hwndParent);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiOpenDeviceInfo(
        IntPtr DeviceInfoSet,
        string DeviceInstanceId,
        IntPtr hwndParent,
        int OpenFlags,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll",
        EntryPoint = "SetupDiGetDevicePropertyW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        in DEVPROPKEY PropertyKey,
        out uint PropertyType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize,
        uint Flags);
    
    private static string? _getDeviceProperty(IntPtr hDevInfo, string pnpId, DEVPROPKEY key)
    {
        var devInfo = new SP_DEVINFO_DATA
        {
            cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>()
        };

        if (!SetupDiOpenDeviceInfo(
                hDevInfo,
                pnpId,
                IntPtr.Zero,
                0,
                ref devInfo))
        {
            return null;
        }

        byte[] buffer = new byte[256];
        if (!SetupDiGetDeviceProperty(
                hDevInfo,
                ref devInfo,
                key,
                out _,
                buffer,
                (uint)buffer.Length,
                out uint bufferSize,
                0))
        {
            return null;
        }
        string propertyString = Encoding.Unicode
            .GetString(buffer, 0, (int)bufferSize - 2);
        return propertyString;
    }
    
    public static string? GetUsbProductFromPnpDeviceId(string pnpId)
    {
        IntPtr hDevInfo = SetupDiCreateDeviceInfoList(
            IntPtr.Zero, IntPtr.Zero);

        if (hDevInfo == IntPtr.Zero)
            return null;

        try
        {
            var pValue = _getDeviceProperty(hDevInfo, pnpId, DEVPKEY_Device_Parent);
            if(pValue == null)
                return null;
            var pName = _getDeviceProperty(hDevInfo, pValue, DEVPKEY_Device_BusReportedDeviceDesc);
            return pName;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }
    }
}

public class UsbWatcher: IDisposable
{
    private readonly List<DeviceInfo> _usbDevices = new ();
    private ManagementEventWatcher? _instanceCreationEventWatcher;
    private ManagementEventWatcher? _instanceDeletionEventWatcher;

    public event EventHandler<DeviceInfo>? Added;
    public event EventHandler<DeviceInfo>? Removed;

    public List<DeviceInfo> USBDevices => _usbDevices;
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
            d.EventArrived -= OnUsbAdded;
            c.EventArrived -= OnUsbRemoved;
            c.Stop();
            c.Dispose();
            d.Stop();
            d.Dispose();
        }
    }
    private DeviceInfo? DetectSlappyBellDevice(string devName, string pnpId)
    {
        var match = Regex.Match(devName, "COM[0-9]+");
        if (match.Success)
        {
            string portName = match.Value;
            var product = SetupApi.GetUsbProductFromPnpDeviceId(pnpId);
            Debug.WriteLine($"DetectSlappyBellDevice {devName} → {product}");
            if (product != null && product.Contains("SlappyBell"))
            {
                return new DeviceInfo("USB", portName,$"{portName}: {product}");
            }
        }
        return null;
    }

    private void ListDevice()
    {
        using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
        using(var collection = searcher.Get())
        {
            foreach (var device in collection)
            {
                string devName = (string)device.GetPropertyValue("Name");

                if (!string.IsNullOrEmpty(devName) && devName.Contains("USB") && devName.Contains("COM"))
                {
                    string? pnpId = device.GetPropertyValue("PNPDeviceID")?.ToString();
                    if (pnpId != null)
                    {
                        var devPort = DetectSlappyBellDevice(devName, pnpId);
                        if (devPort != null)
                        {
                            _usbDevices.Add(devPort);
                            Added?.Invoke(device, devPort);
                        }
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
            string? pnpId = device.GetPropertyValue("PNPDeviceID")?.ToString();
            if (pnpId != null)
            {
                var devPort = DetectSlappyBellDevice(devName,pnpId);
                if (devPort != null)
                {
                    _usbDevices.Add(devPort);
                    Added?.Invoke(device, devPort);
                }
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
                var pos = _usbDevices.FindIndex((n) => n.Address == port);
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

