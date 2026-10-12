using System;

namespace Launchpad.Services;

public interface IWidgetService
{
	string ClockTime { get; }

	string ClockSeconds { get; }

	string ClockDate { get; }

	string ClockFullDate { get; }

	string ClockDayOfWeek { get; }

	string ClockLunar { get; }

	string ClockGreeting { get; }

	double CpuPercent { get; }

	double MemoryPercent { get; }

	double TotalMemoryGb { get; }

	double UsedMemoryGb { get; }

	string MemoryText { get; }

	event Action? Updated;

	void SetActive(bool active);

	void RefreshNow();
}
