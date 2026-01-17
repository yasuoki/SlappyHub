using SlackNet;

namespace SlappyHub.Services.Slack;
public sealed record SlackTestResult(bool Ok, string Message);

public static class SlackConnectionTester
{
	public static async Task TestAsync(
		string appToken,
		string botToken,
		CancellationToken ct = default)
	{
		string? error = null;
		try
		{
			var botApi = new SlackServiceBuilder()
				.UseApiToken(botToken)
				.GetApiClient();

			await botApi.Auth.Test(ct);
		}
		catch (Exception ex)
		{
			error = $"BotToken の確認に失敗しました:\r\n{ex.Message}";
		}

		try
		{
			var appApi = new SlackServiceBuilder()
				.UseApiToken(appToken)
				.GetApiClient();

			var open = await appApi.AppsConnectionsApi.Open(ct);
			if (string.IsNullOrWhiteSpace(open?.Url))
				error = "AppTokenは受理されましたが、接続URLが取得できませんでした。";
		}
		catch (Exception ex)
		{
			error = $"AppTokenの確認に失敗しました:\r\n{ex.Message}";
		}
		if(error != null)
			throw new Exception(error);
	}
}
