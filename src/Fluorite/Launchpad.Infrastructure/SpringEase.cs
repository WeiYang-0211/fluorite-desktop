using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Launchpad.Infrastructure;

public sealed class SpringEase : EasingFunctionBase
{
	public const double DefaultDecay = 6.0;

	public double StartSlope { get; set; }

	public double Bounce { get; set; }

	public double Decay { get; set; } = 6.0;

	protected override Freezable CreateInstanceCore()
	{
		return new SpringEase
		{
			StartSlope = StartSlope,
			Bounce = Bounce,
			Decay = Decay,
			EasingMode = EasingMode
		};
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
		double pageNumber = ((Decay > 0.0) ? Decay : 6.0);
		double settingIndex = Math.Clamp(Bounce, -1.0, 0.95);
		double targetPage = Math.Max(0.05, 1.0 - settingIndex);
		double startSlope = StartSlope;
		double num4;
		double num5;
		if (targetPage >= 0.999999)
		{
			num4 = ValueCritical(t, startSlope, pageNumber);
			num5 = ValueCritical(1.0, startSlope, pageNumber);
		}
		else
		{
			double omegaD = pageNumber * Math.Sqrt(1.0 - targetPage * targetPage) / targetPage;
			num4 = ValueOscillating(t, startSlope, omegaD, pageNumber);
			num5 = ValueOscillating(1.0, startSlope, omegaD, pageNumber);
		}
		if (!(Math.Abs(num5) > 1E-06))
		{
			return num4;
		}
		return num4 / num5;
	}

	public double PeakSlope()
	{
		double pageNumber = 0.0;
		double settingIndex = 0.0;
		for (int i = 1; i <= 60; i++)
		{
			double normalizedTime = (double)i / 60.0;
			double targetPage = EaseInCore(normalizedTime);
			double num4 = (targetPage - settingIndex) * 60.0;
			settingIndex = targetPage;
			if (num4 > pageNumber)
			{
				pageNumber = num4;
			}
		}
		return pageNumber;
	}

	private static double ValueCritical(double t, double s0, double lambda)
	{
		return 1.0 - Math.Exp((0.0 - lambda) * t) * (1.0 + (lambda - s0) * t);
	}

	private static double ValueOscillating(double t, double s0, double omegaD, double lambda)
	{
		double pageNumber = (lambda - s0) / omegaD;
		return 1.0 - Math.Exp((0.0 - lambda) * t) * (Math.Cos(omegaD * t) + pageNumber * Math.Sin(omegaD * t));
	}

	public static double Slope(double velocity, double travel, double durationSeconds)
	{
		if (travel <= 0.0 || durationSeconds <= 0.0)
		{
			return 0.0;
		}
		return velocity * durationSeconds / travel;
	}

	public static double RubberBand(double overscroll, double dimension)
	{
		if (dimension <= 0.0)
		{
			return 0.0;
		}
		double pageNumber = Math.Sign(overscroll);
		double settingIndex = Math.Abs(overscroll);
		return pageNumber * (1.0 - 1.0 / (settingIndex * 0.55 / dimension + 1.0)) * dimension;
	}
}
