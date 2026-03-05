using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

using SlappyHub.Services;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;
using SlappyHub.ViewModels;
using SlappyHub.Views;
using Application = System.Windows.Application;

namespace SlappyHub;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;
    public MainWindow? _mainWindow { get; private set; } = null;
    private NotifyIcon? _notifyIcon;
    private const string MutexName = "SlappyHub.Singleton";
    private const string PipeName = "SlappyHub.Activate";
    private Mutex? _mutex;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        bool created;

        _mutex = new Mutex(true, MutexName, out created);

        if (!created)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(100);
                using var writer = new StreamWriter(client);
                writer.WriteLine("ACTIVATE");
                writer.Flush();
            }
            catch { }

            Shutdown();
            return;
        }
        StartPipeServer();
        base.OnStartup(e);
        var sc = new ServiceCollection();
        sc.AddSingleton<UsbWatcher>();
        sc.AddSingleton<BleWatcher>();
        sc.AddSingleton<SettingsStore>();
        sc.AddSingleton<NotifyExtension>();
        sc.AddSingleton<SlackConnector>();
        sc.AddSingleton<WindowsNotificationConnector>();
        sc.AddSingleton<MessageSourceController>();
        sc.AddSingleton<SlackAppWatcher>();
        
        sc.AddSingleton<NotificationRouter>();
        sc.AddSingleton<SlappyBellController>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<SlackSettingsViewModel>();
        sc.AddSingleton<NotifySettingsViewModel>();
        sc.AddSingleton<MainWindow>();
        
        Services = sc.BuildServiceProvider();
        Services.GetRequiredService<SettingsStore>().Load();
        _mainWindow = Services.GetRequiredService<MainWindow>();//.Show();
        Services.GetRequiredService<NotifyExtension>().Start();
        Services.GetRequiredService<SlackAppWatcher>().Start();
        Services.GetRequiredService<NotificationRouter>().Start();
        _ = Services.GetRequiredService<MessageSourceController>().Start();
        _ = Services.GetRequiredService<SlappyBellController>().Start();
        Services.GetRequiredService<UsbWatcher>().Start();
        Services.GetRequiredService<BleWatcher>().Start();
        
        InitializeTrayIcon();
    }
    
    private async void StartPipeServer()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                using var server = new NamedPipeServerStream("SlappyHub.Activate", PipeDirection.In);

                await server.WaitForConnectionAsync();

                using var reader = new StreamReader(server);
                var msg = await reader.ReadLineAsync();

                if (msg == "ACTIVATE")
                {
                    Dispatcher.Invoke(() =>
                    {
                        ActivateMainWindow();
                    });
                }
            }
        });
    }
    
    private void InitializeTrayIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/profile.ico");
        using Stream iconStream = Application.GetResourceStream(uri)!.Stream;
        
        _notifyIcon = new NotifyIcon
        {
            Icon =new System.Drawing.Icon(iconStream),
            Text = "SlappyHub",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleMainWindow();
            }
        };
    }
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("表示", null, (_, _) => ShowMainWindow());
        menu.Items.Add("終了", null, (_, _) => ExitApplication());

        return menu;
    }
    private void ActivateMainWindow()
    {
        if (MainWindow == null)
            return;
        ShowMainWindow();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Focus();
    }
    
    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            ShowMainWindow();
        }
    }
    
    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _notifyIcon!.Visible = false;
        _notifyIcon.Dispose();
        Shutdown();
    }
    public App()
    {
        CultureInfo osCulture = CultureInfo.InstalledUICulture;
        Thread.CurrentThread.CurrentUICulture = osCulture;
        Thread.CurrentThread.CurrentCulture = osCulture;
    }
}
