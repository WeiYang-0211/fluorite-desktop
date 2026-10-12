using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launchpad.Interop;
using Launchpad.Models;

namespace Launchpad.Services;

internal static class WebShortcutIcon
{
	internal static bool IsWeb(AppItem app)
	{
		string text = app.ExecutablePath;
		string commandLinePath = app.Arguments;
		if (IsWebAddress(text))
		{
			return true;
		}
		if (text.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
		{
			(text, commandLinePath, _, _) = ShellLinkHelper.ResolveShortcut(text);
		}
		if (text.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				bool validated = false;
				foreach (string item in File.ReadLines(text))
				{
					string text3 = item.Trim();
					if (text3.StartsWith('['))
					{
						validated = text3.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase);
					}
					else if (validated && text3.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
					{
						string text4 = text3;
						return IsWebAddress(text4.Substring(4, text4.Length - 4).Trim());
					}
				}
			}
			catch
			{
			}
			return false;
		}
		if (IsWebAddress(text))
		{
			return true;
		}
		bool flag2;
		switch (Path.GetFileNameWithoutExtension(text).ToLowerInvariant())
		{
		case "chrome":
		case "msedge":
		case "firefox":
		case "vivaldi":
		case "brave":
		case "opera":
		case "iexplore":
			flag2 = true;
			break;
		default:
			flag2 = false;
			break;
		}
		if (!flag2)
		{
			return false;
		}
		string[] entries = (commandLinePath ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text5 in entries)
		{
			if (!IsWebAddress(text5.Trim('"')))
			{
				if (!text5.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text4 = text5;
				if (!IsWebAddress(text4.Substring(6, text4.Length - 6).Trim('"')))
				{
					continue;
				}
			}
			return true;
		}
		return false;
	}

	internal static bool IsWebAddress(string value)
	{
		if (Uri.TryCreate(value, UriKind.Absolute, out Uri result))
		{
			if (!(result.Scheme == Uri.UriSchemeHttp))
			{
				return result.Scheme == Uri.UriSchemeHttps;
			}
			return true;
		}
		return false;
	}

	internal static BitmapSource Render()
	{
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			LinearGradientBrush brush = new LinearGradientBrush(Color.FromRgb(48, 181, 235), Color.FromRgb(53, 87, 222), 45.0);
			drawingContext.DrawRoundedRectangle(brush, null, new Rect(8.0, 8.0, 240.0, 240.0), 54.0, 54.0);
			Pen pen = new Pen(Brushes.White, 9.0);
			drawingContext.DrawEllipse(null, pen, new Point(128.0, 128.0), 73.0, 73.0);
			drawingContext.DrawEllipse(null, pen, new Point(128.0, 128.0), 33.0, 73.0);
			drawingContext.DrawLine(pen, new Point(55.0, 128.0), new Point(201.0, 128.0));
			drawingContext.DrawLine(pen, new Point(65.0, 94.0), new Point(191.0, 94.0));
			drawingContext.DrawLine(pen, new Point(65.0, 162.0), new Point(191.0, 162.0));
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(256, 256, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}
}
