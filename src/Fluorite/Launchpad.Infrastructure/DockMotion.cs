using System;

namespace Launchpad.Infrastructure;

internal static class DockMotion
{
	public const double Range = 2.4;

	public static double Bump(double distance)
	{
		distance = Math.Abs(distance);
		if (!(distance >= 2.4))
		{
			return Math.Cos(distance / 2.4 * Math.PI / 2.0);
		}
		return 0.0;
	}

	public static double Normalize(double rawSum, int count, double grow)
	{
		if (rawSum <= 0.0005 || grow <= 0.0005)
		{
			return 1.0;
		}
		double pageNumber = 0.0;
		int settingIndex = Math.Min(Math.Max(0, count - 1), (int)Math.Ceiling(2.4));
		for (int i = -settingIndex; i <= settingIndex; i++)
		{
			pageNumber += Bump(i);
		}
		return Math.Clamp(pageNumber * grow / rawSum, 1.0, 1.04);
	}

	public static double Follow(double current, double target, ref double velocity, double dt, double smoothTime)
	{
		if (smoothTime <= 0.0001)
		{
			velocity = 0.0;
			return target;
		}
		double pageNumber = 2.0 / smoothTime;
		double settingIndex = pageNumber * dt;
		double targetPage = 1.0 / (1.0 + settingIndex + 0.48 * settingIndex * settingIndex + 0.235 * settingIndex * settingIndex * settingIndex);
		double num4 = current - target;
		if (num4 == 0.0)
		{
			velocity = 0.0;
			return target;
		}
		double num5 = (velocity + pageNumber * num4) * dt;
		velocity = (velocity - pageNumber * num5) * targetPage;
		double num6 = target + (num4 + num5) * targetPage;
		bool num7;
		if (!(num4 > 0.0))
		{
			if (!(num4 < 0.0))
			{
				goto IL_00d2;
			}
			num7 = num6 > target;
		}
		else
		{
			num7 = num6 < target;
		}
		if (num7)
		{
			num6 = target;
			velocity = 0.0;
		}
		goto IL_00d2;
		IL_00d2:
		if (Math.Abs(num6 - target) < 0.0001 && Math.Abs(velocity) < 0.001)
		{
			num6 = target;
			velocity = 0.0;
		}
		return num6;
	}
}
