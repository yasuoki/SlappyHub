using System.Windows.Controls;
using System.Windows.Input;
using SlappyHub.ViewModels;

namespace SlappyHub.Views;

public partial class NotifySettingsView : UserControl
{
	public NotifySettingsView()
	{
		InitializeComponent();
	}

	private void SenderTokenInput_KeyDown(object sender, KeyEventArgs e)
	{
		if (DataContext is not NotifySettingsViewModel vm) return;

		if (e.Key == Key.Enter)
		{
			vm.CommitSenderToken();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.Back && string.IsNullOrWhiteSpace(vm.NewSenderTokenText))
		{
			vm.RemoveLastSenderToken();
			e.Handled = true;
		}
	}

	private void TextTokenInput_KeyDown(object sender, KeyEventArgs e)
	{
		if (DataContext is not NotifySettingsViewModel vm) return;

		if (e.Key == Key.Enter)
		{
			vm.CommitTextToken();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.Back && string.IsNullOrWhiteSpace(vm.NewTextTokenText))
		{
			vm.RemoveLastTextToken();
			e.Handled = true;
		}
	}
}
