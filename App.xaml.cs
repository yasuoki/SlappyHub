using System.Globalization;
using System.Windows;
using SlappyHub.WebServer;
using Microsoft.Extensions.DependencyInjection;

using SlappyHub.Services;
using SlappyHub.Services.Notifications;
using SlappyHub.Services.Slack;
using SlappyHub.ViewModels;
using SlappyHub.Views;

namespace SlappyHub;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sc = new ServiceCollection();
        sc.AddSingleton<UsbWatcher>();
        sc.AddSingleton<SettingsStore>();
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
        Services.GetRequiredService<MainWindow>().Show();
        Services.GetRequiredService<SlackAppWatcher>().Start();
        Services.GetRequiredService<NotificationRouter>().Start();
        Services.GetRequiredService<MessageSourceController>().Start();
        Services.GetRequiredService<SlappyBellController>().Start();
        Services.GetRequiredService<UsbWatcher>().Start();
    }
    public App()
    {
        CultureInfo osCulture = CultureInfo.InstalledUICulture;
        Thread.CurrentThread.CurrentUICulture = osCulture;
        Thread.CurrentThread.CurrentCulture = osCulture;
    }
}
