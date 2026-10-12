using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using Launchpad.Interop;

namespace Launchpad.Services;

public sealed class RunningAppsMonitor : IDisposable
{
	private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(1500.0);

	private static readonly int[] LaunchRetries = new int[4] { 250, 700, 1500, 3000 };

	private readonly Dispatcher _dispatcher;

	private Timer? _pulse;

	private readonly List<Timer> _retries = new List<Timer>();

	private int _inFlight;

	private HashSet<string> _reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public event Action<HashSet<string>>? Changed;

	public RunningAppsMonitor(Dispatcher dispatcher)
	{
		_dispatcher = dispatcher;
	}

	public void SetPolling(bool on)
	{
		if (on)
		{
			if (_pulse == null)
			{
				_pulse = new Timer((object? _) =>
				{
					Sample();
				}, null, TimeSpan.Zero, PollEvery);
			}
		}
		else
		{
			_pulse?.Dispose();
			_pulse = null;
		}
	}

	public void RefreshSoon()
	{
		int[] launchRetries = LaunchRetries;
		foreach (int delay in launchRetries)
		{
			ArmRetry(delay);
		}
	}

	private void ArmRetry(int delay)
	{
		Timer timer = null;
		timer = new Timer((object? _) =>
		{
			Sample();
			if (timer != null)
			{
				lock (_retries)
				{
					_retries.Remove(timer);
				}
				timer.Dispose();
			}
		}, null, delay, -1);
		lock (_retries)
		{
			_retries.Add(timer);
		}
	}

	private void Sample()
	{
		if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
		{
			return;
		}
		try
		{
			HashSet<string> names = ProcessSnapshot.CaptureNames();
			if (!names.SetEquals(_reported))
			{
				DescribeChange(_reported, names);
				_reported = names;
				_dispatcher.BeginInvoke((Action)(() =>
				{
					Changed?.Invoke(names);
				}), DispatcherPriority.Background);
			}
		}
		catch (Exception ex)
		{
			App.Log("[RUN] sample failed: " + ex.Message);
		}
		finally
		{
			Interlocked.Exchange(ref _inFlight, 0);
		}
	}

	private static void DescribeChange(HashSet<string> before, HashSet<string> after)
	{
		List<string> list = new List<string>(after);
		list.RemoveAll(before.Contains);
		List<string> list2 = new List<string>(before);
		list2.RemoveAll(after.Contains);
		App.Log($"[RUN] n={after.Count} +[{Brief(list)}] -[{Brief(list2)}]");
	}

	private static string Brief(List<string> names, int atMost = 10)
	{
		if (names.Count != 0)
		{
			if (names.Count > atMost)
			{
				return string.Join(",", names.GetRange(0, atMost)) + $",…{names.Count - atMost}";
			}
			return string.Join(",", names);
		}
		return "-";
	}

	public void Dispose()
	{
		SetPolling(on: false);
		lock (_retries)
		{
			foreach (Timer retry in _retries)
			{
				retry.Dispose();
			}
			_retries.Clear();
		}
	}
}
