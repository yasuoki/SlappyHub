using System.Windows;
using System.Windows.Controls;
using SlappyHub.ViewModels;

namespace SlappyHub.Views;

public partial class SlackSettingsView : System.Windows.Controls.UserControl
{
	public SlackSettingsView()
	{
		InitializeComponent();
	}
	private void MasterPassword_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (DataContext is SlackSettingsViewModel vm && sender is PasswordBox pb)
			vm.MasterPassword = pb.Password;
	}

	private void SlavePassword_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (DataContext is SlackSettingsViewModel vm && sender is PasswordBox pb)
			vm.MasterNodePassword = pb.Password;
	}
}