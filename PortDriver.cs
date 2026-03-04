using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Windows.Foundation;

namespace SlappyHub;

public class PortDriverException(string message) : Exception(message);
public class PortDriverClosedException() : Exception("Device port is closed");

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
		NoWiFiConnection	= 32,
		WiFiConnectFailed	= 33,
		WiFiConnected		= 50,
		WiFiSsidNotFound	= 51,
		WiFiAuthFail	    = 52,
		WiFiDisconnected	= 53,
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
			throw new PortDriverException("invalid result code");
		Code = (ResultCode)Int16.Parse(mCode.Value);
		line = line.Substring(mCode.Index + mCode.Length).Trim();
		if (string.IsNullOrEmpty(line))
			throw new PortDriverException("invalid message");
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

public interface IDevicePort : IDisposable
{
	public string PortType { get; }
	public string Address { get; }
	public bool IsConnected { get; }
	
	public event TypedEventHandler<IDevicePort, string>? DataReceived;
	public event TypedEventHandler<IDevicePort, object>? Disconected;
	public Task ConnectAsync();
	public void Disconnect();
	public Task SendToDeviceAsync(string message);
	public Task SendBytesToDeviceAsync(byte[] data, int offset, int count);
	public void FlushDevice();
}

public class PortDriver : IDisposable
{
	private IDevicePort _port;
	public IDevicePort Port => _port;
	public event EventHandler<ReceiveMessage>? OnNotify;
	public event EventHandler<ReceiveMessage>? OnOther;

	public PortDriver(IDevicePort port)
	{
		_port = port;
		_port.DataReceived += OnDataReceived;
	}

	public void Dispose()
	{
		_port.DataReceived -= OnDataReceived;
		_port.Dispose();
	}

	public async Task ConnectAsync()
	{
		if(_port.IsConnected) 
			return;
		Debug.WriteLine($"PortDriver: ConnectAsync {_port.PortType} {_port.Address}");
		await _port.ConnectAsync();
	}

	public void Disconnect()
	{
		if (_port.IsConnected) {
			Debug.WriteLine($"PortDriver: Disconnect {_port.PortType}");
			_port.Disconnect();
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

	private void OnDataReceived(object? sender, string data)
	{
		Debug.WriteLine($"{DateTime.Now} PortDriver: DataArrived: {data} len={data.Length}");
		try
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.WriteLine($"{DateTime.Now} PortDriver: DataArrived: null or empty");
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
							Debug.WriteLine($"PortDriver: Orphan Response: {message}");
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
			Debug.WriteLine($"PortDriver: DataArrived error: {ex}");
		}
	}


	public Task SendRawAsync(string message)
	{
		EnsureTxPumpStarted();
		if (!_txQueue.Writer.TryWrite(async () => 
		    {
			    await _port.SendToDeviceAsync(message);
		    }))
		{
			throw new PortDriverException("Tx queue is closed");			
		}
		return Task.CompletedTask;
	}

	public async Task<ReceiveMessage> SendReceiveAsync(string message, TimeSpan? timeout = null)
	{
		Debug.WriteLine($"{DateTime.Now} PortDriver: SendReceiveAsync: {message} Transport={_port.PortType}");
		EnsureTxPumpStarted();
		timeout ??= TimeSpan.FromSeconds(3);
		var tcs = new TaskCompletionSource<ReceiveMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!_txQueue.Writer.TryWrite(async () =>
		    {
			    _pendingResponses.Enqueue(tcs);
			    await _port.SendToDeviceAsync(message);
		    }))
		{
			throw new PortDriverException("Tx queue is closed");
		}

		try
		{
			return await tcs.Task.WaitAsync(timeout.Value).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			_pendingResponses.TryDequeue(out _);
			throw;
		}
	}
	
	public async Task<ReceiveMessage> SendReceiveAsync(
		string message,
		byte[] data,
		IProgress<TransferProgress>? progress = null,
		TimeSpan? wait = null,
		TimeSpan? timeout = null)
	{
		EnsureTxPumpStarted();

		if (wait == null)
		{
			if (_port is UsbDevicePort)
			{
				wait = TimeSpan.FromMilliseconds(200);
			}
			else
			{
				wait = TimeSpan.FromMilliseconds(50);
			}
		}
		timeout ??= TimeSpan.FromSeconds(10);

		var sentTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var tcsCmd = new TaskCompletionSource<ReceiveMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		var tcsData = new TaskCompletionSource<ReceiveMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

		if (!_txQueue.Writer.TryWrite(async () =>
		    {
			    try
			    {
				    _pendingResponses.Enqueue(tcsCmd);
					await _port.SendToDeviceAsync(message);
					try
					{
						var ret = await tcsCmd.Task.WaitAsync(timeout.Value).ConfigureAwait(false);
						if (ret.Code != ReceiveMessage.ResultCode.Success)
						{
							tcsData = null;
							throw new PortDriverException(ret.Message);
						}
					}
					catch (OperationCanceledException)
					{
						_pendingResponses.TryDequeue(out _);
						tcsData = null;
						throw;
					}
				    
				    _pendingResponses.Enqueue(tcsData);
				    await WriteBytesWithProgressAsync(_port, data, progress, wait.Value)
					    .ConfigureAwait(false);
				    _port.FlushDevice();
				    sentTcs.TrySetResult();
			    }
			    catch (Exception ex)
			    {
				    sentTcs.SetException(ex);
				    if(tcsData != null)
					    tcsData.TrySetException(ex);
			    }
		    }))
		{
			throw new PortDriverException("Tx queue is closed");
		}
		await sentTcs.Task.ConfigureAwait(false);
		return await tcsData.Task.WaitAsync(timeout.Value).ConfigureAwait(false);
	}

	private static async Task WriteBytesWithProgressAsync(
		IDevicePort port,
		byte[] data,
		IProgress<TransferProgress>? progress,
		TimeSpan interChunkDelay)
	{
		const int ChunkSize = 250;
		long total = data.LongLength;
		long sent = 0;

		progress?.Report(new TransferProgress(0, total));

		long reportStep = Math.Max(1024, total / 100);
		long nextReportAt = reportStep;

		for (int offset = 0; offset < data.Length; offset += ChunkSize)
		{
			int length = Math.Min(ChunkSize, data.Length - offset);
			await port.SendBytesToDeviceAsync(data, offset, length);

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