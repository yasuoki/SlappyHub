using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using SlappyHub.Models;
using Timer = System.Threading.Timer;

namespace SlappyHub.Services;


public class BleWatcher
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private Radio? _radio;
    private DeviceWatcher? _adapterWatcher;
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
        _ = InitRadioAsync();
        InitAdapterWatcher();
    }
    
    private async Task InitRadioAsync()
    {
        var radios = await Radio.GetRadiosAsync();

        _radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (_radio != null)
        {
            _radio.StateChanged += (_, _) =>
            {
                Debug.WriteLine($"Radio status changed, status={_radio.State}");
                if (_radio.State == RadioState.On)
                {
                    if (_isRunning)
                        RestartWatcher();
                }
            };
        }
    }
    private void InitAdapterWatcher()
    {
        var selector = "System.Devices.InterfaceClassGuid:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";
        _adapterWatcher = DeviceInformation.CreateWatcher(selector);
        _adapterWatcher.Added += (_, _) =>
        {
            Debug.WriteLine("Bluetooth adapter added");
            if(_isRunning)
                RestartWatcher();
        };

        _adapterWatcher.Removed += (_, _) =>
        {
            Debug.WriteLine("Bluetooth adapter removed");
            StopWatcherAndClear();
        };
        _adapterWatcher.Start();
    }
    
    public void Start()
    {
        if (_isRunning)
            return;
        _isRunning = true;
        StartWatcher();
    }

    public void Stop()
    {
        if (!_isRunning)
            return;
        _isRunning = false;
        StopWatcherAndClear();
    }

    private void StartWatcher()
    {
        try
        {
            Debug.WriteLine("Starting BLE watcher");
            _cleanupTimer.Change(1000, 1000);
            if(_watcher.Status != BluetoothLEAdvertisementWatcherStatus.Started)
                _watcher.Start();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Starting BLE watcher error, {e.Message}");
        }
    }
    private void StopWatcher()
    {
        try
        {
            Debug.WriteLine("Stopping BLE watcher");
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if(_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started) 
                _watcher.Stop();
        }
        catch(Exception e)
        {
            Debug.WriteLine($"Stopping BLE watcher error, {e.Message}");
        }
    }
    private void StopWatcherAndClear()
    {
        lock (_lock)
        {
            StopWatcher();
            if (_isRunning)
            {
                foreach (var d in _bleDevices)
                {
                    Removed?.Invoke(this, d);
                }
            }
            _bleDevices.Clear();
        }
    }
    private void RestartWatcher()
    {
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            StopWatcher();
            StartWatcher();
        }
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
        
        var now = DateTime.Now;
        lock (_lock)
        {
            var address = e.BluetoothAddress.ToString("X");
            var pos = _bleDevices.FindIndex(d => d.Address == address);
            if (pos < 0)
            {
                var bleDevice = new DeviceInfo("BLE", address, $"BLE:{name}");
                _bleDevices.Add(bleDevice);
                if(_isRunning)
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
        var now = DateTime.Now;

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
                    if(_isRunning)
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
        if (_isRunning)
        {
            RestartWatcher();
        }
    }
}
