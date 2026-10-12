using System.Windows;
using System.Windows.Controls;
using Launchpad.Models;
using Launchpad.Services;
using Launchpad.ViewModels;

namespace Launchpad.Views;

internal static class OpenWindowMenu
{
	internal static ContextMenu Create(MainViewModel model, AppItem app, FrameworkElement owner)
	{
		ContextMenu contextMenu = new ContextMenu();
		if (owner.TryFindResource("MacContextMenuStyle") is Style style)
		{
			contextMenu.Style = style;
		}
		foreach (OpenAppWindow window in model.OpenWindowsFor(app))
		{
			MenuItem menuItem = new MenuItem
			{
				Header = window.Title
			};
			if (owner.TryFindResource("MacMenuItemStyle") is Style style2)
			{
				menuItem.Style = style2;
			}
			menuItem.Click += (object _, RoutedEventArgs _) =>
			{
				model.RestoreSpecificOpenWindow(window);
			};
			contextMenu.Items.Add(menuItem);
		}
		return contextMenu;
	}
}
