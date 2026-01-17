using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
	// parameter:
	//  - "EnumName"             -> equals => Visible
	//  - "EnumName|Hidden"      -> not equals => Hidden
	//  - "EnumName|Invert"      -> equals => Collapsed, not equals => Visible
	//  - "EnumName|Invert|Hidden"
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null || parameter is null) return Visibility.Collapsed;

		var valueType = value.GetType();
		if (!valueType.IsEnum) return Visibility.Collapsed;

		var paramText = parameter as string;
		if (paramText is null) return Visibility.Collapsed;

		var parts = paramText.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0) return Visibility.Collapsed;

		var enumName = parts[0];
		var invert = parts.Any(x => string.Equals(x, "Invert", StringComparison.OrdinalIgnoreCase));
		var useHidden = parts.Any(x => string.Equals(x, "Hidden", StringComparison.OrdinalIgnoreCase));

		object target;
		try
		{
			target = Enum.Parse(valueType, enumName, ignoreCase: true);
		}
		catch
		{
			return Visibility.Collapsed;
		}

		var equals = Equals(value, target);
		var visible = invert ? !equals : equals;

		if (visible) return Visibility.Visible;
		return useHidden ? Visibility.Hidden : Visibility.Collapsed;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> Binding.DoNothing;
}