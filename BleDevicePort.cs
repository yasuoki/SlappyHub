using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;
using SlappyHub.Models;
using Buffer = Windows.Storage.Streams.Buffer;

namespace SlappyHub;

public class BleDevicePortException(string message) : Exception(message);

public class BleDevicePort : IDevicePort
{
    private ulong _bluetoothAddress;
    private BluetoothLEDevice? _dev;
    private GattCharacteristic? _ctrlRx;
    private GattCharacteristic? _ctrlTx;
    private static readonly Guid  SERVICE_UUID = Guid.Parse("032b4ecc-f367-4e08-bfe5-0f42aa4e62c0");
    private static readonly Guid  CTRL_RX_UUID = Guid.Parse("032b4ecc-f367-4e08-bfe5-0f42aa4e62c1");
    private static readonly Guid  CTRL_TX_UUID = Guid.Parse("032b4ecc-f367-4e08-bfe5-0f42aa4e62c2");
    public string PortType => "BLE";
    public string Address => _bluetoothAddress.ToString("X");
    public bool IsConnected => _dev != null;

    public event TypedEventHandler<IDevicePort, string>? DataReceived;
    public event TypedEventHandler<IDevicePort, object>? Disconected;
    public BleDevicePort(DeviceInfo device)
    {
        _bluetoothAddress = ulong.Parse(device.Address, System.Globalization.NumberStyles.HexNumber);;
    }

    public async Task ConnectAsync()
    {
        _dev = await BluetoothLEDevice.FromBluetoothAddressAsync(_bluetoothAddress) ?? throw new Exception("FromIdAsync failed");
        if(_dev == null) throw new Exception("FromIdAsync failed");
        
        var servicesRet = await _dev.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        var svc = servicesRet.Services.FirstOrDefault(s => s.Uuid == SERVICE_UUID);
        if (svc != null)
        {
            var chResult = await svc.GetCharacteristicsAsync();
            var chars = chResult.Characteristics;

            _ctrlRx = chars.First(c => c.Uuid == CTRL_RX_UUID);
            _ctrlTx = chars.First(c => c.Uuid == CTRL_TX_UUID);

            _dev.ConnectionStatusChanged += OnDeviceConectionStatusChanged;
            _ctrlTx.ValueChanged += OnCtrlTxOnValueChanged;

            await _ctrlTx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Indicate);
            await Task.Delay(500);
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_ctrlTx != null)
            {
                _ctrlTx.ValueChanged -= OnCtrlTxOnValueChanged;
                _ctrlTx.Service.Dispose();
            }

            if (_dev != null)
                _dev.Dispose();
        }
        catch
        {
            // ignored
        }
        _ctrlRx = null;
        _ctrlTx = null;
        _dev = null;
    }

    public void Dispose()
    {
        Disconnect();
    }

    public async Task SendToDeviceAsync(string message)
    {
        if (_ctrlRx == null)
        {
            throw new BleDevicePortException("Device port not open");
        }
        var buffer = Encoding.UTF8.GetBytes(message).AsBuffer();
        try
        {
            var r = await _ctrlRx.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse).AsTask();
            Debug.WriteLine($"WriteValueAsync status: {r}");
            if (r != GattCommunicationStatus.Success)
            {
                Disconected?.Invoke(this, EventArgs.Empty);
                throw new PortDriverClosedException();
            }
        }
        catch (Exception e) {
            Debug.WriteLine($"WriteValueAsync error: {e}");
            Debug.Write(e.StackTrace);
        }
    }

    public async Task SendBytesToDeviceAsync(byte[] data, int offset, int count)
    {
        if (_ctrlRx == null)
        {
            throw new BleDevicePortException("Device port not open");
        }
        var buffer = data
            .AsSpan(offset, count)
            .ToArray()
            .AsBuffer();
        var r = await _ctrlRx.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse).AsTask();
        Debug.WriteLine($"WriteValueAsync status: {r}");
        if (r != GattCommunicationStatus.Success)
        {
            Disconected?.Invoke(this, EventArgs.Empty);
            throw new PortDriverClosedException();
        }
    }

    public void FlushDevice()
    {
        //throw new NotImplementedException();
    }
    
    private void OnDeviceConectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        Debug.WriteLine($"Device {sender.Name} connection status changed to {sender.ConnectionStatus}");
        if (sender.ConnectionStatus != BluetoothConnectionStatus.Connected)
        {
            if (_dev != null)
            {
                Disconnect();
                Disconected?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    private void OnCtrlTxOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        byte[] bytes = new byte[args.CharacteristicValue.Length];
        DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(bytes);
        var s = Encoding.UTF8.GetString(bytes);
        DataReceived?.Invoke(this, s);
    }
}