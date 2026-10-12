using System;

namespace Launchpad.Infrastructure;

internal sealed class WheelPageGesture
{
	private const long QuietTicks = 2500000L;

	private long _lastPacket;

	private long _lastStep;

	private bool _active;

	private bool _stepped;

	private int _sum;

	private bool _horizontal;

	public bool TryStep(int delta, bool horizontal, long now)
	{
		if (delta == 0)
		{
			return false;
		}
		if (_active && now - _lastPacket >= 2500000)
		{
			_active = false;
		}
		if (!_active && !horizontal && Math.Abs((long)delta) >= 120 && delta % 120 == 0)
		{
			if (_lastStep != 0L && now - _lastStep <= 1300000)
			{
				return false;
			}
			_lastStep = now;
			return true;
		}
		if (!_active)
		{
			_active = true;
			_stepped = false;
			_sum = 0;
			_horizontal = horizontal;
		}
		_lastPacket = now;
		if (_stepped || horizontal != _horizontal)
		{
			return false;
		}
		_sum = Math.Clamp(_sum + delta, -1000, 1000);
		if (Math.Abs(_sum) <= (horizontal ? 15 : 20))
		{
			return false;
		}
		_stepped = true;
		_lastStep = now;
		return true;
	}
}
