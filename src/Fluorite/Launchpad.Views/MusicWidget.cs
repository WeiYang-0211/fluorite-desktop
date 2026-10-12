using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Launchpad.Models;
using Launchpad.Services;

namespace Launchpad.Views;

public sealed class MusicWidget : UserControl
{
	private readonly TextBlock _title = new TextBlock
	{
		FontSize = 13.0,
		FontWeight = FontWeights.SemiBold,
		TextTrimming = TextTrimming.CharacterEllipsis
	};

	private readonly TextBlock _artist = new TextBlock
	{
		FontSize = 11.0,
		TextTrimming = TextTrimming.CharacterEllipsis,
		Opacity = 0.7
	};

	private readonly Image _cover = new Image
	{
		Width = 44.0,
		Height = 44.0,
		Stretch = Stretch.UniformToFill
	};

	private readonly Button _play;

	private readonly Button _previous;

	private readonly Button _next;

	private SystemMusicService? _service;

	private CancellationTokenSource? _lifetime;

	private bool _controlling;

	private byte[]? _lastArtwork;

	private readonly Grid _presentation = new Grid();

	private MusicState _currentState = MusicState.Empty;

	private readonly Grid _orb = new Grid
	{
		Width = 70.0,
		Height = 70.0
	};

	private readonly Border _flyout;

	private readonly Canvas _orbit = new Canvas
	{
		Width = 146.0,
		Height = 146.0,
		Background = Brushes.Transparent,
		Visibility = Visibility.Collapsed
	};

	private readonly System.Windows.Shapes.Path _progress = new System.Windows.Shapes.Path
	{
		StrokeThickness = 3.0,
		StrokeStartLineCap = PenLineCap.Round,
		StrokeEndLineCap = PenLineCap.Round,
		IsHitTestVisible = false
	};

	private readonly RotateTransform _swirl = new RotateTransform();

	private readonly RotateTransform _albumSpin = new RotateTransform();

	private MusicSpectrumService? _spectrum;

