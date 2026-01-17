using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class GreaterThanZeroToVisibilityConverter : IValueConverter
{
	// parameter: "Hidden" で false時Hidden
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var useHidden = string.Equals(parameter as string, "Hidden", StringComparison.OrdinalIgnoreCase);

		try
		{
			var n = System.Convert.ToDouble(value, culture);
			if (n > 0) return Visibility.Visible;
		}
		catch
		{
			// ignore
		}

		return useHidden ? Visibility.Hidden : Visibility.Collapsed;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> Binding.DoNothing;
}
