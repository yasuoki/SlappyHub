namespace SlappyHub.Models;

public class NotificationEvent
{
	private string _source;
	private string _channel;
	private string _sender;
	private string _text;
	
	public string Source => _source;
	public string Channel => _channel;
	public string Sender => _sender;
	public string Text => _text;
	public string? LedPattern { get; set; }
	public string? Sound { get; set; }
	
	public NotificationEvent(string source, string channel, string sender, string text)
	{
		_source = source;
		_channel = channel;
		_sender = sender;
		_text = text;
	}
}
