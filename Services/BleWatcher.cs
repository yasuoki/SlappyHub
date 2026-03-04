using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;
using SlappyHub.Models;
using Timer = System.Threading.Timer;

namespace SlappyHub.Services;


public class BleWatcher
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);
    private object _lock = new ();
    private bool _isRunning = false;
    private readonly List<DeviceInfo> _bleDevices = new ();
    public event EventHandler<DeviceInfo>? Added;
    public event EventHandler<DeviceInfo>? Removed;

    public BleWatcher()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += OnReceived;
        _watcher.Stopped  += OnStopped;
        _cleanupTimer = new Timer(_ => Cleanup(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (_isRunning)
            return;
        _isRunning = true;
        Debug.WriteLine("Starting BLE watcher");
        _watcher.Start();
        _cleanupTimer.Change(1000, 1000);
    }

    public void Stop()
    {
        if (!_isRunning)
            return;
        _isRunning = false;
        Debug.WriteLine("Stopping BLE watcher");
        _watcher.Stop();
        _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void OnReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs e)
    {
        var name = e.Advertisement.LocalName;
        if (string.IsNullOrWhiteSpace(name) || !name.Contains("SlappyBell"))
        {
            return;
        }
        Debug.WriteLine($"Received BLE advertisement from {name} ({e.BluetoothAddress:X12})");
        
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            var address = e.BluetoothAddress.ToString("X");
            var pos = _bleDevices.FindIndex(d => d.Address == address);
            if (pos < 0)
            {
                var bleDevice = new DeviceInfo("BLE", address, $"BLE:{name}");
                _bleDevices.Add(bleDevice);
                Added?.Invoke(this, bleDevice);
            }
            else
            {
                _bleDevices[pos].LastSeen = now;
            }
        }
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;

        var newList = new List<DeviceInfo>();
        lock (_lock)
        {
            foreach (var bleDev in _bleDevices)
            {
                if (now - bleDev.LastSeen < _timeout)
                {
                    newList.Add(bleDev);
                }
                else
                {
                    Removed?.Invoke(this, bleDev);
                }
            }
            _bleDevices.Clear();
            _bleDevices.AddRange(newList);
        }
    }

    private void OnStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs e)
    {
        Debug.WriteLine("BLE watcher stopped");
        //Stop();
    }

    public void Dispose()
    {
        Stop();
        _watcher.Received -= OnReceived;
        _watcher.Stopped  -= OnStopped;
        _cleanupTimer.Dispose();
    }
}
