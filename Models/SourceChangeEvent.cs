using SlappyHub.Services;

namespace SlappyHub.Models;

public class SourceChangeEvent
{
	private ChannelSourceMode _channeSource;
	private bool _isConnected;
	
	public ChannelSourceMode ChannelSource => _channeSource;
	public bool IsConnected => _isConnected;
	public SourceChangeEvent(ChannelSourceMode channelSource, bool isConnected)
	{
		_channeSource = channelSource;
		_isConnected = isConnected;
	}
}
