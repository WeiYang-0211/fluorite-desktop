using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Launchpad.Views;

public partial class AppTileResources : ResourceDictionary, IComponentConnector, IStyleConnector
{
	public AppTileResources()
	{
		InitializeComponent();
	}

	internal static LaunchpadWindow? Owner(object sender)
	{
		if (sender is MenuItem container)
		{
			ItemsControl itemsControl;
			for (itemsControl = ItemsControl.ItemsControlFromItemContainer(container); itemsControl is MenuItem container2; itemsControl = ItemsControl.ItemsControlFromItemContainer(container2))
			{
			}
			if (itemsControl is ContextMenu { PlacementTarget: not null } contextMenu)
			{
				return Window.GetWindow(contextMenu.PlacementTarget) as LaunchpadWindow;
			}
		}
		if (!(sender is DependencyObject dependencyObject))
		{
			return null;
		}
		return Window.GetWindow(dependencyObject) as LaunchpadWindow;
	}

	private void OnAppButtonClick(object sender, RoutedEventArgs e)
	{
		Owner(sender)?.OnAppButtonClick(sender, e);
	}

	private void OnMenuRenameApp(object sender, RoutedEventArgs e)
	{
		Owner(sender)?.OnMenuRenameApp(sender, e);
	}

	private void OnMenuChangeIcon(object sender, RoutedEventArgs e)
	{
		Owner(sender)?.OnMenuChangeIcon(sender, e);
	}

	private void OnMenuRenameFolder(object sender, RoutedEventArgs e)
	{
		Owner(sender)?.OnMenuRenameFolder(sender, e);
	}
}
