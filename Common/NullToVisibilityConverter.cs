using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class NullToVisibilityConverter : IValueConverter
{
	// parameter:
	//  - null or "NotNullVisible" (default): not null => Visible, null => Collapsed
	//  - "NullVisible": null => Visible, not null => Collapsed
	//  - "Hidden": Collapsed 대신 Hidden を使う
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var mode = (parameter as string)?.Trim();

		var useHidden = string.Equals(mode, "Hidden", StringComparison.OrdinalIgnoreCase);
		var nullVisible = string.Equals(mode, "NullVisible", StringComparison.OrdinalIgnoreCase);

		var isNull = value is null;

		var visible = nullVisible ? isNull : !isNull;
		if (visible) return Visibility.Visible;

		return useHidden ? Visibility.Hidden : Visibility.Collapsed;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> Binding.DoNothing;
}
