using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Launchpad.Infrastructure;

public sealed class SettleEase : EasingFunctionBase
{
	public double StartSlope { get; set; } = 1.5;

	protected override Freezable CreateInstanceCore()
	{
		return new SettleEase();
	}

	protected override double EaseInCore(double t)
	{
		if (t <= 0.0)
		{
			return 0.0;
		}
		if (t >= 1.0)
		{
			return 1.0;
		}
		double pageNumber = Math.Clamp(StartSlope, 0.0, 2.0);
		double settingIndex = t * t * t;
		double targetPage = settingIndex * t;
		double num4 = targetPage * t;
		return (6.0 - 3.0 * pageNumber) * num4 + (8.0 * pageNumber - 15.0) * targetPage + (10.0 - 6.0 * pageNumber) * settingIndex + pageNumber * t;
	}
}
