using System;
using System.Globalization;
using System.Windows.Data;

namespace Launchpad.Infrastructure;

public sealed class InverseScaleConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return (value is double pageNumber && pageNumber > 0.0) ? (1.0 / pageNumber) : 1.0;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
