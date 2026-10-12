using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launchpad.Views;

internal sealed class GlassClockDigits : FrameworkElement
{
	private string _text = "";

	private Geometry? _glyphs;

	private readonly Dictionary<char, Geometry> _characters = new Dictionary<char, Geometry>();

	private double _digitWidth;

	private double _colonWidth;

	private double _top;

	private double _height;

	public string Text
	{
		get
		{
			return _text;
		}
		set
		{
			if (!(_text == value))
			{
				bool validated = _text.Length != value.Length;
				_text = value;
				_glyphs = null;
				if (validated)
				{
					InvalidateMeasure();
				}
				InvalidateVisual();
			}
		}
	}

	public double FontSize { get; init; }

	public FontWeight FontWeight { get; init; }

	public Color Tint { get; set; } = Colors.White;

	public BitmapSource? Backdrop { get; set; }

	public Rect BackdropRegion { get; set; } = new Rect(0.0, 0.0, 1.0, 1.0);

	public double AmbientBrightness { get; set; } = 70.0;

	private FormattedText Format(string text)
	{
		return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI Variable Display"), FontStyles.Normal, FontWeight, FontStretches.Normal), FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
	}

	private void PrepareCharacters()
	{
		if (_characters.Count != 0)
		{
			return;
		}
		double pageNumber = double.MinValue;
		_top = double.MaxValue;
		string text = "0123456789:";
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			FormattedText formattedText = Format(c.ToString());
			Geometry geometry = formattedText.BuildGeometry(default);
			geometry.Freeze();
			_characters.Add(c, geometry);
			if (c == ':')
			{
				_colonWidth = formattedText.WidthIncludingTrailingWhitespace;
			}
			else
			{
				_digitWidth = Math.Max(_digitWidth, formattedText.WidthIncludingTrailingWhitespace);
			}
			_top = Math.Min(_top, geometry.Bounds.Top);
			pageNumber = Math.Max(pageNumber, geometry.Bounds.Bottom);
		}
		_height = pageNumber - _top + 4.0;
	}

	private Geometry Glyphs()
	{
		if (_glyphs != null)
		{
			return _glyphs;
		}
		PrepareCharacters();
		GeometryGroup geometryGroup = new GeometryGroup();
		double pageNumber = 2.0;
		string text = _text;
		foreach (char c in text)
		{
			if (_characters.TryGetValue(c, out Geometry value))
			{
				double settingIndex = ((c == ':') ? _colonWidth : _digitWidth);
				Geometry geometry = value.Clone();
				geometry.Transform = new TranslateTransform(pageNumber + (settingIndex - value.Bounds.Width) / 2.0 - value.Bounds.X, 2.0 - _top);
				geometryGroup.Children.Add(geometry);
				pageNumber += settingIndex;
			}
		}
		geometryGroup.Freeze();
		return _glyphs = geometryGroup;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		PrepareCharacters();
		if (!string.IsNullOrEmpty(_text))
		{
			return new Size(_text.Sum((char character) => (character != ':') ? _digitWidth : _colonWidth) + 4.0, _height);
		}
		return default;
	}

	protected override void OnRender(DrawingContext drawing)
	{
		base.OnRender(drawing);
		Geometry geometry = Glyphs();
		if (!geometry.IsEmpty())
		{
			if (Backdrop != null)
			{
				Rect backdropRegion = BackdropRegion;
				Rect viewbox = new Rect(backdropRegion.X + backdropRegion.Width * 0.025, backdropRegion.Y + backdropRegion.Height * 0.015, backdropRegion.Width * 0.95, backdropRegion.Height * 0.95);
				drawing.PushClip(geometry);
				drawing.DrawRectangle(new ImageBrush(Backdrop)
				{
					ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
					Viewbox = viewbox,
					Stretch = Stretch.Fill
				}, null, new Rect(RenderSize));
				drawing.Pop();
			}
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(0.2, 1.0)
			};
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(95, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0));
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(48, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.42));
			linearGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(125, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0));
			drawing.DrawGeometry(linearGradientBrush, null, geometry);
			drawing.PushClip(geometry);
			(double, int)[] entries = new (double, int)[4]
			{
				(0.09, 5),
				(0.065, 8),
				(0.04, 12),
				(0.02, 18)
			};
			for (int i = 0; i < entries.Length; i++)
			{
				(double, int) tuple = entries[i];
				double item = tuple.Item1;
				LinearGradientBrush brush = new LinearGradientBrush(Color.FromArgb((byte)tuple.Item2, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 55.0);
				drawing.DrawGeometry(null, new Pen(brush, FontSize * item), geometry);
			}
			RadialGradientBrush brush2 = new RadialGradientBrush(Color.FromArgb(180, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue))
			{
				Center = new Point(0.74, 0.98),
				GradientOrigin = new Point(0.74, 0.98),
				RadiusX = 0.45,
				RadiusY = 0.55
			};
			drawing.DrawRectangle(brush2, null, new Rect(RenderSize));
			drawing.Pop();
			LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(0.35, 1.0)
			};
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(90, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.5));
			linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromArgb(110, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0));
			drawing.DrawGeometry(null, new Pen(linearGradientBrush2, 0.5), geometry);
		}
	}
}
