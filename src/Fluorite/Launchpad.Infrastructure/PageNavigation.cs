using System;

namespace Launchpad.Infrastructure;

public static class PageNavigation
{
	public static int StepTarget(int current, int inFlight, int queued, int direction, int total)
	{
		return Math.Clamp(((queued >= 1) ? queued : ((inFlight >= 1) ? inFlight : current)) + Math.Sign(direction), 1, Math.Max(1, total));
	}
}
