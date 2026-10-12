using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using Launchpad.Models;

namespace Launchpad.Services;

internal static class PersistenceSnapshot
{
	private static readonly PropertyInfo[] SettingsProperties = (from p in typeof(AppSettings).GetProperties()
		where p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null
		select p).ToArray();

	public static T Capture<T>(Func<T> capture)
	{
		Dispatcher dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null || dispatcher.CheckAccess())
		{
			return capture();
		}
		return dispatcher.Invoke(capture);
	}

	public static AppItem App(AppItem source)
	{
		return new AppItem
		{
			Id = source.Id,
			Name = source.Name,
			ExecutablePath = source.ExecutablePath,
			Arguments = source.Arguments,
			IconPath = source.IconPath,
			IconIndex = source.IconIndex,
			IconOverridePath = source.IconOverridePath,
			CustomName = source.CustomName,
			Source = source.Source,
			Category = source.Category,
			IsDock = source.IsDock,
			SystemToolGroupingHandled = source.SystemToolGroupingHandled,
			DockIndex = source.DockIndex,
			SortIndex = source.SortIndex,
			LastUsedTime = source.LastUsedTime,
			IsFolder = source.IsFolder,
			IsWidget = source.IsWidget,
			WidgetType = source.WidgetType,
			FolderId = source.FolderId,
			IsHidden = source.IsHidden,
			Children = new ObservableCollection<AppItem>(source.Children.Select(App))
		};
	}

	public static FolderItem Folder(FolderItem source)
	{
		return new FolderItem
		{
			Id = source.Id,
			Name = source.Name,
			SortIndex = source.SortIndex,
			AppIds = new List<string>(source.AppIds)
		};
	}

	public static AppSettings Settings(AppSettings source)
	{
		AppSettings appSettings = new AppSettings();
		PropertyInfo[] settingsProperties = SettingsProperties;
		foreach (PropertyInfo propertyInfo in settingsProperties)
		{
			propertyInfo.SetValue(appSettings, propertyInfo.GetValue(source));
		}
		return appSettings;
	}
}
