using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Launchpad.Models;

namespace Launchpad.Views;

public sealed class AuroraBackdrop : UserControl
{
	public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register("Settings", typeof(AppSettings), typeof(AuroraBackdrop), new PropertyMetadata(null, Changed));

	private readonly Canvas _canvas = new Canvas
	{
		Width = 320.0,
		Height = 180.0,
		ClipToBounds = true,
		Background = new SolidColorBrush(Color.FromRgb(10, 17, 31))
	};

	private readonly List<TranslateTransform> _motions = new List<TranslateTransform>();

	private readonly List<Path> _reflections = new List<Path>();

	private readonly List<AnimationClock> _motionClocks = new List<AnimationClock>();

	private bool _motionSuspended;

	private readonly Viewbox _surface;

	private readonly BlurEffect _blur = new BlurEffect
	{
		RenderingBias = RenderingBias.Quality
	};

	private readonly Border _veil = new Border
	{
		Background = Brushes.Black
	};

	private readonly Grid _root;

	private readonly AuroraFinishEffect _finish = new AuroraFinishEffect();

	private static readonly Dictionary<Color, ImageBrush> GlowBrushes = new Dictionary<Color, ImageBrush>();

	private AppSettings? _watched;

	public AppSettings? Settings
	{
		get
		{
			return (AppSettings)GetValue(SettingsProperty);
		}
		set
		{
			SetValue(SettingsProperty, value);
		}
	}

	public bool IsPreview { get; set; }

	public bool IsAnimating { get; private set; }

	private double DetailScale => IsPreview ? 1 : 12;

	private static ImageBrush SmoothGlow(Color color)
	{
		if (GlowBrushes.TryGetValue(color, out ImageBrush value))
		{
			return value;
		}
		byte[] entries = new byte[4194304];
		for (int i = 0; i < 1024; i++)
		{
			for (int j = 0; j < 1024; j++)
			{
				double pageNumber = ((double)j + 0.5) / 1024.0 * 2.0 - 1.0;
				double settingIndex = ((double)i + 0.5) / 1024.0 * 2.0 - 1.0;
				double targetPage = Math.Max(0.0, 1.0 - Math.Sqrt(pageNumber * pageNumber + settingIndex * settingIndex));
				int num4 = (i * 1024 + j) * 4;
				uint num5 = (uint)((j + i * 1024) * 747796405 + -1403630843);
				num5 = ((num5 >> (int)((num5 >> 28) + 4)) ^ num5) * 277803737;
				num5 = (num5 >> 22) ^ num5;
				double num6 = (double)(num5 & 0xFFFF) / 65536.0;
				byte b = (byte)Math.Min(255, (int)(targetPage * 255.0 + num6));
				entries[num4] = (byte)Math.Min(b, (int)((double)(int)color.B * targetPage + num6));
				entries[num4 + 1] = (byte)Math.Min(b, (int)((double)(int)color.G * targetPage + num6));
				entries[num4 + 2] = (byte)Math.Min(b, (int)((double)(int)color.R * targetPage + num6));
				entries[num4 + 3] = b;
			}
		}
		BitmapSource bitmapSource = BitmapSource.Create(1024, 1024, 96.0, 96.0, PixelFormats.Pbgra32, null, entries, 4096);
		bitmapSource.Freeze();
		ImageBrush imageBrush = new ImageBrush(bitmapSource)
		{
			Stretch = Stretch.Fill
		};
		imageBrush.Freeze();
		GlowBrushes.Add(color, imageBrush);
		return imageBrush;
	}

	public void SetMotionSuspended(bool suspended)
	{
		_motionSuspended = suspended;
		ApplyMotionState();
	}

	private void ApplyMotionState()
	{
		bool validated = _motionSuspended || !IsVisible;
		foreach (AnimationClock motionClock in _motionClocks)
		{
			if (validated)
			{
				motionClock.Controller?.Pause();
			}
			else
			{
				motionClock.Controller?.Resume();
			}
		}
		IsAnimating = !validated && _motionClocks.Count > 0;
	}

