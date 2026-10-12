using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launchpad.Infrastructure;

namespace Launchpad.Models;

public class PageIndicatorItem : ObservableObject
{
	[ObservableProperty]
	private int _pageIndex;

	[ObservableProperty]
	private bool _isSelected;

	[ObservableProperty]
	private double _indicatorWidth = 6.0;

	[ObservableProperty]
	private double _indicatorOpacity = 0.35;

	private DispatcherTimer? _tween;

	private Stopwatch? _tweenSw;

	private double _tweenFromWidth;

	private double _tweenToWidth;

	private double _tweenFromOpacity;

	private double _tweenToOpacity;

	private double _tweenDurationMs = 200.0;

	private readonly SpringEase _tweenEase = new SpringEase
	{
		Bounce = 0.18
	};

	public int PageIndex
	{
		get
		{
			return _pageIndex;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_pageIndex, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageIndex);
				_pageIndex = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageIndex);
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isSelected, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSelected);
				_isSelected = value;
				OnIsSelectedChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSelected);
			}
		}
	}

	public double IndicatorWidth
	{
		get
		{
			return _indicatorWidth;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_indicatorWidth, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IndicatorWidth);
				_indicatorWidth = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IndicatorWidth);
			}
		}
	}

	public double IndicatorOpacity
	{
		get
		{
			return _indicatorOpacity;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_indicatorOpacity, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IndicatorOpacity);
				_indicatorOpacity = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IndicatorOpacity);
			}
		}
	}

	public void SetLive(double width, double opacity)
	{
		_tween?.Stop();
		IndicatorWidth = width;
		IndicatorOpacity = opacity;
	}

	public void TweenTo(double width, double opacity, double durationMs)
	{
		if (!(Math.Abs(IndicatorWidth - width) < 0.05) || !(Math.Abs(IndicatorOpacity - opacity) < 0.005))
		{
			_tweenFromWidth = IndicatorWidth;
			_tweenToWidth = width;
			_tweenFromOpacity = IndicatorOpacity;
			_tweenToOpacity = opacity;
			_tweenDurationMs = Math.Max(1.0, durationMs);
			if (_tween == null)
			{
				_tweenSw = new Stopwatch();
				_tween = new DispatcherTimer(DispatcherPriority.Render)
				{
					Interval = TimeSpan.FromMilliseconds(8.0)
				};
				_tween.Tick += OnTweenTick;
			}
			_tweenSw.Restart();
			_tween.Stop();
			_tween.Start();
		}
	}

	private void OnTweenTick(object? sender, EventArgs e)
	{
		double num = ((_tweenSw != null) ? (_tweenSw.Elapsed.TotalMilliseconds / _tweenDurationMs) : 1.0);
		if (num >= 1.0)
		{
			_tween?.Stop();
			SetLive(_tweenToWidth, _tweenToOpacity);
		}
		else
		{
			double num2 = _tweenEase.Ease(Math.Clamp(num, 0.0, 1.0));
			IndicatorWidth = _tweenFromWidth + (_tweenToWidth - _tweenFromWidth) * num2;
			IndicatorOpacity = _tweenFromOpacity + (_tweenToOpacity - _tweenFromOpacity) * num2;
		}
	}

	private void OnIsSelectedChanged(bool value)
	{
		TweenTo(value ? 14.0 : 6.0, value ? 0.95 : 0.35, 200.0);
	}
}
