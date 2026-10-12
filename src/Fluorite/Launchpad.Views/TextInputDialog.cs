using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Launchpad.Views;

public class TextInputDialog : Window
{
	private readonly TextBox _input;

	public string? Value { get; private set; }

	public TextInputDialog(Window owner, string title, string initialText)
	{
		Window window = DialogKit.OwnerOrDefault(owner, out var centerScreen);
		DialogKit.Configure(this, centerScreen);
		if (window != null)
		{
			Owner = window;
		}
		StackPanel stackPanel = new StackPanel
		{
			Children = { (UIElement)DialogKit.Title(title) }
		};
		_input = new TextBox
		{
			Text = initialText,
			FontSize = 12.5,
			Padding = new Thickness(10.0, 8.0, 10.0, 8.0),
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			BorderThickness = new Thickness(1.0)
		};
		_input.SetResourceReference(Control.ForegroundProperty, "T.Fg_F5");
		_input.SetResourceReference(TextBoxBase.CaretBrushProperty, "T.Fg_F5");
		_input.SetResourceReference(Control.BackgroundProperty, "T.Rp_14");
		_input.SetResourceReference(Control.BorderBrushProperty, "T.Fg_1A");
		_input.SetResourceReference(Control.FontFamilyProperty, "AppFont");
		stackPanel.Children.Add(_input);
		Button button = DialogKit.Push("取消", primary: false);
		button.Click += (object s, RoutedEventArgs e) =>
		{
			DialogResult = false;
			Close();
		};
		Button button2 = DialogKit.Push("确定", primary: true);
		button2.IsDefault = true;
		button2.Click += (object s, RoutedEventArgs e) =>
		{
			Accept();
		};
		stackPanel.Children.Add(DialogKit.Buttons(button, button2));
		Border border = DialogKit.Card(360.0);
		border.Child = stackPanel;
		Content = border;
		PreviewKeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				DialogResult = false;
				Close();
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				Accept();
				e.Handled = true;
			}
		};
		Loaded += (object s, RoutedEventArgs e) =>
		{
			_input.Focus();
			_input.SelectAll();
		};
	}

	private void Accept()
	{
		if (!string.IsNullOrWhiteSpace(_input.Text))
		{
			Value = _input.Text.Trim();
			DialogResult = true;
			Close();
		}
	}
}
