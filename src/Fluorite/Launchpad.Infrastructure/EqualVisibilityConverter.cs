using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launchpad.Infrastructure;

public sealed class EqualVisibilityConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return (!string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
