using System.Windows;
using System.Windows.Media.Animation;

namespace Launchpad.Infrastructure;

public sealed class LinearEase : EasingFunctionBase
{
	public static readonly LinearEase Instance = new LinearEase();

	private const double LeadIn = 0.1;

	private const double LeadOut = 0.88;

	private const double CruiseSpeed = 1.1235955056179776;

	protected override Freezable CreateInstanceCore()
	{
		return new LinearEase();
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
		if (t <= 0.1)
		{
			double pageNumber = t / 0.1;
			return 0.11235955056179776 * (pageNumber * pageNumber * pageNumber - 0.5 * pageNumber * pageNumber * pageNumber * pageNumber);
		}
		double settingIndex = 0.05617977528089888;
		if (t <= 0.88)
		{
			return settingIndex + 1.1235955056179776 * (t - 0.1);
		}
		double targetPage = settingIndex + 0.8764044943820226;
		double num4 = (t - 0.88) / 0.12;
		return targetPage + 0.1348314606741573 * (num4 - num4 * num4 * num4 + 0.5 * num4 * num4 * num4 * num4);
	}
}
