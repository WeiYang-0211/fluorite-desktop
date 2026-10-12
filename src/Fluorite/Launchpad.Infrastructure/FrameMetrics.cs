using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Launchpad.Infrastructure;

public sealed class FrameMetrics
{
	private readonly List<double> _gaps = new List<double>();

	private readonly Stopwatch _watch = new Stopwatch();

	private string? _phase;

	private long? _lastTicks;

	private double? _firstFrameMs;

	private static readonly object WriteGate = new object();

	public void Begin(string phase)
	{
		End();
		if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AIRDECK_PERF_REPORT")))
		{
			_phase = phase;
			_gaps.Clear();
			_lastTicks = null;
			_firstFrameMs = null;
			_watch.Restart();
		}
	}

	public void Record(long renderingTicks)
	{
		if (_phase == null)
		{
			return;
		}
		double valueOrDefault = _firstFrameMs.GetValueOrDefault();
		if (!_firstFrameMs.HasValue)
		{
			valueOrDefault = _watch.Elapsed.TotalMilliseconds;
			_firstFrameMs = valueOrDefault;
		}
		long? lastTicks = _lastTicks;
		if (lastTicks.HasValue)
		{
			long valueOrDefault2 = lastTicks.GetValueOrDefault();
			if (renderingTicks > valueOrDefault2)
			{
				_gaps.Add((double)(renderingTicks - valueOrDefault2) / 10000.0);
			}
		}
		_lastTicks = renderingTicks;
	}

	public void End()
	{
		if (_phase == null)
		{
			return;
		}
		string phase = _phase;
		_phase = null;
		_watch.Stop();
		string environmentVariable = Environment.GetEnvironmentVariable("AIRDECK_PERF_REPORT");
		if (string.IsNullOrWhiteSpace(environmentVariable))
		{
			return;
		}
		double[] ordered = _gaps.Order().ToArray();
		using (Process process = Process.GetCurrentProcess())
		{
			var value = new
			{
				utc = DateTime.UtcNow,
				phase = phase,
				durationMs = _watch.Elapsed.TotalMilliseconds,
				firstFrameMs = _firstFrameMs,
				intervals = ordered.Length,
				p50Ms = Percentile(0.5),
				p95Ms = Percentile(0.95),
				p99Ms = Percentile(0.99),
				worstMs = Percentile(1.0),
				workingSetBytes = process.WorkingSet64,
				privateBytes = process.PrivateMemorySize64
			};
			try
			{
				lock (WriteGate)
				{
					File.AppendAllText(environmentVariable, JsonSerializer.Serialize(value) + Environment.NewLine);
				}
			}
			catch (Exception ex)
			{
				App.Log("[PERF] report failed: " + ex.Message);
			}
		}
		double Percentile(double p)
		{
			if (ordered.Length != 0)
			{
				return ordered[(int)Math.Ceiling(p * (double)(ordered.Length - 1))];
			}
			return 0.0;
		}
	}
}
