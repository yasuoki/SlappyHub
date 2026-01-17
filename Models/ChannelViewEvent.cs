namespace SlappyHub.Models;

public class SlackViewChangeEvent
{
	private bool _isForeground;
	private string _workspaceName;
	private string _channel;
	private string _sender;
	
	public bool IsForeground => _isForeground;
	public string WorkspaceName => _workspaceName;
	public string Channel => _channel;
	public string Sender => _sender;
	public SlackViewChangeEvent(bool isForeground, string workspaceName,  string channel, string sender)
	{
		_workspaceName = workspaceName;
		_isForeground = isForeground;
		_channel = channel;
		_sender = sender;
	}
}
