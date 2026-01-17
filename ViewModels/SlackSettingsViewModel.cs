using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SlappyHub.Common;
using SlappyHub.Services;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;

namespace SlappyHub.ViewModels;

public sealed class SlackSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<bool>? RequestClose;


    // ===== Commands =====
    public ICommand RequestWindowsNotifyAccessCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    // ===== Mode（XAMLは bool 3つを参照）======
    private ChannelSourceMode _channelSource = ChannelSourceMode.Undefined;


    public bool CanTest { get; set; } = true;
    public bool IsChannelSocket
    {
        get { return _channelSource == ChannelSourceMode.Socket; }
        set
        {
            if (value)
            {
                _channelSource = ChannelSourceMode.Socket;
            }
            OnPropertyChanged(nameof(IsChannelSocket));
        }
    }

    public bool IsChannelNotify
    {
        get { return _channelSource == ChannelSourceMode.WindowsNotify; }
        set {
            if (value)
            {
                _channelSource = ChannelSourceMode.WindowsNotify;
            }
            OnPropertyChanged(nameof(IsChannelNotify));
        }
    }
    public bool IsChannelMasterNode {
        get { return _channelSource == ChannelSourceMode.MasterNode;}
        set {
            if (value)
            {
                _channelSource = ChannelSourceMode.MasterNode;
            }
            OnPropertyChanged(nameof(IsChannelMasterNode));
        }
    }
    
    // Slack Socket(BOT)設定
    public string AppToken { get; set; } = "";
    public string BotToken { get; set; } = "";
    
    // Windows Notify監視設定
    // ChannelSource = ChannelSourceMode.WindowsNotifyの場合に監視するワークスペースを設定（オプション）
    public string CaptureWorkspace { get; set; } = "";
    public string WindowsNotifyAccessStatus { get; private set; } = "ABC";
    
    // Master Node設定
    // ChannelSource = ChannelSourceMode.MasterNodeの場合にMasterNodeへの接続設定が必要
    public string MasterNodeHost { get; set; } = "";
    public string MasterNodePortText { get; set; } = "";
    public string MasterNodePassword { get; set; } = "";
    
    // Master Nodeの構成情報
    // ChannelSource = ChannelSourceMode.Socketの場合のみtrueにでき、Masterノードとなれる
    private bool _useMasterNode;
    public bool UseMasterNode
    {
        get => _useMasterNode;
        set
        {
            if (_useMasterNode == value) return;
            _useMasterNode = value;
            OnPropertyChanged();
        }
    }
    public string MasterListenPortText { get; set; } = "";
    public string MasterPassword { get; set; } = "";
    
    // ダイレクトメッセージ受信設定
    public bool EnableDirectMessage { get; set; } = false;
    
    public SlackSettingsViewModel()
    {
        CanTest = true;
        // Commands
        OkCommand = new RelayCommand(OnOk);
        CancelCommand = new RelayCommand(OnCancel);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        RequestWindowsNotifyAccessCommand =
            new AsyncRelayCommand(RequestWindowsNotifyAccessAsync);
    }

    public void LoadFrom(AppSettings s)
    {
        // Channel Source 
        _channelSource = s.ChannelSource;
        
        // ChannelSource = Socket(BOT)設定
        AppToken = s.SlackAppToken ?? "";
        BotToken = s.SlackBotToken ?? "";
        
        // ChannelSource = WindowsNotify設定
        CaptureWorkspace = s.CaptureWorkspace ?? "";
        
        
        // ChannelSource = MasterNode設定
        MasterNodeHost = s.MasterNodeHost ?? "";
        MasterNodePortText = s.MasterNodePort?.ToString() ?? "";
        MasterNodePassword = s.MasterNodePassword ?? "";
        
        // Master Nodeの構成情報
        UseMasterNode = s.UseMasterNode;
        MasterListenPortText = s.MasterListenPort?.ToString() ?? "";
        MasterPassword = s.MasterPassword ?? "";
        // ダイレクトメッセージ受信設定
        EnableDirectMessage = s.EnableDirectMessage;
        
        RaiseAll(); 
    }
    
    public AppSettings ApplyTo(AppSettings s)
    {
        int? masterNodePort = TryParsePort(MasterNodePortText);
        int? listenPort = TryParsePort(MasterListenPortText);

        // ChannelSourceに応じて必要な値だけ残し、不要な値はnullへ（後々の混乱を防ぐ）
        var channel = _channelSource;

        // 自分がMasterになれるのは Socket のときだけ
        var useMaster = UseMasterNode;

        return s with
        {
            ChannelSource = channel,

            SlackAppToken = NullIfEmpty(AppToken),
            SlackBotToken = NullIfEmpty(BotToken),
            
            CaptureWorkspace = NullIfEmpty(CaptureWorkspace),

            MasterNodeHost = NullIfEmpty(MasterNodeHost),
            MasterNodePort = masterNodePort,
            MasterNodePassword = NullIfEmpty(MasterNodePassword),

            UseMasterNode = useMaster,
            MasterListenPort = listenPort,
            MasterPassword = NullIfEmpty(MasterPassword),

            EnableDirectMessage = EnableDirectMessage,
        };
    }
    
    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsChannelSocket));
        OnPropertyChanged(nameof(IsChannelNotify));
        OnPropertyChanged(nameof(IsChannelMasterNode));
        OnPropertyChanged(nameof(AppToken));
        OnPropertyChanged(nameof(BotToken));
        OnPropertyChanged(nameof(CaptureWorkspace));
        OnPropertyChanged(nameof(MasterNodeHost));
        OnPropertyChanged(nameof(MasterNodePortText));
        OnPropertyChanged(nameof(MasterNodePassword));
        OnPropertyChanged(nameof(UseMasterNode));
        OnPropertyChanged(nameof(MasterListenPortText));
        OnPropertyChanged(nameof(MasterPassword));
        OnPropertyChanged(nameof(EnableDirectMessage));
    }
    
    // ===== actions =====

    private async Task RequestWindowsNotifyAccessAsync()
    {
        try
        {
            await WindowsNotificationTester.TestAsync();
            System.Windows.MessageBox.Show("通知へのアクセスが許可されました。");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"{ex.Message}\r\n[設定]の[プライバシーとセキュリティ]から通知へのアクセスを許可してください。");
        }
        finally
        {
            CanTest = true;
        }
    }
    
    private async Task TestConnectionAsync()
    {
        await Task.Yield();
        if (!CanTest) return; // 連打防止
        CanTest = false;
        try
        {
            if (!TryValidate(out var error))
            {
                System.Windows.MessageBox.Show(error, "接続テスト（入力エラー）");
                return;
            }
            try
            {
                if (_channelSource == ChannelSourceMode.Socket)
                {
                    await SlackConnectionTester.TestAsync(AppToken, BotToken);
                }
                else if (_channelSource == ChannelSourceMode.WindowsNotify)
                {
                    await WindowsNotificationTester.TestAsync();
                }

                System.Windows.MessageBox.Show("接続テストに成功しました");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "接続テスト（例外）");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "接続テスト（例外）");
        }
        finally
        {
            CanTest = true;
        }
    }

    private void OnOk()
    {
        if (!TryValidate(out var error))
        {
            System.Windows.MessageBox.Show(error, "入力エラー");
            return;
        }
        RequestClose?.Invoke(this, true);
    }
    
    private void OnCancel()
    {
        RequestClose?.Invoke(this, false);
    }
    private bool TryValidate(out string error)
    {
        error = "";
        if (_channelSource == ChannelSourceMode.Socket)
        {
            if (string.IsNullOrWhiteSpace(AppToken) || !AppToken.StartsWith("xapp-", StringComparison.OrdinalIgnoreCase))
            {
                error = "App Token（xapp-…）を入力してください。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(BotToken) || !BotToken.StartsWith("xoxb-", StringComparison.OrdinalIgnoreCase))
            {
                error = "Bot Token（xoxb-…）を入力してください。";
                return false;
            }
        }
        else if (_channelSource == ChannelSourceMode.MasterNode)
        {
            if (string.IsNullOrWhiteSpace(MasterNodeHost))
            {
                error = "Master Node IP/Hostを入力してください。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(MasterNodePortText))
            {
                error = "Matser Node Portを入力してください。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(MasterNodePassword))
            {
                error = "Master Node Passwordを入力してください。";
                return false;
            }

            if (TryParsePort(MasterNodePortText) == null)
            {
                error = "Matser Node Portは1~65535の値を入力してください。";
                return false;
            }
        }

        if (UseMasterNode) 
        {
            if (string.IsNullOrWhiteSpace(MasterListenPortText))
            {
                error = "Matser Listen Portを入力してください。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(MasterPassword))
            {
                error = "Master Passwordを入力してください。";
                return false;
            }

            if (TryParsePort(MasterListenPortText) == null)
            {
                error = "Matser Listen Portは1~65535の値を入力してください。";
                return false;
            }
        }
        return true;
    }

    private static int? TryParsePort(string text)
        => int.TryParse(text, out var p) && p is >= 1 and <= 65535 ? p : null;
    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


