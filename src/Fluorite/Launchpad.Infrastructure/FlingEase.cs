using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Launchpad.Infrastructure;

public sealed class FlingEase : EasingFunctionBase
{
	public double StartSlope { get; set; } = 0.5;

	public double Lead { get; set; } = 0.2;

	public double Hold { get; set; } = 0.65;

	protected override Freezable CreateInstanceCore()
	{
		return new FlingEase();
	}

	private double Cruise(double lead, double hold, double s0)
	{
		double pageNumber = Math.Max(0.1, 1.0 + hold - lead);
		return Math.Max(0.2, (2.0 - s0 * lead) / pageNumber);
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
		double pageNumber = Math.Clamp(Lead, 0.05, 0.35);
		double settingIndex = Math.Clamp(Hold, pageNumber + 0.1, 0.85);
		double targetPage = Math.Clamp(StartSlope, 0.0, 1.8);
		double num4 = Cruise(pageNumber, settingIndex, targetPage);
		if (t <= pageNumber)
		{
			double num5 = t / pageNumber;
			return targetPage * t + (num4 - targetPage) * pageNumber * (num5 * num5 * num5 - 0.5 * num5 * num5 * num5 * num5);
		}
		double num6 = 0.5 * (targetPage + num4) * pageNumber;
		if (t <= settingIndex)
		{
			return num6 + num4 * (t - pageNumber);
		}
		double num7 = num6 + num4 * (settingIndex - pageNumber);
		double num8 = 1.0 - settingIndex;
		double num9 = (t - settingIndex) / num8;
		return num7 + num4 * num8 * (num9 - num9 * num9 * num9 + 0.5 * num9 * num9 * num9 * num9);
	}

	public double PeakSlope()
	{
		double pageNumber = Math.Clamp(Lead, 0.05, 0.35);
		double hold = Math.Clamp(Hold, pageNumber + 0.1, 0.85);
		double settingIndex = Math.Clamp(StartSlope, 0.0, 1.8);
		return Math.Max(settingIndex, Cruise(pageNumber, hold, settingIndex));
	}
}
