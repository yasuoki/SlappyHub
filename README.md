# SlappyHub
## About
**もう、Slackのメッセージを見落として、対応が遅れたり後で気まずくなることはありません**  
SlappyHubは、Slackのメッセージが届くとデスクトップガジェット(ハードウェア）SlappyBellと連動し、LEDの発光と音で通知するアプリケーションです。  
[SlappyBell ソースコード リポジトリ](https://github.com/yasuoki/SlappyBell)


SlappyHubアプリケーションのUI  
<img src="img/fig0.png" width="80%"><br>  
---------------------
SlappyBell  
<img src="img/fig1.jpg" width="80%"><br>  
---------------------

***Youtube***   
[<img src="https://img.youtube.com/vi/7GKKwvN1aGI/hqdefault.jpg" width="80%"/>](https://www.youtube.com/embed/7GKKwvN1aGI)

## Who is this for?
- Slackメッセージの着信を常時気にしていられない人
- 離席中でも重要な通知を確実に受け取りたい人
- デスクトップガジェットが好きな人

## Features
- **Slackメッセージの通知**  
Slackと連動し、メッセージの着信をLEDと音声で通知します。  
連携は、SlackのボットとしてSlackサービスに接続する形態のほか、Windowsの通知を監視して連携する2種類のモードがあります。

- **LED通知**  
通知するチャンネルやDMを6個のキノコに割り当て、メッセージの受信でキノコに仕込まれたフルカラーLEDをさまざまな発光パターンで発光させることができます。
LEDの発光は、Slackアプリで通知されたチャンネルを表示するまで継続するので、離席中の着信でも見落としません。
  
- **音声通知**  
6個のキノコに割り当てたチャンネルやDMに応じて、SlappyBellに内蔵するスピーカーから様々な着信音を鳴らすことができます。
もちろん音量の調整や一時的なミュートにも対応しています。

- **Wi-Fi接続**  
着信音は、SlappyBell本体にアップロードした mp3 ファイルを再生するのが基本動作です。  
また、SlappyBellのWi-Fi接続機能を利用して、http://～.mp3 のURLを指定し、ネット上の mp3 ファイルを再生することもできます。  

- **簡単な設定**  
Windowsの通知を監視するモードでは、Slack側の設定が不要なため簡単に利用できます。  
一方、Slackのボットとして接続するモードでは設定はやや手間ですが、より正確な通知が可能です。

## System Requirements
- SlappyBell デバイス
- Windows 11
- .NET 9.0
- Windows版 Slack デスクトップアプリケーション（ブラウザ版は非対応）

## Slack ボット連携
Slackボットモードは、Windows通知モードと比較し、チャンネルメッセージ着信後、より素早く反応することができますが、Windows通知モードでもメッセージの着信から反応するまでの時間は、およそ2秒以内なのでそれほどメリットがあるわけでもありません。（Youtubeの動画では反応が遅いと言っていますが、すでに改善されています！）  
もう一つのメリットはメッセージの全文を把握できるため、テキストフィルターを正確に処理することができる点ですが、テキストフィルターを使用しない場合にはそのメリットもありません。  
一方で、Slackボットモードでは、Slackアプリで設定する通知スケジュール（深夜や休日は通知しないなど）と無関係に反応するため、それがメリットになることもデメリットになることもあります。  
総合的に見て、より手軽で便利なWindows通知モードの利用をお勧めします。  

Slackのボットとして接続（Events API / Socket Mode）するためには、https://api.slack.com/apps/ からAppを作成し、接続トークンをSlappyHubに設定する必要があります。  
トークンはxapp-で始まるApp-Levelトークンと、xoxb-で始まるBot User OAuthトークンが必要になります。
トークン発行など詳細手順は、Slackの公式ページの説明を参照してください。  

App-Levelトークンは`connections:write`スコープを与えてください。

SlappyHubの動作には、OAuthトークンに次の権限が必要です。
```
app_mentions:read
channels:history
groups:history
im:history
mpim:history
channels:read
groups:read
mpim:read
reactions:read
users:read
```

また、通知したいチャンネルにSlappyHubボットを連携される必要があります。Slackアプリのチャンネル詳細から、インテグレーションにSlappyHubのAppを追加してください。

※ この設定は **Slackボット連携モードを使用する場合のみ必要** です。  
※ Slackはひとつのワークスペースに同時に接続するソケットモードコネクションを制限しています。多くの人が参加するワークスペースでは接続数の制約にかかる可能性があります。

## Extension
JavaScriptで拡張することにより、メールなどSlack以外の通知を行うことができます。  
この仕組みは、Windows通知とアプリケーションのウィンドウの状態変化の監視がベースとなっており、JavaScriptで希望するアプリケーションに応じたフィルタリングを行うことで実現されます。
JavaScriptはSlappyHub.exeと同じディレクトリに配置した、`slappy_extension.js`ファイルに記述します。  
SlappyHubは、`slappy_extension.js`ファイルの存在と更新を監視し、常に最新の状態で実行されます。

### 通知の生成
`slappy_extension.js`に次のようなonNotify関数を定義します。

``` javascript
onNotify = function(app, title, body) {
//	Log.print(`onNotify(app=${app} title=${title} body=${body})`);
	if(app.match(/thunderbird/i)) {
		var seg = body.split(":");
		if(seg.length >= 2) {
			var sender = seg[0];
			if(sender.endsWith(" より")) {
				sender = sender.slice(0,-3);
			}
			body = seg.filter(n => n!=0).join();
			var e = new NotificationEvent("thunderbird","[Mail]",sender,body);
			e.LedPattern = "ff0000,00ff00";
			e.Sound = "butuyoku.mp3";
		}
	}
	return null;
}
```
このコード例は、Thunderbirdメーラの着信を通知しています。  
onNotify関数は、Windows通知に新しい通知が到着すると呼び出され、戻り値としてNotificationEventオブジェクトを返すと通知の候補となります。  

onNotify関数の引数は以下の意味を持ちます。
- app: Windows通知を発行したアプリケーション名
- title: Windows通知のタイトル
- body: Windows通知の本文

NotificationEventコンストラクタは以下の引数を取ります。
``` javascript
NotificationEvent(source,channel,sender,body)
```
- source: 通知元のアプリケーション
- channel: 通知するチャンネル名
- sender: 送信者
- body: 通知の本文

生成したNotificationEventオブジェクトのchannelが、SlappyBellの通知スロット（キノコ）の定義に一致すると、そのスロットで通知されますが、一致する定義がない場合には通知されません。  
また、設定で送信者と本文のフィルターが指定され、senderやbodyがマッチする場合も通知されません。  
通知元アプリケーションを示すsourceは、アプリケーションのウィンドウ状態と通知の制御で使用されます。  
通知される際のLED発光パターンと着信音は設定に従いますが、NotificationEventオブジェクトのLedPatternとSoundプロパティで、設定とは別の通知方法に変更することもできます。  

### LEDの消灯
SlappyBellの通知で点灯したLEDは、対応するアプリケーションのウィンドウが前面なった時や、さらにウィンドウタイトルが特定の状態に変換したときに消灯します。  
これらの状態検知は、次のように`onForeground`関数と`onTitleChange`関数を定義することで行われます。

```javascript
onForeground = function(processName,title) {
//	Log.print(`onForeground(processName=${processName} title=${title})`);
	if(processName.match(/thunderbird/i)) {
		return new ViewChangeEvent("thunderbird", "[Mail]", "");
	}
	return null;
}

onTitleChange = function(processName,title) {
//	Log.print(`onTitleChange(processName=${processName} title=${title})`);
	return null;
}
```
このコード例は、Thunderbirdメーラの通知LEDを消灯する状態を定義しています。  
`onForeground`関数は、デスクトップ上でウィンドウの前後関係が変化したときに呼び出され、`onTitleChange`関数は最前面のウィンドウのタイトルが変化したときに呼び出されます。  
どちらの関数もLEDを消灯するべき状態になったと判断した場合に、ViewCHangeEventオブジェクトを戻り値として返します。

コード例では、`onForeground`関数ではThunderbirdメーラが前面に出たことを検知すると、ViewChangeEventを生成して戻り値として返し、"[Mail]"スロットのLEDを消灯するようにSlappyHubに指示しています。  
`onTitleChange`関数は常にnullを返し、ウィンドウタイトルの変化は無視されます。

これらの関数の引数は以下の意味を持ちます。
- processName: 最前面またはタイトルの変化したウィンドウを表示しているプロセスの名前
- title: ウィンドウタイトル

ViewChangeEventコンストラクタは以下の引数を取ります。
``` javascript
ViewChangeEvent(source,channel,sender)
```
- source: 通知元のアプリケーション
- channel: 通知するチャンネル名
- sender: 送信者

※ sourceとsenderは、現在SlappyHub内で使用されていませんが、適切な値を指定するようにしてください。

### ログの出力
記述したJavaScriptにエラーがあったり例外が発生すると、`slappy_extension.js`ファイルと同じディレクトリに`slappy_hub.log`ファイルにその内容がログ出力されます。  
また、Log.print(text)関数を呼び出すことでも、ログを出力できます。

