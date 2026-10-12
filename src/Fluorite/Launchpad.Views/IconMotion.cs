using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Launchpad.Infrastructure;
using Launchpad.Models;
using Launchpad.ViewModels;

namespace Launchpad.Views;

public static class IconMotion
{
	private sealed class State : IDisposable
	{
		private readonly Border _icon;

		private readonly AppSettings _settings;

		private readonly ScaleTransform _scale = new ScaleTransform();

		private readonly SkewTransform _skew = new SkewTransform();

		private readonly RotateTransform _rotate = new RotateTransform();

		private readonly TranslateTransform _shift = new TranslateTransform();

		private readonly Border _light;

		private readonly RadialGradientBrush _shine = new RadialGradientBrush(Color.FromArgb(90, byte.MaxValue, byte.MaxValue, byte.MaxValue), Colors.Transparent);

		private UIElement _inputOwner;

		private bool _pressed;

		private bool _dragged;

		private Point _down;

		private bool Blocked
		{
			get
			{
				if (!_settings.ReduceMotion && _icon.IsVisible)
				{
					if (Window.GetWindow(_icon)?.DataContext is MainViewModel mainViewModel)
					{
						if (!mainViewModel.IsItemDragActive)
						{
							return mainViewModel.IsPageSwiping;
						}
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public State(Border icon, AppSettings settings)
		{
			_icon = icon;
			_inputOwner = icon;
			_settings = settings;
			TransformGroup renderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)_scale,
					(Transform)_skew,
					(Transform)_rotate,
					(Transform)_shift
				}
			};
			icon.RenderTransformOrigin = new Point(0.5, 0.5);
			icon.RenderTransform = renderTransform;
			_light = new Border
			{
				Background = _shine,
				CornerRadius = icon.CornerRadius,
				IsHitTestVisible = false,
				Opacity = 0.0
			};
			if (icon.Child is Grid grid)
			{
				Panel.SetZIndex(_light, 50);
				grid.Children.Add(_light);
			}
			icon.MouseMove += Move;
			icon.MouseLeave += Leave;
			icon.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Down), handledEventsToo: true);
			icon.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Up), handledEventsToo: true);
			icon.IsVisibleChanged += VisibilityChanged;
			icon.Loaded += Loaded;
			icon.Unloaded += Unloaded;
			if (icon.IsLoaded)
			{
				settings.PropertyChanged += SettingsChanged;
			}
		}

		private void Loaded(object sender, RoutedEventArgs e)
		{
			_settings.PropertyChanged -= SettingsChanged;
			_settings.PropertyChanged += SettingsChanged;
			DependencyObject dependencyObject = _icon;
			while (dependencyObject != null && !(dependencyObject is Button))
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			if (dependencyObject is Button button && _inputOwner != button)
			{
				_inputOwner.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Down));
				_inputOwner.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Up));
				_inputOwner = button;
				button.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Down), handledEventsToo: true);
				button.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Up), handledEventsToo: true);
			}
		}

		private void Unloaded(object sender, RoutedEventArgs e)
		{
			_settings.PropertyChanged -= SettingsChanged;
			Reset();
		}

		private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
		{
			bool validated;
			switch (e.PropertyName)
			{
			case "ReduceMotion":
			case "EnableIconTilt":
			case "EnableClickSpring":
				validated = true;
				break;
			default:
				validated = false;
				break;
			}
			if (validated)
			{
				Reset();
			}
		}

		private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (!(bool)e.NewValue)
			{
				Reset();
			}
		}

		private void Move(object sender, MouseEventArgs e)
		{
			Point position = e.GetPosition(_icon);
			if (_pressed && (position - _down).Length > 6.0)
			{
				_dragged = true;
				ResetVisual();
			}
			if (!Blocked && !_dragged && e.LeftButton != MouseButtonState.Pressed && _settings.EnableIconTilt)
			{
				double pageNumber = Math.Clamp(position.X / Math.Max(1.0, _icon.ActualWidth) * 2.0 - 1.0, -1.0, 1.0);
				double settingIndex = Math.Clamp(position.Y / Math.Max(1.0, _icon.ActualHeight) * 2.0 - 1.0, -1.0, 1.0);
				_skew.BeginAnimation(SkewTransform.AngleXProperty, null);
				_skew.BeginAnimation(SkewTransform.AngleYProperty, null);
				_rotate.BeginAnimation(RotateTransform.AngleProperty, null);
				_shift.BeginAnimation(TranslateTransform.XProperty, null);
				_shift.BeginAnimation(TranslateTransform.YProperty, null);
				_skew.AngleX = (0.0 - settingIndex) * 3.0;
				_skew.AngleY = pageNumber * 2.0;
				_rotate.Angle = pageNumber * 1.5;
				_shift.X = pageNumber * 2.0;
				_shift.Y = settingIndex * 2.0;
				RadialGradientBrush shine = _shine;
				Point center = (_shine.GradientOrigin = new Point((pageNumber + 1.0) / 2.0, (settingIndex + 1.0) / 2.0));
				shine.Center = center;
				_light.BeginAnimation(UIElement.OpacityProperty, null);
				_light.Opacity = 0.7;
			}
		}

		private void Down(object sender, MouseButtonEventArgs e)
		{
			if (!Blocked && _settings.EnableClickSpring)
			{
				_pressed = true;
				_dragged = false;
				_down = e.GetPosition(_icon);
				AnimateScale(0.93, 85, spring: false);
			}
		}

		private void Up(object sender, MouseButtonEventArgs e)
		{
			if (_pressed)
			{
				bool validated = !_dragged && !Blocked && _inputOwner.IsMouseOver;
				_pressed = false;
				_dragged = false;
				AnimateScale(1.0, validated ? 260 : 120, validated);
				if (validated)
				{
					_light.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.45, 0.0, TimeSpan.FromMilliseconds(220.0)));
				}
			}
		}

		private void AnimateScale(double value, int ms, bool spring)
		{
			DoubleAnimation animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(ms))
			{
				EasingFunction = new SpringEase
				{
					Bounce = (spring ? 0.2 : 0.0)
				}
			};
			_scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			_scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}

		private void Leave(object sender, MouseEventArgs e)
		{
			_pressed = (_dragged = false);
			if (Blocked)
			{
				ResetVisual();
				return;
			}
			AnimateScale(1.0, 140, spring: false);
			(Animatable, DependencyProperty)[] entries = new (Animatable, DependencyProperty)[5]
			{
				(_skew, SkewTransform.AngleXProperty),
				(_skew, SkewTransform.AngleYProperty),
				(_rotate, RotateTransform.AngleProperty),
				(_shift, TranslateTransform.XProperty),
				(_shift, TranslateTransform.YProperty)
			};
			for (int i = 0; i < entries.Length; i++)
			{
				var (animatable, dp) = entries[i];
				animatable.BeginAnimation(dp, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
			_light.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140.0)));
		}

		private void ResetVisual()
		{
			_scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			_scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			_skew.BeginAnimation(SkewTransform.AngleXProperty, null);
			_skew.BeginAnimation(SkewTransform.AngleYProperty, null);
			_rotate.BeginAnimation(RotateTransform.AngleProperty, null);
			_shift.BeginAnimation(TranslateTransform.XProperty, null);
			_shift.BeginAnimation(TranslateTransform.YProperty, null);
			ScaleTransform scale = _scale;
			double scaleX = (_scale.ScaleY = 1.0);
			scale.ScaleX = scaleX;
			SkewTransform skew = _skew;
			SkewTransform skew2 = _skew;
			RotateTransform rotate = _rotate;
			TranslateTransform shift = _shift;
			double settingIndex = (_shift.Y = 0.0);
			double num4 = (shift.X = settingIndex);
			double num6 = (rotate.Angle = num4);
			scaleX = (skew2.AngleY = num6);
			skew.AngleX = scaleX;
			_light.BeginAnimation(UIElement.OpacityProperty, null);
			_light.Opacity = 0.0;
		}

		private void Reset()
		{
			_pressed = (_dragged = false);
			ResetVisual();
		}

		public void Dispose()
		{
			Reset();
			_settings.PropertyChanged -= SettingsChanged;
			_icon.MouseMove -= Move;
			_icon.MouseLeave -= Leave;
			_icon.IsVisibleChanged -= VisibilityChanged;
			_icon.Loaded -= Loaded;
			_icon.Unloaded -= Unloaded;
			_inputOwner.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Down));
			_inputOwner.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Up));
			if (_icon.Child is Grid grid)
			{
				grid.Children.Remove(_light);
			}
		}
	}

	public static readonly DependencyProperty SettingsProperty = DependencyProperty.RegisterAttached("Settings", typeof(AppSettings), typeof(IconMotion), new PropertyMetadata(null, Changed));

	private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(State), typeof(IconMotion));

	public static void SetSettings(DependencyObject target, AppSettings value)
	{
		target.SetValue(SettingsProperty, value);
	}

	public static AppSettings? GetSettings(DependencyObject target)
	{
		return (AppSettings)target.GetValue(SettingsProperty);
	}

	private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
	{
		if (target is Border border)
		{
			(border.GetValue(StateProperty) as State)?.Dispose();
			border.SetValue(StateProperty, (args.NewValue is AppSettings settings) ? new State(border, settings) : null);
		}
	}
}
