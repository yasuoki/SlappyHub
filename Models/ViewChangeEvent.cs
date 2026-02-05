namespace SlappyHub.Models;

public class ViewChangeEvent
{
	private string _source;
	private string _channel;
	private string _sender;
	
	public string Source => _source;
	public string Channel => _channel;
	public string Sender => _sender;
	public ViewChangeEvent(string source,  string channel, string sender)
	{
		_source = source;
		_channel = channel;
		_sender = sender;
	}
}
