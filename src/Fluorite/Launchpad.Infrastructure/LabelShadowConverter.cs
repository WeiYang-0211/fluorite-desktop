using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Launchpad.Infrastructure;

public sealed class LabelShadowConverter : IMultiValueConverter
{
	private static readonly DropShadowEffect LightInk = Create(4.0, 1.0, 0.75);

	private static readonly DropShadowEffect DarkInk = Create(2.0, 0.5, 0.25);

	private static DropShadowEffect Create(double blur, double depth, double opacity)
	{
		DropShadowEffect dropShadowEffect = new DropShadowEffect();
		dropShadowEffect.Color = Colors.Black;
		dropShadowEffect.BlurRadius = blur;
		dropShadowEffect.ShadowDepth = depth;
		dropShadowEffect.Direction = 270.0;
		dropShadowEffect.Opacity = opacity;
		dropShadowEffect.RenderingBias = RenderingBias.Quality;
		dropShadowEffect.Freeze();
		return dropShadowEffect;
	}

	public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length >= 2)
		{
			object target = values[0];
			if (target is bool && (bool)target)
			{
				if (!(values[1] is SolidColorBrush { Color: var color }))
				{
					return null;
				}
				if (!((double)(int)color.R * 0.299 + (double)(int)color.G * 0.587 + (double)(int)color.B * 0.114 < 128.0))
				{
					return LightInk;
				}
				return DarkInk;
			}
		}
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
