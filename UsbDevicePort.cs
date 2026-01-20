using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace SlappyHub;

public class DevicePortException(string message) : Exception(message);

public class PortStatus
{
	public virtual string Status { get; }
	public bool IsConnecting { get; }
	public PortStatus(string status, bool isConnecting)
	{
		Status = status;
		IsConnecting = isConnecting;
	}
}

public class ReceiveMessage
{
	static readonly string RESPONSE_PREFIX = "[R@APM]";
	static readonly string NOTIFY_PREFIX = "[N@APM]";
	public enum MessageType
	{
		Response = 0,
		Notify = 1,
		Unknown = 2
	}

	public enum ResultCode
	{
		Success				= 0,
		CommandError		= 10,
		UnknownCommand		= 11,
		BadCommandFormat	= 12,
		IntegerParseError	= 13,
		StringParseError	= 14,
		SlotError			= 20,
		BadLedPattern		= 21,
		FileNotFound		= 22,
		TooLongCommand		= 23,
		StorageFull			= 30,
		FileIoError			= 31,
		NoWifiConnection	= 32,
		WifiConnectFailed	= 33,
		WifiConnected		= 50,
		WifiSsidNotFound	= 51,
		WifiAuthFail	    = 52,
		WifiDisconnected	= 53,
		Error				= 90,
	}

	public MessageType Type;
	public ResultCode Code;
	public string Message;
	public string? Body;
	private static string _chunk = "";
	private static ReceiveMessage? _chunkMessage = null;
	private static bool _lastLineEmpty = false;
	public ReceiveMessage(string src)
	{
		Type = MessageType.Unknown;
		var line = src.Trim();
		if (line.StartsWith(RESPONSE_PREFIX))
		{
			Type = MessageType.Response;
			line = line.Substring(RESPONSE_PREFIX.Length);
		}
		else if (line.StartsWith(NOTIFY_PREFIX))
		{
			Type = MessageType.Notify;
			line = line.Substring(NOTIFY_PREFIX.Length);
		}
		else
		{
			// Failover
			var p = line.IndexOf(RESPONSE_PREFIX);
			if (p != -1)
			{
				Type = MessageType.Response;
				line = line.Substring( p+RESPONSE_PREFIX.Length);
			}
			else
			{
				p = line.IndexOf(NOTIFY_PREFIX);
				if (p != -1)
				{
					Type = MessageType.Notify;
					line = line.Substring(p+NOTIFY_PREFIX.Length);
				}
			}

			if (Type == MessageType.Unknown)
			{
				Message = line;
				return;
			}
		}

		var mCode = Regex.Match(line, " +[0-9]* ");
		if (!mCode.Success)
			throw new DevicePortException("invalid result code");
		Code = (ResultCode)Int16.Parse(mCode.Value);
		line = line.Substring(mCode.Index + mCode.Length).Trim();
		if (string.IsNullOrEmpty(line))
			throw new DevicePortException("invalid message");
		Message = line;
	}

	public ReceiveMessage(ResultCode code, string message)
	{
		Type = MessageType.Response;
		Code = code;
		Message = message;
	}

	public static List<ReceiveMessage> ParseMessages(string data, Action<string>? onLine)
	{
		var rms = new List<ReceiveMessage>();

		var src = _chunk + data;
		_chunk = "";

		if (src.Length == 0)
			return rms;
		var lines = src
			.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
			.ToList();

		if (!src.EndsWith('\n'))
		{
			_chunk = lines[^1];
			lines.RemoveAt(lines.Count - 1);
			if (lines.Count == 0)
				return rms;
		}

		if (onLine != null)
		{
			foreach (var line in lines)
			{
				if (line.Length == 0) continue;
				onLine(line);
			}
		}
		
		var n = 0;
		while (n < lines.Count)
		{
			if (_chunkMessage == null)
			{
				var line = lines[n++];
				if (!string.IsNullOrEmpty(line))
				{
					var m = new ReceiveMessage(line);
					if (m.Message.EndsWith('+'))
					{
						_chunkMessage = m;
					}
					else
					{
						rms.Add(m);
					}
				}
				_lastLineEmpty = false;
			}
			else
			{
				var line = lines[n++];
				if (line == "")
				{
					if (_lastLineEmpty)
					{
						rms.Add(_chunkMessage);
						_chunkMessage = null;
						_lastLineEmpty = false;
					}
					else
					{
						_lastLineEmpty = true;
					}
				}
				else
				{
					_lastLineEmpty = false;
					if (_chunkMessage.Body != null)
					{
						_chunkMessage.Body += '\n' + line;
					}
					else
					{
						_chunkMessage.Body = line;
					}
				}
			}
		}

		return rms;
	}
}

public readonly record struct TransferProgress(long SentBytes, long TotalBytes)
{
	public double Ratio => TotalBytes == 0 ? 0 : (double)SentBytes / TotalBytes;
}

public class UsbDevicePort : IDisposable
{
	private SerialPort? _port;
	private string _portName;
	private string _status;
	public string PortAddress => _portName;
	public PortStatus PortStatus => new(_status, _port != null && _port.IsOpen);

	public UsbDevicePort(string portName)
	{
		_portName = portName;
		_status = "closed";
	}

	public void Dispose()
	{
		if (_port != null)
		{
			_port.Dispose();
			_port = null;
		}
	}

	public async Task ConnectAsync()
	{
		if (_port == null)
		{
			var port = new SerialPort();
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
			catch (Exception ex)
			{
				port.Close();
				throw;
			}
			_port = port;
			_status = "connected";
			_port.DataReceived += OnDataArrived;
		}
	}

