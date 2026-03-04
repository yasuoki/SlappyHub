namespace SlappyHub.Models;

public class DeviceInfo
{
	public string Transport { get; init; }
	public string Address { get; init; }
	public string Description { get; init; }
	public DateTime LastSeen { get; set; }

	public DeviceInfo(string transport, string address, string description)
	{
		Transport = transport;
		Address = address;
		Description = description;
		LastSeen = DateTime.Now;
	}
}
