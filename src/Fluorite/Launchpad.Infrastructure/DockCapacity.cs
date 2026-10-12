using System;

namespace Launchpad.Infrastructure;

public static class DockCapacity
{
	public static int ForWidth(double width, double iconSize, double magnification, double spacing = 15.0)
	{
		if (!double.IsFinite(width) || width <= 0.0 || !double.IsFinite(iconSize) || iconSize <= 0.0)
		{
			return 0;
		}
		spacing = (double.IsFinite(spacing) ? Math.Clamp(spacing, 0.0, 40.0) : 15.0);
		double pageNumber = iconSize + spacing;
		double settingIndex = 3.3 * pageNumber * Math.Max(0.0, magnification - 1.0);
		return Math.Max(0, (int)Math.Floor((width - 32.0 - 32.0 - settingIndex + spacing) / pageNumber));
	}
}