	public void Disconnect()
	{
		if (_port != null)
		{
			_port.Close();
			_port = null;
			_status = "closed";
		}
	}

	private readonly Channel<Func<Task>> _txQueue =
		Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

	private Task? _txPump;

	private void EnsureTxPumpStarted()
	{
		_txPump ??= Task.Run(async () =>
		{
			await foreach (var job in _txQueue.Reader.ReadAllAsync())
			{
				await job().ConfigureAwait(false);
			}
		});
	}

	private readonly ConcurrentQueue<TaskCompletionSource<ReceiveMessage>> _pendingResponses = new();
	private readonly object _lineWaitLock = new();
	private readonly List<(Func<string, bool> pred, TaskCompletionSource<string> tcs)> _lineWaiters = new();

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

	public Task<string> WaitForLineAsync(Func<string, bool> predicate, TimeSpan timeout)
	{
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		lock (_lineWaitLock)
		{
			_lineWaiters.Add((predicate, tcs));
		}

		return tcs.Task.WaitAsync(timeout);
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

			var messages = ReceiveMessage.ParseMessages(data, onLine: FeedLine);
			lock (_rxLock)
			{
				foreach (var message in messages)
				{
					if (message.Type == ReceiveMessage.MessageType.Response)
					{
						if (_pendingResponses.TryDequeue(out var tcs))
							tcs.TrySetResult(message);
						else
							Debug.WriteLine($"UsbDevicePort: Orphan Response: {message}");
					}
					else if (message.Type == ReceiveMessage.MessageType.Notify)
					{
						OnNotify?.Invoke(this, message);
					}
					else
					{
						OnOther?.Invoke(this, message);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"UsbDevicePort: DataArrived error: {ex}");
		}
	}

	public event EventHandler<ReceiveMessage>? OnNotify;
	public event EventHandler<ReceiveMessage>? OnOther;

	private void SendToDevice(string message)
	{
		if (_port == null)
		{
			throw new DevicePortException("Device port not open");
		}
		_port.Write(message);
	}

	private void SendBytesToDevice(byte[] data, int offset, int count)
	{
		if (_port == null)
		{
			throw new DevicePortException("Device port not open");
		}

		_port.Write(data, offset, count);
	}

	private void FlushDevice()
	{
		_port?.BaseStream.Flush();
	}

	public Task SendRawAsync(string message)
	{
		EnsureTxPumpStarted();
		if (!_txQueue.Writer.TryWrite(() =>
		    {
			    SendToDevice(message);
			    return Task.CompletedTask;
		    }))
		{
			throw new DevicePortException("Tx queue is closed");			
		}
		return Task.CompletedTask;
	}

	public async Task<ReceiveMessage> SendReceiveAsync(string message, TimeSpan? timeout = null)
	{
		EnsureTxPumpStarted();
		timeout ??= TimeSpan.FromSeconds(3);
		var tcs = new TaskCompletionSource<ReceiveMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pendingResponses.Enqueue(tcs);
		if (!_txQueue.Writer.TryWrite(() =>
		    {
			    SendToDevice(message);
			    return Task.CompletedTask;
		    }))
		{
			throw new DevicePortException("Tx queue is closed");
		}
		return await tcs.Task.WaitAsync(timeout.Value).ConfigureAwait(false);
	}
	
	public async Task<ReceiveMessage> SendReceiveAsync(
		string message,
		byte[] data,
		IProgress<TransferProgress>? progress = null,
		TimeSpan? wait = null,
		TimeSpan? timeout = null)
	{
		EnsureTxPumpStarted();

		wait ??= TimeSpan.Zero;
		timeout ??= TimeSpan.FromSeconds(10);

		var tcs = new TaskCompletionSource<ReceiveMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pendingResponses.Enqueue(tcs);
		var sentTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		if (!_txQueue.Writer.TryWrite(async () =>
		    {
			    try
			    {
				    SendToDevice(message);
				    if (wait.Value > TimeSpan.Zero)
					    await Task.Delay(wait.Value).ConfigureAwait(false);
				    await WriteBytesWithProgressAsync(SendBytesToDevice, data, progress, wait.Value)
					    .ConfigureAwait(false);
				    FlushDevice();
				    sentTcs.TrySetResult();
			    }
			    catch (Exception ex)
			    {
				    sentTcs.SetException(ex);
				    tcs.TrySetException(ex);
			    }
		    }))
		{
			throw new DevicePortException("Tx queue is closed");
		}
		await sentTcs.Task.ConfigureAwait(false);
		return await tcs.Task.WaitAsync(timeout.Value).ConfigureAwait(false);
	}

	private static async Task WriteBytesWithProgressAsync(
		Action<byte[], int, int> sendBytes,
		byte[] data,
		IProgress<TransferProgress>? progress,
		TimeSpan interChunkDelay)
	{
		const int ChunkSize = 256;
		long total = data.LongLength;
		long sent = 0;

		progress?.Report(new TransferProgress(0, total));

		long reportStep = Math.Max(1024, total / 100);
		long nextReportAt = reportStep;

		for (int offset = 0; offset < data.Length; offset += ChunkSize)
		{
			int length = Math.Min(ChunkSize, data.Length - offset);
			sendBytes(data, offset, length);

			sent += length;

			if (progress != null && sent >= nextReportAt)
			{
				progress.Report(new TransferProgress(sent, total));
				nextReportAt = sent + reportStep;
			}

			if (interChunkDelay > TimeSpan.Zero)
				await Task.Delay(interChunkDelay).ConfigureAwait(false);
		}
		progress?.Report(new TransferProgress(total, total));
	}
}