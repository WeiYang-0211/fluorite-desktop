using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Launchpad.Models;
using Microsoft.Win32;

namespace Launchpad.Infrastructure;

public static class ThemeManager
{
	private const string Prefix = "/Fluorite;component/Resources/Theme.";

	public static AppTheme Requested { get; private set; } = AppTheme.Dark;

	public static AppTheme Effective { get; private set; } = AppTheme.Dark;

	public static event Action? Applied;

	public static double VeilFloor()
	{
		object target = Application.Current.TryFindResource("T.VeilFloor");
		if (target is double)
		{
			return (double)target;
		}
		return 0.0;
	}

	public static void Apply(AppTheme requested)
	{
		Requested = requested;
		AppTheme appTheme = ((requested != AppTheme.System) ? requested : ((!IsSystemLight()) ? AppTheme.Dark : AppTheme.Light));
		App.Log($"[THEME] requested={requested} effective={appTheme} onScreen={Effective}");
		int pageNumber = IndexOfTheme();
		if (pageNumber < 0 || Effective != appTheme)
		{
			string text = appTheme switch
			{
				AppTheme.Light => "/Fluorite;component/Resources/Theme.Light.xaml", 
				AppTheme.HighContrast => "/Fluorite;component/Resources/Theme.HighContrast.xaml", 
				_ => "/Fluorite;component/Resources/Theme.MacDark.xaml", 
			};
			ResourceDictionary resourceDictionary = Load(text);
			Collection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
			if (pageNumber >= 0)
			{
				mergedDictionaries[pageNumber] = resourceDictionary;
			}
			else
			{
				mergedDictionaries.Add(resourceDictionary);
			}
			Effective = appTheme;
			App.Log($"[THEME] requested={requested} effective={appTheme} ({text})");
			Applied?.Invoke();
		}
	}

	public static ResourceDictionary LightPalette()
	{
		return Load("/Fluorite;component/Resources/Theme.Light.xaml");
	}

	private static ResourceDictionary Load(string file)
	{
		return new ResourceDictionary
		{
			Source = new Uri(file, UriKind.Relative)
		};
	}

	public static AppTheme PeekStoredTheme()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fluorite", "settings.json");
			if (!File.Exists(path))
			{
				return AppTheme.Dark;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			if (jsonDocument.RootElement.TryGetProperty("Theme", out var value) && value.TryGetInt32(out var failure) && Enum.IsDefined(typeof(AppTheme), failure))
			{
				return (AppTheme)failure;
			}
		}
		catch (Exception ex)
		{
			App.Log("[THEME] stored theme unreadable: " + ex.Message);
		}
		return AppTheme.Dark;
	}

	private static int IndexOfTheme()
	{
		Collection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
		for (int i = 0; i < mergedDictionaries.Count; i++)
		{
			string text = mergedDictionaries[i]?.Source?.OriginalString;
			if (text != null && text.Contains("/Fluorite;component/Resources/Theme."))
			{
				return i;
			}
		}
		return -1;
	}

	private static bool IsSystemLight()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
			return registryKey?.GetValue("AppsUseLightTheme") is int pageNumber && pageNumber != 0;
		}
		catch (Exception ex)
		{
			App.Log("[THEME] AppsUseLightTheme unreadable: " + ex.Message);
			return false;
		}
	}
}
