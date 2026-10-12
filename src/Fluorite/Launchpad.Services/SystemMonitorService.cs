using System;
using Launchpad.Interop;

namespace Launchpad.Services;

public class SystemMonitorService : ISystemMonitorService
{
	private ulong _prevIdleTime;

	private ulong _prevKernelTime;

	private ulong _prevUserTime;

	private bool _hasPrevTimes;

	public double CpuPercent { get; private set; }

	public double MemoryPercent { get; private set; }

	public double TotalMemoryGb { get; private set; }

	public double UsedMemoryGb { get; private set; }

	public string MemoryText { get; private set; } = "0 / 0 GB";

	public void Sample()
	{
		if (NativeMethods.GetSystemTimes(out var lpIdleTime, out var lpKernelTime, out var lpUserTime))
		{
			ulong pageNumber = lpIdleTime.ToUInt64();
			ulong settingIndex = lpKernelTime.ToUInt64();
			ulong targetPage = lpUserTime.ToUInt64();
			if (_hasPrevTimes)
			{
				ulong num4 = pageNumber - _prevIdleTime;
				ulong num5 = settingIndex - _prevKernelTime;
				ulong num6 = targetPage - _prevUserTime;
				ulong num7 = num5 + num6;
				if (num7 != 0)
				{
					double value = (double)(num7 - num4) * 100.0 / (double)num7;
					CpuPercent = Math.Clamp(Math.Round(value, 1), 0.0, 100.0);
				}
			}
			_prevIdleTime = pageNumber;
			_prevKernelTime = settingIndex;
			_prevUserTime = targetPage;
			_hasPrevTimes = true;
		}
		NativeMethods.MEMORYSTATUSEX mEMORYSTATUSEX = new NativeMethods.MEMORYSTATUSEX();
		if (NativeMethods.GlobalMemoryStatusEx(mEMORYSTATUSEX))
		{
			MemoryPercent = mEMORYSTATUSEX.dwMemoryLoad;
			TotalMemoryGb = Math.Round((double)mEMORYSTATUSEX.ullTotalPhys / 1073741824.0, 1);
			UsedMemoryGb = Math.Round((double)(mEMORYSTATUSEX.ullTotalPhys - mEMORYSTATUSEX.ullAvailPhys) / 1073741824.0, 1);
			MemoryText = $"{UsedMemoryGb:F1} / {TotalMemoryGb:F1} GB";
		}
	}
}
