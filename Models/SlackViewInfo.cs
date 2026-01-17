namespace SlappyHub.Models;

public class SlackViewInfo
{
	public string WorkspaceName { get; init; }
	public string Channel { get; init; }
	public string Sender { get; init; }

	public SlackViewInfo(string workspaceName, string channel, string sender)
	{
		WorkspaceName = workspaceName;
		Channel = channel;
		Sender = sender;
	}
}