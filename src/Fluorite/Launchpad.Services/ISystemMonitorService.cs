namespace Launchpad.Services;

public interface ISystemMonitorService
{
	double CpuPercent { get; }

	double MemoryPercent { get; }

	double TotalMemoryGb { get; }

	double UsedMemoryGb { get; }

	string MemoryText { get; }

	void Sample();
}
