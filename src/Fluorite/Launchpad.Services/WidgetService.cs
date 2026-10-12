using System;
using System.Globalization;
using System.Windows.Threading;

namespace Launchpad.Services;

public class WidgetService : IWidgetService
{
	private readonly ISystemMonitorService _monitorService;

	private readonly DispatcherTimer _timer;

	private static readonly ChineseLunisolarCalendar _lunarCalendar = new ChineseLunisolarCalendar();

	private static readonly string[] _chineseMonthNames = new string[13]
	{
		"", "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月",
		"十月", "冬月", "腊月"
	};

	private static readonly string[] _chineseDayNames = new string[31]
	{
		"", "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九",
		"初十", "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九",
		"二十", "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九",
		"三十"
	};

	public string ClockTime { get; private set; } = "--:--";

	public string ClockSeconds { get; private set; } = "00";

	public string ClockDate { get; private set; } = string.Empty;

	public string ClockFullDate { get; private set; } = string.Empty;

	public string ClockDayOfWeek { get; private set; } = string.Empty;

	public string ClockLunar { get; private set; } = string.Empty;

	public string ClockGreeting { get; private set; } = string.Empty;

	public double CpuPercent => _monitorService.CpuPercent;

	public double MemoryPercent => _monitorService.MemoryPercent;

	public double TotalMemoryGb => _monitorService.TotalMemoryGb;

	public double UsedMemoryGb => _monitorService.UsedMemoryGb;

	public string MemoryText => _monitorService.MemoryText;

	public event Action? Updated;

	public WidgetService(ISystemMonitorService monitorService)
	{
		_monitorService = monitorService;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_timer.Tick += (object? s, EventArgs e) =>
		{
			RefreshNow();
		};
		RefreshNow();
	}

	public void SetActive(bool active)
	{
		if (active)
		{
			if (!_timer.IsEnabled)
			{
				RefreshNow();
				_timer.Start();
			}
		}
		else
		{
			_timer.Stop();
		}
	}

	public void RefreshNow()
	{
		DateTime now = DateTime.Now;
		ClockTime = now.ToString("HH:mm");
		ClockSeconds = now.ToString("ss");
		ClockDate = now.ToString("M月d日");
		ClockFullDate = now.ToString("yyyy年M月d日");
		ClockDayOfWeek = GetChineseDayOfWeek(now);
		ClockLunar = GetChineseLunarDate(now);
		ClockGreeting = GetGreeting(now);
		_monitorService.Sample();
		Updated?.Invoke();
	}

	private static string GetChineseDayOfWeek(DateTime dt)
	{
		return dt.DayOfWeek switch
		{
			DayOfWeek.Monday => "星期一", 
			DayOfWeek.Tuesday => "星期二", 
			DayOfWeek.Wednesday => "星期三", 
			DayOfWeek.Thursday => "星期四", 
			DayOfWeek.Friday => "星期五", 
			DayOfWeek.Saturday => "星期六", 
			DayOfWeek.Sunday => "星期日", 
			_ => string.Empty, 
		};
	}

	private static string GetGreeting(DateTime dt)
	{
		switch (dt.Hour)
		{
		case 5:
		case 6:
		case 7:
		case 8:
			return "清晨好，迎接崭新一天";
		case 9:
		case 10:
		case 11:
			return "上午好，专注当下";
		case 12:
		case 13:
			return "中午好，适度小憩";
		case 14:
		case 15:
		case 16:
		case 17:
			return "下午好，保持活力";
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
			return "晚上好，享受平静";
		default:
			return "夜深了，早点休息";
		}
	}

	private static string GetChineseLunarDate(DateTime dt)
	{
		try
		{
			int year = _lunarCalendar.GetYear(dt);
			int month = _lunarCalendar.GetMonth(dt);
			int dayOfMonth = _lunarCalendar.GetDayOfMonth(dt);
			int leapMonth = _lunarCalendar.GetLeapMonth(year);
			string text;
			if (leapMonth <= 0 || month < leapMonth)
			{
				text = _chineseMonthNames[month];
			}
			else
			{
				text = ((month != leapMonth) ? _chineseMonthNames[month - 1] : ("闰" + _chineseMonthNames[month - 1]));
			}
			string commandLinePath = ((dayOfMonth <= 30) ? _chineseDayNames[dayOfMonth] : $"{dayOfMonth}日");
			return "农历" + text + commandLinePath;
		}
		catch
		{
			return string.Empty;
		}
	}
}
