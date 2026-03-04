using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Windows.Foundation;
using SlappyHub.Models;

namespace SlappyHub;

public class UsbDevicePortException(string message) : Exception(message);

public class UsbDevicePort : IDevicePort
{
	private SerialPort? _port;
	private string _portName;
	public string PortType => _portName;
	public string Address => _portName;
	public bool IsConnected => _port != null && _port.IsOpen;
	
	public event TypedEventHandler<IDevicePort, string>? DataReceived;
	public event TypedEventHandler<IDevicePort, object>? Disconected;

	public UsbDevicePort(DeviceInfo device)
	{
		_portName = device.Address;
	}

	public void Dispose()
	{
		Disconnect();
		if (_port != null)
		{
			_port.Dispose();
			_port = null;
		}
	}

	public async Task ConnectAsync()
	{
		var port = _port;
		if (port == null)
			port = new SerialPort();
		try
		{
			await Task.Delay(600);
			port.BaudRate = 115200;
			port.NewLine = "\n";
			port.Parity = Parity.None;
			port.RtsEnable = false;
			port.DtrEnable = false;
			port.DataBits = 8;
			port.StopBits = StopBits.One;
			port.Handshake = Handshake.None;
			port.PortName = _portName;
			port.Open();
			port.RtsEnable = true;
			port.DtrEnable = true;
			port.DiscardInBuffer();
			port.DiscardOutBuffer();
			await Task.Delay(800);
		}
		catch (Exception )
		{
			port.Close();
			throw;
		}
		_port = port;
		_port.DataReceived += OnDataArrived;
	}

	public void Disconnect()
	{
		if (_port != null)
		{
			_port.DataReceived -= OnDataArrived;
			_port.Close();
		}
	}

	public Task SendToDeviceAsync(string message)
	{
		if (_port == null)
		{
			throw new UsbDevicePortException("Device port not open");
		}

		try
		{
			_port.Write(message);
		}
		catch (InvalidOperationException)
		{
			Disconected?.Invoke(this, EventArgs.Empty);
			throw new PortDriverClosedException();
		}

		return Task.CompletedTask;
	}

	public Task SendBytesToDeviceAsync(byte[] data, int offset, int count)
	{
		if (_port == null)
		{
			throw new UsbDevicePortException("Device port not open");
		}

		try
		{
			_port.Write(data, offset, count);
		}
		catch (InvalidOperationException)
		{
			Disconected?.Invoke(this, EventArgs.Empty);
			throw new PortDriverClosedException();
		}

		return Task.CompletedTask;
	}
	
	public void FlushDevice()
	{
		_port?.BaseStream.Flush();
	}

	private readonly object _rxLock = new();
	private void OnDataArrived(object sender, SerialDataReceivedEventArgs e)
	{
		try
		{
			SerialPort sp = (SerialPort)sender;
			string data = sp.ReadExisting();
			if (string.IsNullOrEmpty(data))
			{
				return;
			}
			DataReceived?.Invoke(this, data);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"UsbDevicePort: DataArrived error: {ex}");
		}
	}
}