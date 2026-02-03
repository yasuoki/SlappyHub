using System.Globalization;
using System.IO;
using System.Windows;
using SlappyHub.WebServer;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sc = new ServiceCollection();
        sc.AddSingleton<UsbWatcher>();
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
        Services.GetRequiredService<MessageSourceController>().Start();
        Services.GetRequiredService<SlappyBellController>().Start();
        Services.GetRequiredService<UsbWatcher>().Start();
        
        InitializeTrayIcon();
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
