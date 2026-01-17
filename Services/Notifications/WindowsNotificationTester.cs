using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace SlappyHub.Services.Notifications;

public static class WindowsNotificationTester
{
	public static async Task TestAsync()
	{
		if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
		{
			throw new Exception("通知の取得に対応していません");
		}
		var listener = UserNotificationListener.Current;
		var access = await listener.RequestAccessAsync();

		if (access != UserNotificationListenerAccessStatus.Allowed)
		{
			throw new Exception( "通知へのアクセスは許可されませんでした");
		}
	}
}
