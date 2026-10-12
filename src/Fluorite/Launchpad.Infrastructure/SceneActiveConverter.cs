using System;
using System.Globalization;
using System.Windows.Data;

namespace Launchpad.Infrastructure;

public sealed class SceneActiveConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		return values.Length == 2 && values[0] is string text && !string.IsNullOrEmpty(text) && values[1] is string commandLinePath && text == commandLinePath;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
