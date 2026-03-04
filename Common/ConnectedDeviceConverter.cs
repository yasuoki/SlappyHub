using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace SlappyHub.Common;

public class ConnectedDeviceConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType,
		object parameter, CultureInfo culture)
	{
		if (values.Length != 2)
			return false;

		var deviceAddress = values[0] as string;
		var connectedAddress = values[1] as string;

		if( deviceAddress != null &&
		       connectedAddress != null &&
		       deviceAddress == connectedAddress)
			return new SolidColorBrush(Color.FromRgb(79, 195, 247));
		else				
			return new SolidColorBrush(Color.FromRgb(120, 120, 120));
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}