	private void Animate(Animatable target, DependencyProperty property, DoubleAnimation animation)
	{
		AnimationClock animationClock = (AnimationClock)animation.CreateClock(hasControllableRoot: true);
		target.ApplyAnimationClock(property, animationClock);
		_motionClocks.Add(animationClock);
		if (_motionSuspended)
		{
			animationClock.Controller?.Pause();
		}
	}

	private void AnimateReflection(Path reflection, DoubleAnimation animation)
	{
		AnimationClock animationClock = (AnimationClock)animation.CreateClock(hasControllableRoot: true);
		reflection.ApplyAnimationClock(UIElement.OpacityProperty, animationClock);
		_motionClocks.Add(animationClock);
		if (_motionSuspended)
		{
			animationClock.Controller?.Pause();
		}
	}

	public AuroraBackdrop()
	{
		IsHitTestVisible = false;
		Grid grid = new Grid
		{
			ClipToBounds = true
		};
		Grid grid2 = grid;
		_root = grid;
		Grid grid3 = grid2;
		_surface = new Viewbox
		{
			Stretch = Stretch.Fill,
			Child = _canvas
		};
		RenderOptions.SetEdgeMode(_canvas, EdgeMode.Unspecified);
		grid3.Children.Add(_surface);
		grid3.Children.Add(_veil);
		Content = grid3;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			Attach();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			Detach();
		};
		IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			UpdateMotion();
		};
		SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			UpdateFinishSize();
		};
	}

	private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((AuroraBackdrop)d).Attach();
	}

	private void Attach()
	{
		Detach();
		_watched = Settings;
		if (IsLoaded && _watched != null)
		{
			_watched.PropertyChanged += SettingsChanged;
		}
		if (IsLoaded)
		{
			Build();
			UpdateVeil();
			UpdateBlur();
			UpdateMotion();
		}
	}

	private void Detach()
	{
		if (_watched != null)
		{
			_watched.PropertyChanged -= SettingsChanged;
		}
		_watched = null;
		Stop();
	}

	private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "AuroraPalette")
		{
			Build();
			UpdateMotion();
			return;
		}
		string propertyName = e.PropertyName;
		if ((propertyName == "ReduceMotion" || propertyName == "EnableAuroraBackground") ? true : false)
		{
			UpdateMotion();
		}
		else if (e.PropertyName == "ScrimPercent")
		{
			UpdateVeil();
		}
		else if (e.PropertyName == "BackdropBlur")
		{
			UpdateBlur();
		}
	}

	private void UpdateVeil()
	{
		_veil.Opacity = Math.Clamp((Settings?.ScrimPercent ?? 0.0) / 100.0, 0.0, 0.8);
	}

	private void UpdateBlur()
	{
		double pageNumber = (IsPreview ? 0.0 : Math.Clamp(Settings?.BackdropBlur ?? 0.0, 0.0, 40.0));
		_blur.Radius = pageNumber;
		_surface.Margin = new Thickness((0.0 - pageNumber) * 2.0);
		_surface.Effect = ((pageNumber > 0.0) ? _blur : null);
		_root.Effect = ((pageNumber > 0.0) ? _finish : null);
		UpdateFinishSize();
	}

	private void UpdateFinishSize()
	{
		DpiScale dpi = VisualTreeHelper.GetDpi(this);
		_finish.PixelSize = new Point(Math.Max(1.0, ActualWidth * dpi.DpiScaleX), Math.Max(1.0, ActualHeight * dpi.DpiScaleY));
	}

	private Geometry DetailedCurve(string path)
	{
		Geometry geometry = Geometry.Parse(path).Clone();
		geometry.Transform = new ScaleTransform(DetailScale, DetailScale);
		Geometry obj = (IsPreview ? geometry : geometry.GetFlattenedPathGeometry(0.08, ToleranceType.Absolute));
		obj.Freeze();
		return obj;
	}

	private void Build()
	{
		Stop();
		_canvas.Children.Clear();
		_motions.Clear();
		_reflections.Clear();
		_canvas.Width = 320.0 * DetailScale;
		_canvas.Height = 180.0 * DetailScale;
		_canvas.Background = new SolidColorBrush(Color.FromRgb(10, 17, 31));
		bool validated = Settings?.AuroraPalette == "Ocean";
		bool flag2 = Settings?.AuroraPalette == "Sunset";
		string[] entries;
		if (validated)
		{
			entries = new string[4] { "#21447F", "#397FC5", "#689EE6", "#53BECE" };
		}
		else
		{
			entries = ((!flag2) ? new string[4] { "#6147A0", "#3A81B8", "#AC89DC", "#62CBC4" } : new string[4] { "#693C83", "#D3708C", "#A968B9", "#EDAB8D" });
		}
		for (int i = 0; i < 4; i++)
		{
			Color color = (Color)ColorConverter.ConvertFromString(entries[i]);
			Brush fill = (IsPreview ? ((Brush)new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B))) : ((Brush)SmoothGlow(color)));
			TranslateTransform translateTransform = new TranslateTransform();
			_motions.Add(translateTransform);
			Ellipse element = new Ellipse
			{
				Width = 310.0 * DetailScale,
				Height = 230.0 * DetailScale,
				Fill = fill,
				Opacity = 0.9,
				RenderTransform = translateTransform
			};
			Canvas.SetLeft(element, (double)((i % 2 == 0) ? (-85) : 130) * DetailScale);
			Canvas.SetTop(element, (double)((i < 2) ? (-65) : 40) * DetailScale);
			_canvas.Children.Add(element);
		}
		for (int j = 0; j < 2; j++)
		{
			Color color2 = (Color)ColorConverter.ConvertFromString(entries[(j == 0) ? 3 : 0]);
			Color color3 = Color.FromRgb((byte)((double)(int)color2.R * 0.48), (byte)((double)(int)color2.G * 0.48), (byte)((double)(int)color2.B * 0.55));
			Color color4 = Color.FromRgb((byte)((double)(int)color2.R + (double)(255 - color2.R) * 0.38), (byte)((double)(int)color2.G + (double)(255 - color2.G) * 0.38), (byte)((double)(int)color2.B + (double)(255 - color2.B) * 0.38));
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush
			{
				StartPoint = new Point(0.15, 0.0),
				EndPoint = new Point(0.8, 1.0),
				ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation
			};
			linearGradientBrush.GradientStops.Add(new GradientStop(color3, 0.0));
			linearGradientBrush.GradientStops.Add(new GradientStop(color4, 0.5));
			linearGradientBrush.GradientStops.Add(new GradientStop(color2, 1.0));
			linearGradientBrush.Freeze();
			string text;
			if (j != 0)
			{
				if (validated)
				{
					text = "M-100,-5 C15,115 100,100 185,20 C270,-60 340,-5 420,40";
				}
				else
				{
					text = (flag2 ? "M-100,0 C30,95 105,105 190,35 C270,-40 345,-20 420,25" : "M-100,0 C20,105 100,110 180,30 C260,-55 340,-10 420,30");
				}
			}
			else if (validated)
			{
				text = "M-100,160 C30,215 110,180 185,95 C270,-10 320,80 420,105";
			}
			else
			{
				text = (flag2 ? "M-100,160 C35,175 140,215 205,110 C265,15 315,65 420,100" : "M-100,140 C20,215 120,205 190,115 C280,-5 315,65 420,100");
			}
			Geometry data = DetailedCurve(text + ((j == 0) ? " L420,180 C315,150 265,65 215,140 C130,260 15,240 -100,185 Z" : " L420,70 C335,35 270,-10 205,65 C105,155 15,130 -100,40 Z"));
			TranslateTransform translateTransform2 = new TranslateTransform();
			_motions.Add(translateTransform2);
			Canvas canvas = new Canvas
			{
				RenderTransform = translateTransform2
			};
			canvas.Children.Add(new Path
			{
				Data = data,
				Fill = linearGradientBrush
			});
			LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation
			};
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(0, color4.R, color4.G, color4.B), 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(0, color4.R, color4.G, color4.B), 0.3));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(100, color4.R, color4.G, color4.B), 0.49));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(0, color4.R, color4.G, color4.B), 0.7));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(0, color4.R, color4.G, color4.B), 1.0));
			linearGradientBrush2.Freeze();
			Path path = new Path
			{
				Data = DetailedCurve(text),
				Stroke = linearGradientBrush2,
				StrokeThickness = 0.18 * DetailScale,
				Opacity = 0.38
			};
			canvas.Children.Add(path);
			_reflections.Add(path);
			_canvas.Children.Add(canvas);
		}
	}

	private void Stop()
	{
		_motionClocks.Clear();
		foreach (TranslateTransform motion in _motions)
		{
			motion.BeginAnimation(TranslateTransform.XProperty, null);
			motion.BeginAnimation(TranslateTransform.YProperty, null);
		}
		foreach (Path reflection in _reflections)
		{
			reflection.BeginAnimation(UIElement.OpacityProperty, null);
		}
		IsAnimating = false;
	}

	private void UpdateMotion()
	{
		if (!IsPreview && IsLoaded)
		{
			AppSettings? settings = Settings;
			if (settings != null && settings.EnableAuroraBackground && !Settings.ReduceMotion)
			{
				if (_motionClocks.Count > 0)
				{
					ApplyMotionState();
					return;
				}
				if (!IsVisible)
				{
					IsAnimating = false;
					return;
				}
				for (int i = 0; i < _motions.Count; i++)
				{
					double pageNumber = ((i == 5) ? 0.65 : ((i < 4) ? 0.8 : 1.0)) * DetailScale;
					TimeSpan timeSpan = TimeSpan.FromSeconds((double)(IsPreview ? 5 : 13) + (double)i * 1.7);
					Animate(_motions[i], TranslateTransform.XProperty, new DoubleAnimation((double)((i % 2 == 0) ? (-24) : 24) * pageNumber, (double)((i % 2 == 0) ? 32 : (-32)) * pageNumber, timeSpan)
					{
						BeginTime = TimeSpan.FromSeconds((0.0 - timeSpan.TotalSeconds) * 0.3),
						AutoReverse = true,
						RepeatBehavior = RepeatBehavior.Forever,
						EasingFunction = new SineEase
						{
							EasingMode = EasingMode.EaseInOut
						}
					});
					Animate(_motions[i], TranslateTransform.YProperty, new DoubleAnimation(-10.0 * pageNumber, (double)((i % 2 == 0) ? 18 : (-18)) * pageNumber, TimeSpan.FromSeconds(timeSpan.TotalSeconds * 1.37))
					{
						BeginTime = TimeSpan.FromSeconds((0.0 - timeSpan.TotalSeconds) * 0.2),
						AutoReverse = true,
						RepeatBehavior = RepeatBehavior.Forever,
						EasingFunction = new SineEase
						{
							EasingMode = EasingMode.EaseInOut
						}
					});
				}
				for (int j = 0; j < _reflections.Count; j++)
				{
					AnimateReflection(_reflections[j], new DoubleAnimation(0.18, 0.65, TimeSpan.FromSeconds((IsPreview ? 5 : 19) + j * 6))
					{
						AutoReverse = true,
						RepeatBehavior = RepeatBehavior.Forever,
						EasingFunction = new SineEase
						{
							EasingMode = EasingMode.EaseInOut
						}
					});
				}
				ApplyMotionState();
				return;
			}
		}
		Stop();
	}
}
