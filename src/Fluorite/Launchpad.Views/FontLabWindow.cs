using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launchpad.Views;

public class FontLabWindow : Window
{
	private const string PuHuiTi = "pack://application:,,,/Assets/Fonts/#Alibaba PuHuiTi 3.0";

	private static readonly Uri FontDir = new Uri("file:///C:/doc/code/winStart/Tools/font-src/");

	private static readonly FontFamily Noto = new FontFamily(FontDir, "./#Noto Sans SC");

	private static readonly FontFamily NotoMedium = new FontFamily(FontDir, "./#Noto Sans SC Medium");

	private static readonly FontFamily Family = new FontFamily("pack://application:,,,/Assets/Fonts/#Alibaba PuHuiTi 3.0");

	private const string TitleText = "重新扫描应用";

	private const string HintText = "扫描开始菜单、桌面与商店应用，补齐新装的程序";

	public FontLabWindow()
	{
		Title = "FontLab";
		Width = 1000.0;
		Height = 880.0;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		Background = Brushes.White;
		UseLayoutRounding = true;
		Topmost = true;
		ShowInTaskbar = true;
		StackPanel stackPanel = (StackPanel)(Content = new StackPanel
		{
			Margin = new Thickness(28.0, 20.0, 28.0, 20.0)
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "直接在屏幕上看，不要看截图",
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold,
			FontFamily = Family,
			Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
		});
		AddRow(stackPanel, "E  普惠体 current: Display / Grayscale / 17 SemiBold", Family, Family, TextFormattingMode.Display, TextRenderingMode.Grayscale, 17.0, FontWeights.SemiBold, 14.0, FontWeights.Medium);
		AddRow(stackPanel, "N2  思源: Display / Grayscale / 18 Medium", NotoMedium, Noto, TextFormattingMode.Display, TextRenderingMode.Grayscale, 18.0, FontWeights.Normal, 14.0, FontWeights.Normal);
		AddRow(stackPanel, "N3  思源: Display / Grayscale / 19 Medium", NotoMedium, Noto, TextFormattingMode.Display, TextRenderingMode.Grayscale, 19.0, FontWeights.Normal, 14.0, FontWeights.Normal);
		AddRow(stackPanel, "N4  思源: Display / Grayscale / 18 Bold", Noto, Noto, TextFormattingMode.Display, TextRenderingMode.Grayscale, 18.0, FontWeights.Bold, 14.0, FontWeights.Normal);
		AddRow(stackPanel, "N5  思源: ClearType / 18 Medium", NotoMedium, Noto, TextFormattingMode.Display, TextRenderingMode.Auto, 18.0, FontWeights.Normal, 14.0, FontWeights.Normal);
		AddRow(stackPanel, "N6  思源: ClearType / 18 Bold", Noto, Noto, TextFormattingMode.Display, TextRenderingMode.Auto, 18.0, FontWeights.Bold, 14.0, FontWeights.Normal);
	}

	private static void AddRow(Panel parent, string label, FontFamily titleFamily, FontFamily hintFamily, TextFormattingMode fmt, TextRenderingMode render, double titleSize, FontWeight titleWeight, double hintSize, FontWeight hintWeight)
	{
		SolidColorBrush foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31));
		SolidColorBrush foreground2 = new SolidColorBrush(Color.FromRgb(85, 96, 110));
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(20.0, 14.0, 20.0, 14.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		};
		TextOptions.SetTextFormattingMode(border, fmt);
		TextOptions.SetTextRenderingMode(border, render);
		AutomationProperties.SetAutomationId(border, label);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			FontSize = 11.0,
			FontFamily = Family,
			FontWeight = FontWeights.Medium,
			Foreground = foreground2,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "重新扫描应用",
			FontFamily = titleFamily,
			FontSize = titleSize,
			FontWeight = titleWeight,
			Foreground = foreground
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "扫描开始菜单、桌面与商店应用，补齐新装的程序",
			FontFamily = hintFamily,
			FontSize = hintSize,
			FontWeight = hintWeight,
			Foreground = foreground2,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		});
		border.Child = stackPanel;
		parent.Children.Add(border);
	}
}
