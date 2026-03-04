using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using SlappyHub.Models;
using SlappyHub.Services;
using SlappyHub.ViewModels;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace SlappyHub.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	public MainWindow(MainViewModel vm)
	{
		InitializeComponent();
		DataContext = vm;
		vm.SlackSettingsRequested += (_, open) =>
		{
			if (open) ShowSlackOverlay();
			else HideSlackOverlay();
		};
		vm.NotifySettingsRequested += (_, open) =>
		{
			if (open) ShowNotifyOverlay();
			else HideNotifyOverlay();
		};
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (!Application.Current.ShutdownMode.Equals(ShutdownMode.OnExplicitShutdown))
		{
			e.Cancel = true;
			Hide();
		}
	}
	private void Repo_RequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
		{
			UseShellExecute = true
		});
		e.Handled = true;
	}
	
	private void Hotspot_Enter(object sender, MouseEventArgs e)
	{
	}

	private void Hotspot_Leave(object sender, MouseEventArgs e)
	{
	}

	//-----------------------------------------
	// Slack Connect settings
	//-----------------------------------------
	private void SlackIcon_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenSlackSettingsCommand.Execute(null);
		e.Handled = true;
	}

	private void SlackOverlay_BackgroundClick(object sender, MouseButtonEventArgs e)
	{
		HideSlackOverlay();
		e.Handled = true;
	}

	private void SlackDrawer_Click(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void ShowSlackOverlay()
	{
		SlackOverlay.Visibility = Visibility.Visible;
		((Storyboard)Resources["ShowSlackDrawer"]).Begin();
	}

	private void HideSlackOverlay()
	{
		var sb = (Storyboard)Resources["HideSlackDrawer"];
		sb.Completed += (_, __) => SlackOverlay.Visibility = Visibility.Collapsed;
		sb.Begin();
	}
	
	//-----------------------------------------
	// Sound settings
	//-----------------------------------------
	private void VolumeIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		VolumePopup.IsOpen = !VolumePopup.IsOpen;
	}

	private void VolumePopup_Closed(object sender, EventArgs e)
	{
		if (DataContext is MainViewModel vm)
		{
			vm.CommitSoundSettings();
		}
	}
	//-----------------------------------------
	// Wi-Fi settings
	//-----------------------------------------
	private void WiFiIcon_Click(object sender, MouseButtonEventArgs e)
	{
		WiFiPopup.IsOpen = !WiFiPopup.IsOpen;
	}

	private void WiFiPopup_Closed(object sender, EventArgs e)
	{
		if (DataContext is MainViewModel vm)
		{
			vm.CommitWiFiSettings();
		}
	}
	private void WiFiPassword_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (DataContext is MainViewModel vm && sender is PasswordBox pb)
		{
			vm.WiFiPassword = SettingsStore.ProtectString(pb.Password);
		}
	}

	//-----------------------------------------
	// SlappyBell device settings
	//-----------------------------------------
	private void DeviceSelectorIcon_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
		{
			DeviceSelectorPopup.IsOpen = true;
			vm.DeviceSelectorOpen();
		}
	}
	private void DeviceSelectorPopup_Closed(object sender, EventArgs e)
	{
		if (DataContext is MainViewModel vm)
		{
			vm.DeviceSelectorClose();
		}
	}
	private async void DeviceSelectorItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Grid grid && grid.DataContext is DeviceInfo device)
		{
			if (DataContext is MainViewModel vm)
			{
				if (vm.IsSlappyBellConnecting)
					return;
				await vm.ConnectToSlappyBell(device);
			}
		}
	}
	//-----------------------------------------
	// Notification settings
	//-----------------------------------------

	private void SlotIcon0_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(0);
		e.Handled = true;
	}

	private void SlotIcon1_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(1);
		e.Handled = true;
	}

	private void SlotIcon2_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(2);
		e.Handled = true;
	}

	private void SlotIcon3_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(3);
		e.Handled = true;
	}

	private void SlotIcon4_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(4);
		e.Handled = true;
	}

	private void SlotIcon5_Click(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
			vm.OpenNotifySettingsCommand.Execute(5);
		e.Handled = true;
	}

	private void ShowNotifyOverlay()
	{
		NotifyOverlay.Visibility = Visibility.Visible;
		((Storyboard)Resources["ShowNotifyDrawer"]).Begin();
	}

	private void HideNotifyOverlay()
	{
		var sb = (Storyboard)Resources["HideNotifyDrawer"];
		sb.Completed += (_, __) => NotifyOverlay.Visibility = Visibility.Collapsed;
		sb.Begin();
	}

	private void NotifyOverlay_BackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		if (DataContext is MainViewModel vm)
		{
			if (vm.NotifySettings.IsUploading)
				return;
			HideNotifyOverlay();
		}
	}

	private void NotifyDrawer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		e.Handled = true;
	}
}