	private readonly DispatcherTimer _spectrumTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(33.0)
	};

	private readonly ScaleTransform _haloScale = new ScaleTransform(1.0, 1.0);

	private readonly GradientStop _haloColor = new GradientStop(Color.FromArgb(225, 65, 170, byte.MaxValue), 0.58);

	private readonly System.Windows.Shapes.Path _halo = new System.Windows.Shapes.Path
	{
		Width = 112.0,
		Height = 112.0,
		Opacity = 0.0,
		IsHitTestVisible = false,
		StrokeThickness = 1.6,
		StrokeLineJoin = PenLineJoin.Round,
		StrokeStartLineCap = PenLineCap.Round,
		StrokeEndLineCap = PenLineCap.Round
	};

	private readonly System.Windows.Shapes.Path _spectrumEcho = new System.Windows.Shapes.Path
	{
		Width = 112.0,
		Height = 112.0,
		Opacity = 0.0,
		Stroke = new SolidColorBrush(Color.FromArgb(210, 235, 248, byte.MaxValue)),
		StrokeThickness = 0.8,
		IsHitTestVisible = false,
		RenderTransformOrigin = new Point(0.5, 0.5),
		RenderTransform = new ScaleTransform(0.94, 0.94)
	};

	private double _spectrumPhase;

	private double _bass;

	private double _mid;

	private double _treble;

	private FrameworkElement? _bounds;

	private Point? _press;

	private bool _dragging;

	private bool _rightEdge;

	private bool _topEdge;

	public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register("Settings", typeof(AppSettings), typeof(MusicWidget), new PropertyMetadata(null, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
	{
		MusicWidget musicWidget = (MusicWidget)d;
		if (e.OldValue is AppSettings appSettings)
		{
			appSettings.PropertyChanged -= musicWidget.OrbSettingsChanged;
		}
		if (musicWidget.IsLoaded && e.NewValue is AppSettings appSettings2)
		{
			appSettings2.PropertyChanged += musicWidget.OrbSettingsChanged;
		}
		if (musicWidget.IsLoaded)
		{
			musicWidget.PositionAtEdge();
		}
		musicWidget.UpdatePresentation();
	}));

	private readonly ScaleTransform _hoverScale = new ScaleTransform(1.0, 1.0);

	private readonly ScaleTransform _flyoutScale = new ScaleTransform(1.0, 1.0);

	private readonly TranslateTransform _flyoutOffset = new TranslateTransform();

	private bool _closing;

	private int _transition;

	private bool _orbAnimating;

	private bool _wantsAnimation;

	public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register("ReduceMotion", typeof(bool), typeof(MusicWidget), new PropertyMetadata(false, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
	{
		MusicWidget musicWidget = (MusicWidget)d;
		musicWidget.AnimateOrb(musicWidget._wantsAnimation);
	}));

	private readonly DispatcherTimer _closeTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(160.0)
	};

	public static readonly DependencyProperty DockHeightProperty = DependencyProperty.Register("DockHeight", typeof(double), typeof(MusicWidget), new PropertyMetadata(0.0, PositionChanged));

	public static readonly DependencyProperty DockEnabledProperty = DependencyProperty.Register("DockEnabled", typeof(bool), typeof(MusicWidget), new PropertyMetadata(false, PositionChanged));

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

	public bool ReduceMotion
	{
		get
		{
			return (bool)GetValue(ReduceMotionProperty);
		}
		set
		{
			SetValue(ReduceMotionProperty, value);
		}
	}

	public double DockHeight
	{
		get
		{
			return (double)GetValue(DockHeightProperty);
		}
		set
		{
			SetValue(DockHeightProperty, value);
		}
	}

	public bool DockEnabled
	{
		get
		{
			return (bool)GetValue(DockEnabledProperty);
		}
		set
		{
			SetValue(DockEnabledProperty, value);
		}
	}

	private static void PositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MusicWidget musicWidget = (MusicWidget)d;
		if (musicWidget.Settings != null && musicWidget.IsLoaded)
		{
			musicWidget.PositionAtEdge();
		}
		else
		{
			musicWidget.Margin = new Thickness(16.0, 0.0, 16.0, musicWidget.DockEnabled ? (musicWidget.DockHeight + 40.0) : 16.0);
		}
	}

	public MusicWidget()
	{
		Width = 70.0;
		Height = 146.0;
		HorizontalContentAlignment = HorizontalAlignment.Stretch;
		Grid presentation = _presentation;
		_halo.Stroke = new LinearGradientBrush(new GradientStopCollection
		{
			new GradientStop(Color.FromRgb(230, 250, byte.MaxValue), 0.0),
			_haloColor,
			new GradientStop(Color.FromRgb(202, 192, 245), 1.0)
		}, 45.0);
		_halo.Data = CreateSpectrumRing(0.0, 0.0, 0.0, 0.0);
		_halo.RenderTransform = _haloScale;
		_halo.RenderTransformOrigin = new Point(0.5, 0.5);
		Canvas canvas = new Canvas
		{
			Width = 70.0,
			Height = 70.0,
			IsHitTestVisible = false,
			ClipToBounds = false
		};
		Canvas.SetLeft(_halo, -21.0);
		Canvas.SetTop(_halo, -21.0);
		Canvas.SetLeft(_spectrumEcho, -21.0);
		Canvas.SetTop(_spectrumEcho, -21.0);
		canvas.Children.Add(_spectrumEcho);
		canvas.Children.Add(_halo);
		_orb.Children.Add(canvas);
		_spectrumTimer.Tick += (object? _, EventArgs _) =>
		{
			UpdateSpectrumFrame();
		};
		_orb.Children.Add(new Ellipse
		{
			Width = 64.0,
			Height = 64.0,
			Fill = new LinearGradientBrush(Color.FromArgb(100, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(48, 224, 231, 238), 90.0),
			Effect = new DropShadowEffect
			{
				Color = Color.FromRgb(20, 30, 44),
				Opacity = 0.16,
				BlurRadius = 10.0,
				ShadowDepth = 2.0
			},
			IsHitTestVisible = false
		});
		Ellipse ellipse = new Ellipse
		{
			Width = 56.0,
			Height = 56.0,
			RenderTransform = _swirl,
			RenderTransformOrigin = new Point(0.5, 0.5)
		};
		ellipse.Fill = new LinearGradientBrush(new GradientStopCollection
		{
			new GradientStop(Color.FromArgb(210, 220, 231, 240), 0.0),
			new GradientStop(Color.FromArgb(165, 151, 170, 193), 0.4),
			new GradientStop(Color.FromArgb(175, 188, 183, 209), 0.72),
			new GradientStop(Color.FromArgb(210, 209, 231, 229), 1.0)
		}, 45.0);
		Grid grid = new Grid
		{
			Width = 56.0,
			Height = 56.0,
			Clip = new EllipseGeometry(new Point(28.0, 28.0), 28.0, 28.0)
		};
		grid.Children.Add(ellipse);
		grid.Children.Add(new TextBlock
		{
			Text = "♪",
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 28.0,
			Foreground = new SolidColorBrush(Color.FromArgb(170, 57, 72, 91)),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		});
		_cover.Width = 56.0;
		_cover.Height = 56.0;
		_cover.RenderTransform = _albumSpin;
		_cover.RenderTransformOrigin = new Point(0.5, 0.5);
		grid.Children.Add(_cover);
		grid.Children.Add(new Ellipse
		{
			Width = 56.0,
			Height = 56.0,
			Fill = new RadialGradientBrush(Color.FromArgb(115, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue))
			{
				Center = new Point(0.28, 0.18),
				GradientOrigin = new Point(0.28, 0.18),
				RadiusX = 0.72,
				RadiusY = 0.65
			},
			IsHitTestVisible = false
		});
		_orb.Children.Add(grid);
		_orb.Children.Add(new Ellipse
		{
			Width = 64.0,
			Height = 64.0,
			Stroke = new LinearGradientBrush(Color.FromArgb(185, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(35, byte.MaxValue, byte.MaxValue, byte.MaxValue), 70.0),
			StrokeThickness = 1.0,
			IsHitTestVisible = false
		});
		_progress.Stroke = new LinearGradientBrush(Color.FromRgb(152, 208, 224), Color.FromRgb(192, 187, 220), 45.0);
		_orb.Children.Add(_progress);
		_orb.HorizontalAlignment = HorizontalAlignment.Left;
		_orb.VerticalAlignment = VerticalAlignment.Bottom;
		_orb.RenderTransform = _hoverScale;
		_orb.RenderTransformOrigin = new Point(0.5, 0.5);
		_orb.Background = Brushes.Transparent;
		_orb.Cursor = Cursors.Hand;
		StackPanel stackPanel = new StackPanel();
		_title.SetResourceReference(TextBlock.ForegroundProperty, "SheetTextBrush");
		_artist.SetResourceReference(TextBlock.ForegroundProperty, "SheetTextBrush");
		stackPanel.Children.Add(_title);
		stackPanel.Children.Add(_artist);
		_previous = Control("‹", "上一首", "previous");
		_play = Control("▶", "播放 / 暂停", "toggle");
		_next = Control("›", "下一首", "next");
		_previous.FontSize = (_next.FontSize = 24.0);
		PlaceOrbitButton(_previous, 16.0, 20.0);
		PlaceOrbitButton(_play, 66.0, 44.0);
		PlaceOrbitButton(_next, 88.0, 94.0);
		_flyout = new Border
		{
			Child = stackPanel,
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(14.0),
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(150.0, 0.0, 0.0, 8.0),
			VerticalAlignment = VerticalAlignment.Bottom,
			Visibility = Visibility.Collapsed
		};
		_flyout.SetResourceReference(Border.BackgroundProperty, "T.Chip_F2");
		_flyout.SetResourceReference(Border.BorderBrushProperty, "T.SearchBorder");
		TransformGroup renderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)_flyoutScale,
				(Transform)_flyoutOffset
			}
		};
		_flyout.RenderTransform = renderTransform;
		presentation.Children.Add(_flyout);
		presentation.Children.Add(_orbit);
		presentation.Children.Add(_orb);
		Content = presentation;
		MouseEnter += (object _, MouseEventArgs _) =>
		{
			Expand();
		};
		GotKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			Expand();
		};
		MouseLeave += (object _, MouseEventArgs _) =>
		{
			_closeTimer.Stop();
			_closeTimer.Start();
		};
		LostKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			_closeTimer.Stop();
			_closeTimer.Start();
		};
		_closeTimer.Tick += (object? _, EventArgs _) =>
		{
			_closeTimer.Stop();
			if (!IsMouseOver && !IsKeyboardFocusWithin)
			{
				Collapse(animate: true);
			}
		};
		_orb.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			if (_bounds != null)
			{
				_press = e.GetPosition(_bounds);
				_dragging = false;
				_orb.CaptureMouse();
				e.Handled = true;
			}
		};
		_orb.MouseMove += (object _, MouseEventArgs e) =>
		{
			if (_press.HasValue && _bounds != null && e.LeftButton == MouseButtonState.Pressed)
			{
				Point position = e.GetPosition(_bounds);
				if (_dragging || !((position - _press.Value).Length < 6.0))
				{
					_dragging = true;
					Collapse();
					HorizontalAlignment = HorizontalAlignment.Left;
					VerticalAlignment = VerticalAlignment.Top;
					Margin = new Thickness(Math.Clamp(position.X - 35.0, 0.0, Math.Max(0.0, _bounds.ActualWidth - 70.0)), Math.Clamp(position.Y - (double)(_topEdge ? 35 : 111), 0.0, Math.Max(0.0, _bounds.ActualHeight - Height)), 0.0, 0.0);
					e.Handled = true;
				}
			}
		};
		_orb.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			if (_dragging && _bounds != null && Settings != null)
			{
				Point position = e.GetPosition(_bounds);
				Snap(position);
			}
			_press = null;
			_dragging = false;
			_orb.ReleaseMouseCapture();
			if (!_dragging)
			{
				Expand();
			}
			e.Handled = true;
		};
		_orb.LostMouseCapture += (object _, MouseEventArgs _) =>
		{
			_press = null;
			_dragging = false;
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			if (Settings != null)
			{
				Settings.PropertyChanged += OrbSettingsChanged;
			}
			_bounds = Parent as FrameworkElement;
			if (_bounds != null)
			{
				_bounds.SizeChanged += BoundsChanged;
			}
			PositionAtEdge();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			if (Settings != null)
			{
				Settings.PropertyChanged -= OrbSettingsChanged;
			}
			if (_bounds != null)
			{
				_bounds.SizeChanged -= BoundsChanged;
			}
			_bounds = null;
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			UpdateLifetime();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			Stop();
		};
		IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			UpdateLifetime();
		};
		Apply(MusicState.Empty);
	}

	private void Expand()
	{
		_closeTimer.Stop();
		if (_dragging || (_orbit.Visibility == Visibility.Visible && !_closing))
		{
			return;
		}
		bool validated = _orbit.Visibility != Visibility.Visible;
		_closing = false;
		_transition++;
		double value = ((Parent is FrameworkElement frameworkElement) ? (frameworkElement.ActualWidth - 32.0) : 320.0);
		Width = Math.Clamp(value, 146.0, 360.0);
		_orbit.Visibility = Visibility.Visible;
		_flyout.Visibility = ((!(Width >= 280.0)) ? Visibility.Collapsed : Visibility.Visible);
		PositionAtEdge();
		_orbit.IsHitTestVisible = true;
		_flyout.IsHitTestVisible = true;
		if (ReduceMotion)
		{
			_orbit.BeginAnimation(UIElement.OpacityProperty, null);
			_orbit.Opacity = 1.0;
			_flyout.BeginAnimation(UIElement.OpacityProperty, null);
			_flyout.Opacity = 1.0;
			return;
		}
		Tween(_hoverScale, ScaleTransform.ScaleXProperty, 1.035, 300);
		Tween(_hoverScale, ScaleTransform.ScaleYProperty, 1.035, 300);
		if (validated)
		{
			_orbit.BeginAnimation(UIElement.OpacityProperty, null);
			_orbit.Opacity = 0.0;
			_flyout.BeginAnimation(UIElement.OpacityProperty, null);
			_flyout.Opacity = 0.0;
		}
		AnimateFlyout(opening: true, validated);
		Tween(_orbit, UIElement.OpacityProperty, 1.0, 200);
		Tween(_flyout, UIElement.OpacityProperty, 1.0, 270);
		AnimateButtons(opening: true, validated);
	}

	private static void Tween(Animatable target, DependencyProperty property, double to, int milliseconds, int delay = 0)
	{
		target.BeginAnimation(property, Motion((double)target.GetValue(property), to, milliseconds, delay));
	}

	private static void Tween(UIElement target, DependencyProperty property, double to, int milliseconds)
	{
		target.BeginAnimation(property, Motion((double)target.GetValue(property), to, milliseconds));
	}

	private static DoubleAnimationUsingKeyFrames Motion(double from, double to, int milliseconds, int delay = 0)
	{
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		if (delay > 0)
		{
			doubleAnimationUsingKeyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
		}
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds + delay)), new KeySpline(0.2, 0.85, 0.25, 1.0)));
		return doubleAnimationUsingKeyFrames;
	}

	private void AnimateFlyout(bool opening, bool fresh = false)
	{
		double pageNumber = (_rightEdge ? 12 : (-12));
		_flyout.RenderTransformOrigin = new Point(_rightEdge ? 1 : 0, _topEdge ? 0.2 : 0.8);
		if (fresh)
		{
			ScaleTransform flyoutScale = _flyoutScale;
			double scaleX = (_flyoutScale.ScaleY = 0.96);
			flyoutScale.ScaleX = scaleX;
			_flyoutOffset.X = pageNumber;
		}
		Tween(_flyoutScale, ScaleTransform.ScaleXProperty, opening ? 1.0 : 0.96, opening ? 340 : 180);
		Tween(_flyoutScale, ScaleTransform.ScaleYProperty, opening ? 1.0 : 0.96, opening ? 340 : 180);
		Tween(_flyoutOffset, TranslateTransform.XProperty, opening ? 0.0 : pageNumber, opening ? 340 : 180);
	}

	private void AnimateButtons(bool opening, bool fresh = false)
	{
		int pageNumber = 0;
		foreach (FrameworkElement child in _orbit.Children)
		{
			double settingIndex = ((double)(_rightEdge ? 111 : 35) - (Canvas.GetLeft(child) + 20.0)) * 0.55;
			double targetPage = ((double)(_topEdge ? 35 : 111) - (Canvas.GetTop(child) + 20.0)) * 0.55;
			TransformGroup transformGroup = child.RenderTransform as TransformGroup;
			if (transformGroup == null)
			{
				transformGroup = new TransformGroup();
				transformGroup.Children.Add(new ScaleTransform(1.0, 1.0));
				transformGroup.Children.Add(new TranslateTransform());
				child.RenderTransform = transformGroup;
				child.RenderTransformOrigin = new Point(0.5, 0.5);
			}
			ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
			TranslateTransform translateTransform = (TranslateTransform)transformGroup.Children[1];
			if (fresh)
			{
				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				translateTransform.X = settingIndex;
				translateTransform.Y = targetPage;
				double scaleX = (scaleTransform.ScaleY = 0.82);
				scaleTransform.ScaleX = scaleX;
			}
			int delay = ((opening & fresh) ? (pageNumber * 28) : 0);
			Tween(translateTransform, TranslateTransform.XProperty, opening ? 0.0 : settingIndex, opening ? 360 : 220, delay);
			Tween(translateTransform, TranslateTransform.YProperty, opening ? 0.0 : targetPage, opening ? 360 : 220, delay);
			Tween(scaleTransform, ScaleTransform.ScaleXProperty, opening ? 1.0 : 0.82, opening ? 360 : 220, delay);
			Tween(scaleTransform, ScaleTransform.ScaleYProperty, opening ? 1.0 : 0.82, opening ? 360 : 220, delay);
			pageNumber++;
		}
	}

	private void Collapse(bool animate = false)
	{
		if (animate && !ReduceMotion && _orbit.Visibility == Visibility.Visible)
		{
			if (_closing)
			{
				return;
			}
			_closing = true;
			int transition = ++_transition;
			_orbit.IsHitTestVisible = false;
			_flyout.IsHitTestVisible = false;
			AnimateButtons(opening: false);
			AnimateFlyout(opening: false);
			Tween(_flyout, UIElement.OpacityProperty, 0.0, 180);
			Tween(_hoverScale, ScaleTransform.ScaleXProperty, 1.0, 200);
			Tween(_hoverScale, ScaleTransform.ScaleYProperty, 1.0, 200);
			DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = Motion(_orbit.Opacity, 0.0, 240);
			doubleAnimationUsingKeyFrames.Completed += (object? _, EventArgs _) =>
			{
				if (_closing && transition == _transition)
				{
					Collapse();
				}
			};
			_orbit.BeginAnimation(UIElement.OpacityProperty, doubleAnimationUsingKeyFrames);
			return;
		}
		_transition++;
		_closing = false;
		_orbit.BeginAnimation(UIElement.OpacityProperty, null);
		_flyout.BeginAnimation(UIElement.OpacityProperty, null);
		_hoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		_hoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ScaleTransform hoverScale = _hoverScale;
		double scaleX = (_hoverScale.ScaleY = 1.0);
		hoverScale.ScaleX = scaleX;
		_flyoutScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		_flyoutScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		_flyoutOffset.BeginAnimation(TranslateTransform.XProperty, null);
		ScaleTransform flyoutScale = _flyoutScale;
		scaleX = (_flyoutScale.ScaleY = 1.0);
		flyoutScale.ScaleX = scaleX;
		_flyoutOffset.X = 0.0;
		_flyout.Visibility = Visibility.Collapsed;
		_orbit.Visibility = Visibility.Collapsed;
		Width = 70.0;
		if (!_dragging)
		{
			PositionAtEdge();
		}
	}

	private void OrbSettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "EnableMusicSpectrum" || propertyName == "ShowMusicWidget") ? true : false)
		{
			UpdateSpectrumLifetime();
		}
		if (e.PropertyName == "HideMusicWidgetWhenIdle")
		{
			UpdatePresentation();
		}
		propertyName = e.PropertyName;
		if ((propertyName == "MusicOrbEdge" || propertyName == "MusicOrbOffset") ? true : false)
		{
			PositionAtEdge();
		}
	}

	private void BoundsChanged(object sender, SizeChangedEventArgs e)
	{
		PositionAtEdge();
	}

	private void Snap(Point point)
	{
		if (Settings != null && _bounds != null)
		{
			double actualWidth = _bounds.ActualWidth;
			double actualHeight = _bounds.ActualHeight;
			string item = new (string, double)[4]
			{
				("Left", point.X),
				("Right", actualWidth - point.X),
				("Top", point.Y),
				("Bottom", actualHeight - point.Y)
			}.OrderBy(((string, double) x) => x.Item2).First().Item1;
			Settings.MusicOrbEdge = item;
			AppSettings settings = Settings;
			bool validated = ((item == "Left" || item == "Right") ? true : false);
			settings.MusicOrbOffset = Math.Clamp(validated ? (point.Y / Math.Max(1.0, actualHeight)) : (point.X / Math.Max(1.0, actualWidth)), 0.0, 1.0);
			PositionAtEdge();
		}
	}

	private void PositionAtEdge()
	{
		if (_bounds != null && Settings != null && !(_bounds.ActualWidth <= 0.0) && !(_bounds.ActualHeight <= 0.0))
		{
			double actualWidth = _bounds.ActualWidth;
			double actualHeight = _bounds.ActualHeight;
			double pageNumber = (double.IsFinite(Settings.MusicOrbOffset) ? Math.Clamp(Settings.MusicOrbOffset, 0.0, 1.0) : 1.0);
			double settingIndex = Math.Max(16.0, actualHeight - 70.0 - (DockEnabled ? (DockHeight + 40.0) : 16.0));
			double targetPage = Math.Clamp(pageNumber * actualWidth - 35.0, 16.0, Math.Max(16.0, actualWidth - 86.0));
			double num4 = Math.Clamp(pageNumber * actualHeight - 35.0, 16.0, settingIndex);
			switch (Settings.MusicOrbEdge)
			{
			case "Right":
				targetPage = Math.Max(16.0, actualWidth - 86.0);
				break;
			case "Top":
				num4 = 16.0;
				break;
			case "Bottom":
				num4 = settingIndex;
				break;
			default:
				targetPage = 16.0;
				break;
			}
			_rightEdge = Settings.MusicOrbEdge == "Right" || targetPage + Width > actualWidth - 16.0;
			_topEdge = num4 < 76.0;
			_previous.Content = "‹";
			_next.Content = "›";
			_orb.HorizontalAlignment = (_rightEdge ? HorizontalAlignment.Right : HorizontalAlignment.Left);
			_orb.VerticalAlignment = ((!_topEdge) ? VerticalAlignment.Bottom : VerticalAlignment.Top);
			_orbit.HorizontalAlignment = _orb.HorizontalAlignment;
			_orbit.VerticalAlignment = VerticalAlignment.Top;
			_orbit.LayoutTransform = Transform.Identity;
			bool validated = Settings.MusicOrbEdge == "Top";
			Button button = ((_rightEdge != validated) ? _next : _previous);
			Button button2 = ((_rightEdge != validated) ? _previous : _next);
			ArrangeControl(button, _topEdge ? 88 : 16, _topEdge ? 12 : 20);
			ArrangeControl(_play, 66.0, _topEdge ? 62 : 44);
			ArrangeControl(button2, _topEdge ? 16 : 88, _topEdge ? 86 : 94);
			_flyout.Margin = (_rightEdge ? new Thickness(0.0, 8.0, 150.0, 8.0) : new Thickness(150.0, 8.0, 0.0, 8.0));
			_flyout.VerticalAlignment = ((!_topEdge) ? VerticalAlignment.Bottom : VerticalAlignment.Top);
			HorizontalAlignment = (_rightEdge ? HorizontalAlignment.Right : HorizontalAlignment.Left);
			VerticalAlignment = VerticalAlignment.Top;
			double top = Math.Max(0.0, _topEdge ? num4 : (num4 - 76.0));
			Margin = (_rightEdge ? new Thickness(0.0, top, Math.Max(16.0, actualWidth - targetPage - 70.0), 0.0) : new Thickness(Math.Max(16.0, targetPage), top, 0.0, 0.0));
		}
	}

	private void ArrangeControl(Button button, double x, double y)
	{
		button.LayoutTransform = Transform.Identity;
		Canvas.SetLeft(button, _rightEdge ? (106.0 - x) : x);
		Canvas.SetTop(button, y);
	}

	private void PlaceOrbitButton(Button button, double x, double y)
	{
		button.Width = 40.0;
		button.Height = 40.0;
		button.Margin = new Thickness(0.0);
		button.Cursor = Cursors.Hand;
		button.IsEnabledChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			button.Opacity = (button.IsEnabled ? 1.0 : 0.35);
		};
		button.Template = new ControlTemplate(typeof(Button))
		{
			VisualTree = OrbitButtonVisual()
		};
		Canvas.SetLeft(button, x);
		Canvas.SetTop(button, y);
		_orbit.Children.Add(button);
		_orbit.HorizontalAlignment = HorizontalAlignment.Left;
		_orbit.VerticalAlignment = VerticalAlignment.Bottom;
	}

	private static FrameworkElementFactory OrbitButtonVisual()
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(20.0));
		frameworkElementFactory.SetResourceReference(Border.BackgroundProperty, "T.Chip_F2");
		frameworkElementFactory.SetResourceReference(Border.BorderBrushProperty, "T.SearchBorder");
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		return frameworkElementFactory;
	}

	private void AnimateOrb(bool animate)
	{
		_wantsAnimation = animate;
		UpdateSpectrumLifetime();
		animate = animate && !ReduceMotion;
		if (_orbAnimating != animate)
		{
			_orbAnimating = animate;
			double angle = _albumSpin.Angle;
			_albumSpin.BeginAnimation(RotateTransform.AngleProperty, animate ? new DoubleAnimation(_albumSpin.Angle, _albumSpin.Angle + 360.0, TimeSpan.FromSeconds(18.0))
			{
				RepeatBehavior = RepeatBehavior.Forever
			} : null);
			if (!animate)
			{
				_albumSpin.Angle = angle;
			}
			_swirl.BeginAnimation(RotateTransform.AngleProperty, animate ? new DoubleAnimation(0.0, 360.0, TimeSpan.FromSeconds(18.0))
			{
				RepeatBehavior = RepeatBehavior.Forever
			} : null);
		}
	}

	private void UpdateSpectrumLifetime()
	{
		if (_wantsAnimation && IsLoaded && IsVisible && _presentation.Visibility == Visibility.Visible)
		{
			AppSettings? settings = Settings;
			if (settings != null && settings.ShowMusicWidget && Settings.EnableMusicSpectrum && _currentState.Playing && !ReduceMotion && _lifetime != null)
			{
				if (_spectrum == null)
				{
					_spectrum = new MusicSpectrumService();
				}
				_spectrumTimer.Start();
				return;
			}
		}
		_spectrum?.Dispose();
		_spectrum = null;
		if (IsLoaded && IsVisible && !ReduceMotion && _presentation.Visibility == Visibility.Visible)
		{
			AppSettings? settings2 = Settings;
			if (settings2 != null && settings2.ShowMusicWidget && Settings.EnableMusicSpectrum)
			{
				if (_halo.Opacity > 0.001)
				{
					_spectrumTimer.Start();
				}
				return;
			}
		}
		_spectrumTimer.Stop();
		_bass = (_mid = (_treble = 0.0));
		_halo.Opacity = 0.0;
		_spectrumEcho.Opacity = 0.0;
		ScaleTransform haloScale = _haloScale;
		double scaleX = (_haloScale.ScaleY = 1.0);
		haloScale.ScaleX = scaleX;
	}

	private void UpdateSpectrumFrame()
	{
		MusicSpectrumService.Levels levels = _spectrum?.Current;
		_bass = Smooth(_bass, Boost(levels?.Bass ?? 0.0));
		_mid = Smooth(_mid, Boost(levels?.Mid ?? 0.0));
		_treble = Smooth(_treble, Boost(levels?.Treble ?? 0.0));
		ScaleTransform haloScale = _haloScale;
		double scaleX = (_haloScale.ScaleY = 1.0);
		haloScale.ScaleX = scaleX;
		_spectrumPhase += 0.055;
		_halo.Data = CreateSpectrumRing(_bass, _mid, _treble, _spectrumPhase);
		_spectrumEcho.Data = _halo.Data;
		_halo.Opacity = Math.Clamp(_bass * 0.85 + _mid * 0.65 + _treble * 0.35, 0.0, 0.95);
		_spectrumEcho.Opacity = _halo.Opacity * 0.32;
		_haloColor.Color = Color.FromArgb(225, (byte)(65.0 + 100.0 * _treble), (byte)(150.0 + 55.0 * _mid), byte.MaxValue);
		if (_spectrum == null && _halo.Opacity < 0.001)
		{
			_halo.Opacity = 0.0;
			_spectrumTimer.Stop();
		}
		static double Boost(double value)
		{
			return Math.Clamp(Math.Sqrt(Math.Max(0.0, value)) * 1.35, 0.0, 1.0);
		}
		static double Smooth(double current, double target)
		{
			return current + (target - current) * ((target > current) ? 0.4 : 0.12);
		}
	}

	internal static StreamGeometry CreateSpectrumRing(double bass, double mid, double treble, double phase)
	{
		Point[] entries = new Point[96];
		for (int i = 0; i < 96; i++)
		{
			double pageNumber = (double)i * Math.PI * 2.0 / 96.0;
			double settingIndex = 38.0 + bass * 1.7 + bass * 1.6 * Math.Sin(3.0 * pageNumber + phase) + mid * 1.5 * Math.Sin(6.0 * pageNumber - phase) + treble * 0.8 * Math.Sin(12.0 * pageNumber + phase * 1.3);
			entries[i] = new Point(56.0 + settingIndex * Math.Cos(pageNumber), 56.0 + settingIndex * Math.Sin(pageNumber));
		}
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(entries[0], isFilled: false, isClosed: true);
			for (int j = 0; j < 96; j++)
			{
				Point point = entries[(j + 96 - 1) % 96];
				Point point2 = entries[j];
				Point point3 = entries[(j + 1) % 96];
				Point point4 = entries[(j + 2) % 96];
				streamGeometryContext.BezierTo(point2 + (point3 - point) / 6.0, point3 - (point4 - point2) / 6.0, point3, isStroked: true, isSmoothJoin: true);
			}
		}
		streamGeometry.Freeze();
		return streamGeometry;
	}

	internal void SetProgress(double fraction)
	{
		if (fraction <= 0.0 || fraction > 1.0)
		{
			_progress.Data = null;
			return;
		}
		double pageNumber = Math.Min(fraction, 0.99999) * Math.PI * 2.0;
		Point point = new Point(35.0 + 32.0 * Math.Sin(pageNumber), 35.0 - 32.0 * Math.Cos(pageNumber));
		PathFigure pathFigure = new PathFigure
		{
			StartPoint = new Point(35.0, 3.0)
		};
		pathFigure.Segments.Add(new ArcSegment(point, new Size(32.0, 32.0), 0.0, fraction > 0.5, SweepDirection.Clockwise, isStroked: true));
		_progress.Data = new PathGeometry(new PathFigure[1] { pathFigure });
	}

	private Button Control(string text, string tip, string action)
	{
		Button button = new Button();
		button.Content = text;
		button.ToolTip = tip;
		button.MinWidth = 30.0;
		button.Height = 30.0;
		button.Padding = new Thickness(5.0);
		button.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		button.VerticalAlignment = VerticalAlignment.Center;
		button.SetResourceReference(FrameworkElement.StyleProperty, "WorkspaceButton");
		button.Click += async (object _, RoutedEventArgs _) =>
		{
			if (_controlling || _service == null || _lifetime == null)
			{
				return;
			}
			_controlling = true;
			try
			{
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
				timeout.CancelAfter(TimeSpan.FromSeconds(5.0));
				if (!(await _service.ControlAsync(action, timeout.Token)))
				{
					_artist.Text = "播放器暂不支持此操作";
				}
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				_controlling = false;
			}
		};
		return button;
	}

	private void UpdateLifetime()
	{
		if (!IsLoaded || !IsVisible)
		{
			Stop();
		}
		else if (_lifetime == null)
		{
			_lifetime = new CancellationTokenSource();
			_service = new SystemMusicService();
			AnimateOrb(animate: true);
			PollAsync(_service, _lifetime.Token);
		}
	}

	private async Task PollAsync(SystemMusicService service, CancellationToken cancellation)
	{
		try
		{
			while (!cancellation.IsCancellationRequested)
			{
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
				timeout.CancelAfter(TimeSpan.FromSeconds(5.0));
				try
				{
					MusicState state = await service.ReadAsync(timeout.Token);
					if (!cancellation.IsCancellationRequested)
					{
						Apply(state);
					}
				}
				catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
				{
					Apply(MusicState.Empty with
					{
						Artist = "播放器响应较慢，正在重试…"
					});
				}
				await Task.Delay(1000, cancellation);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex3)
		{
			App.Log("Music widget: " + ex3);
		}
		finally
		{
			service.Dispose();
		}
	}

	private void Apply(MusicState state)
	{
		_currentState = state;
		UpdatePresentation();
		_title.Text = state.Title;
		_artist.Text = state.Artist;
		_play.Content = (state.Playing ? "Ⅱ" : "▶");
		_play.IsEnabled = state.CanPlay;
		_next.IsEnabled = state.CanNext;
		_previous.IsEnabled = state.CanPrevious;
		SetProgress(state.Progress);
		AnimateOrb(IsVisible && _presentation.Visibility == Visibility.Visible && (state.Player == "" || state.Playing));
		if (_lastArtwork == state.Artwork)
		{
			return;
		}
		_lastArtwork = state.Artwork;
		_cover.Source = null;
		byte[] artwork = state.Artwork;
		if (artwork == null || artwork.Length <= 0)
		{
			return;
		}
		try
		{
			using MemoryStream streamSource = new MemoryStream(state.Artwork);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = 88;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			_cover.Source = bitmapImage;
		}
		catch (Exception ex)
		{
			App.Log("Music artwork ignored: " + ex.Message);
		}
	}

	private void UpdatePresentation()
	{
		AppSettings? settings = Settings;
		bool validated = settings == null || !settings.HideMusicWidgetWhenIdle || !string.IsNullOrWhiteSpace(_currentState.Player);
		if (!validated)
		{
			Collapse();
			AnimateOrb(animate: false);
		}
		_presentation.Visibility = ((!validated) ? Visibility.Collapsed : Visibility.Visible);
		IsHitTestVisible = validated;
		if (validated)
		{
			AnimateOrb(IsVisible && (_currentState.Player == "" || _currentState.Playing));
		}
	}

	private void Stop()
	{
		_closeTimer.Stop();
		Collapse();
		_spectrum?.Dispose();
		_spectrum = null;
		_spectrumTimer.Stop();
		System.Windows.Shapes.Path halo = _halo;
		double opacity = (_spectrumEcho.Opacity = 0.0);
		halo.Opacity = opacity;
		_bass = (_mid = (_treble = 0.0));
		AnimateOrb(animate: false);
		_lifetime?.Cancel();
		_lifetime?.Dispose();
		_lifetime = null;
		_service?.Dispose();
		_service = null;
	}
}
