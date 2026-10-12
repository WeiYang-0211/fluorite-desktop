using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Launchpad.Views;

public static class SettingsMoreMenu
{
	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SettingsMoreMenu), new PropertyMetadata(false, Changed));

	public static bool GetEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(EnabledProperty);
	}

	public static void SetEnabled(DependencyObject element, bool value)
	{
		element.SetValue(EnabledProperty, value);
	}

	private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
	{
		if (element is Button button)
		{
			if ((bool)args.OldValue)
			{
				button.Click -= Open;
			}
			if ((bool)args.NewValue)
			{
				button.Click += Open;
			}
		}
	}

	private static void Open(object sender, RoutedEventArgs args)
	{
		if (sender is Button { ContextMenu: { } contextMenu } button)
		{
			contextMenu.PlacementTarget = button;
			contextMenu.Placement = PlacementMode.Bottom;
			contextMenu.IsOpen = true;
			args.Handled = true;
		}
	}
}
