using System.Text.Json.Serialization;

namespace SlappyHub.Services;


public enum ChannelSourceMode
{
	Undefined,
	Socket,
	WindowsNotify,
	MasterNode
}

public record NotifySettings
{
	public string Channel { get; init; } = "";
	public string Sound { get; init; } = "";
	public string LedPattern { get; init; } = "";
	public string SenderFilter { get; init; } = "";
	public string TextFilter { get; init; } = "";
	public string Script { get; init; } = "";
}


public record AppSettings
{
	// Channelメッセージソースの設定
	public ChannelSourceMode ChannelSource { get; init; } = ChannelSourceMode.WindowsNotify;

	// Slack Socket(BOT)設定
	// ChannelSource = ChannelSourceMode.Socketの場合に接続情報の設定が必要
	public string? SlackAppToken { get; init; }
	public string? SlackBotToken { get; init; }

	// Windows Notify監視設定
	// ChannelSource = ChannelSourceMode.WindowsNotifyの場合に監視するワークスペースを設定（オプション）
	public string? CaptureWorkspace { get; init; }

	// Master Node設定
	// ChannelSource = ChannelSourceMode.MasterNodeの場合にMasterNodeへの接続設定が必要
	public string? MasterNodeHost { get; init; }
	public int? MasterNodePort { get; init; }
	public string? MasterNodePassword { get; init; }

	// Master Nodeの構成情報
	// ChannelSource = ChannelSourceMode.Socketの場合のみtrueにでき、Masterノードとなれる
	public bool UseMasterNode { get; init; }	
	public int? MasterListenPort { get; init; }
	public string? MasterPassword { get; init; }
	
	// ダイレクトメッセージ受信設定
	public bool EnableDirectMessage { get; init; }

	// Wi-Fi設定（mp3ファイルのダウンロードでのみ利用)
	public string? WiFiSsid { get; init; }
	public string? WiFiPassword { get; init; }
	
	// 着信音の音量(0%~100%)
	public int Volume { get; init; } = 100;
	public bool Mute { get; init; } = false;

	// メッセージのフィルタリングと通知方法の設定
	public NotifySettings Slot0 { get; init; } = new NotifySettings();
	public NotifySettings Slot1 { get; init; } = new NotifySettings();
	public NotifySettings Slot2 { get; init; } = new NotifySettings();
	public NotifySettings Slot3 { get; init; } = new NotifySettings();
	public NotifySettings Slot4 { get; init; } = new NotifySettings();
	public NotifySettings Slot5 { get; init; } = new NotifySettings();
	[JsonIgnore]
	public NotifySettings[] NotifySettings {
		get => new NotifySettings[] { Slot0, Slot1, Slot2, Slot3, Slot4, Slot5 };
	}
}

