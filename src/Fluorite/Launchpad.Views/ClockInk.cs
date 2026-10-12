using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Launchpad.Views;

public static class ClockInk
{
	public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterAttached("Color", typeof(string), typeof(ClockInk), new PropertyMetadata("", Changed));

	private static readonly object DefaultInk = new object();

	private static readonly DependencyProperty OriginalProperty = DependencyProperty.RegisterAttached("Original", typeof(object), typeof(ClockInk));

	public static void SetColor(DependencyObject target, string value)
	{
		target.SetValue(ColorProperty, value);
	}

	public static string GetColor(DependencyObject target)
	{
		return (string)target.GetValue(ColorProperty);
	}

	private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
	{
		if (!(target is TextBlock textBlock))
		{
			return;
		}
		if (textBlock is ClockTextBlock)
		{
			textBlock.CoerceValue(TextBlock.ForegroundProperty);
			return;
		}
		object folderEntry = textBlock.GetValue(OriginalProperty);
		if (folderEntry == null)
		{
			folderEntry = BindingOperations.GetBindingBase(textBlock, TextBlock.ForegroundProperty) ?? textBlock.ReadLocalValue(TextBlock.ForegroundProperty);
			if (folderEntry == DependencyProperty.UnsetValue)
			{
				folderEntry = DefaultInk;
			}
			textBlock.SetValue(OriginalProperty, folderEntry);
		}
		Brush brush = null;
		try
		{
			if (!string.IsNullOrWhiteSpace(e.NewValue as string))
			{
				brush = (Brush)new BrushConverter().ConvertFromString((string)e.NewValue);
			}
		}
		catch
		{
		}
		if (brush != null)
		{
			textBlock.SetCurrentValue(TextBlock.ForegroundProperty, brush);
		}
		else if (folderEntry is BindingBase binding)
		{
			BindingOperations.SetBinding(textBlock, TextBlock.ForegroundProperty, binding);
		}
		else if (folderEntry == DefaultInk)
		{
			textBlock.ClearValue(TextBlock.ForegroundProperty);
		}
		else
		{
			textBlock.SetValue(TextBlock.ForegroundProperty, folderEntry);
		}
	}
}
