using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
	// parameter:
	//  - null or "NotEmptyVisible" (default): not empty => Visible, empty => Collapsed
	//  - "EmptyVisible": empty => Visible, not empty => Collapsed
	//  - "Hidden": Collapsed 대신 Hidden
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var s = value as string;
		var empty = string.IsNullOrWhiteSpace(s);

		var mode = (parameter as string)?.Trim();
		var useHidden = string.Equals(mode, "Hidden", StringComparison.OrdinalIgnoreCase);
		var emptyVisible = string.Equals(mode, "EmptyVisible", StringComparison.OrdinalIgnoreCase);

		var visible = emptyVisible ? empty : !empty;
		if (visible) return Visibility.Visible;

		return useHidden ? Visibility.Hidden : Visibility.Collapsed;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> Binding.DoNothing;
}