using System;
using System.Globalization;
using System.Windows.Data;

namespace SlappyHub.Common;

public sealed class EnumEqualsToBoolConverter : IValueConverter
{
	// parameter: enum name (string) or enum value
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null || parameter is null) return false;

		var valueType = value.GetType();

		if (!valueType.IsEnum) return false;

		object? target = parameter;

		if (parameter is string name)
		{
			try
			{
				target = Enum.Parse(valueType, name, ignoreCase: true);
			}
			catch
			{
				return false;
			}
		}
		else if (parameter.GetType() != valueType)
		{
			return false;
		}

		return Equals(value, target);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		// RadioButtonのIsChecked=true のときに enum を返す
		if (value is true && parameter is not null)
		{
			if (targetType.IsEnum && parameter is string name)
				return Enum.Parse(targetType, name, ignoreCase: true);

			if (targetType.IsEnum && parameter.GetType() == targetType)
				return parameter;
		}

		return Binding.DoNothing;
	}
}
