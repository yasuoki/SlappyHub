using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class BoolToVisibilityConverterEx : IValueConverter
{
	// parameter:
	//  - null or "Collapsed" -> false => Collapsed
	//  - "Hidden"           -> false => Hidden
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var b = value is bool bb && bb;
		if (b) return Visibility.Visible;

		var mode = (parameter as string)?.Trim();
		return string.Equals(mode, "Hidden", StringComparison.OrdinalIgnoreCase)
			? Visibility.Hidden
			: Visibility.Collapsed;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is Visibility v && v == Visibility.Visible;
	}
}