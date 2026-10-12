using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Launchpad.Views;

public sealed class ConfirmDialog : Window
{
	public bool Confirmed { get; private set; }

	public ConfirmDialog(string title, string message, string? detail = null, string confirmText = "确定", string cancelText = "取消", bool destructive = false)
	{
		Window window = DialogKit.OwnerOrDefault(null, out var centerScreen);
		DialogKit.Configure(this, centerScreen);
		if (window != null)
		{
			Owner = window;
		}
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)DialogKit.Title(title),
				(UIElement)DialogKit.Body(message, 12.5, dim: false)
			}
		};
		if (!string.IsNullOrWhiteSpace(detail))
		{
			stackPanel.Children.Add(DialogKit.Body(detail));
		}
		Button button = DialogKit.Push(cancelText, primary: false);
		button.Click += (object s, RoutedEventArgs e) =>
		{
			Close();
		};
		Button button2 = DialogKit.Push(confirmText, primary: true, destructive);
		button2.IsDefault = true;
		button2.Click += (object s, RoutedEventArgs e) =>
		{
			Confirmed = true;
			Close();
		};
		stackPanel.Children.Add(DialogKit.Buttons(button, button2));
		Border border = DialogKit.Card();
		border.Child = stackPanel;
		Content = border;
		PreviewKeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	public static bool Ask(string title, string message, string? detail = null, string confirmText = "确定", string cancelText = "取消", bool destructive = false)
	{
		ConfirmDialog confirmDialog = new ConfirmDialog(title, message, detail, confirmText, cancelText, destructive);
		confirmDialog.ShowDialog();
		return confirmDialog.Confirmed;
	}
}
