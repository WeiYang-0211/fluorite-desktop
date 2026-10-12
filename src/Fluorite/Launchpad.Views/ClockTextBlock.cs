using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launchpad.Views;

public sealed class ClockTextBlock : TextBlock
{
	static ClockTextBlock()
	{
		TextBlock.ForegroundProperty.OverrideMetadata(typeof(ClockTextBlock), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits, null, CoerceInk));
	}

	private static object CoerceInk(DependencyObject d, object value)
	{
		string color = ClockInk.GetColor(d);
		if (!string.IsNullOrWhiteSpace(color))
		{
			try
			{
				return (Brush)new BrushConverter().ConvertFromString(color);
			}
			catch
			{
			}
		}
		return value;
	}
}
