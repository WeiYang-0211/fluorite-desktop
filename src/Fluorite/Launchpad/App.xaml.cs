using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using Launchpad.Infrastructure;
using Launchpad.Models;
using Launchpad.Services;
using Launchpad.Services.Update;
using Launchpad.ViewModels;
using Launchpad.Views;

namespace Launchpad;

public partial class App : Application
{
	private MainViewModel? _mainViewModel;

	private LaunchpadWindow? _launchpadWindow;

	private DesktopDockWindow? _desktopDockWindow;

	private StartupLoadingWindow? _startupLoadingWindow;

	private IHotkeyService? _hotkeyService;

	private IStorageService? _storageService;

	private IAppScannerService? _scannerService;

	private IStartupService? _startupService;

	private ITrayIconService? _trayIconService;

	private HotCornerService? _hotCornerService;

	private SettingsViewModel? _settingsViewModel;

	private nint _hotkeyWindow;

	private SingleInstanceService? _singleInstanceService;

	private UpdateService? _updates;

	private DateTime _lastSaveFailureNotice;

	private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fluorite.log");

	private static readonly BufferedLog DiagnosticLog = new BufferedLog(LogFile);

	private DateTime _ignoreHotkeyUntil;

	public static IStorageService? Storage => (Application.Current as App)?._storageService;

	public static UpdateService? Updates => (Application.Current as App)?._updates;

	public App()
	{
		ShutdownMode = ShutdownMode.OnExplicitShutdown;
		AppDomain.CurrentDomain.ProcessExit += (object? s, EventArgs e) =>
		{
			Log("ProcessExit event fired! Stack:\r\n" + Environment.StackTrace);
			DiagnosticLog.Dispose();
		};
	}

	public static void Log(string message)
	{
		DiagnosticLog.Write(message);
	}

	public static bool TryAdoptHotkey(uint modifiers, uint key, out string error)
	{
		App app = Application.Current as App;
		AppSettings appSettings = app?._mainViewModel?.Settings;
		if (app?._hotkeyService == null || appSettings == null)
		{
			error = "快捷键服务尚未就绪";
			return false;
		}
		if (app._hotkeyService.Register(app._hotkeyWindow, modifiers, key, out error))
		{
			appSettings.HotkeyModifiers = modifiers;
			appSettings.HotkeyKey = key;
			appSettings.HotkeyDisplay = FormatHotkey(modifiers, key);
			Log("Hotkey adopted: " + appSettings.HotkeyDisplay);
			return true;
		}
		Log($"Hotkey rejected ({modifiers}+{key}): {error}");
		app.ApplyConfiguredHotkey();
		return false;
	}

	private void ApplyConfiguredHotkey()
	{
		if (_hotkeyService == null || _mainViewModel == null)
		{
			return;
		}
		AppSettings settings = _mainViewModel.Settings;
		if (_hotkeyService.Register(_hotkeyWindow, settings.HotkeyModifiers, settings.HotkeyKey, out string errorMessage))
		{
			settings.HotkeyDisplay = FormatHotkey(settings.HotkeyModifiers, settings.HotkeyKey);
			return;
		}
		Log("Stored hotkey could not be registered (" + errorMessage + "); falling back to Alt + Space");
		if (_hotkeyService.Register(_hotkeyWindow, 1u, 32u, out errorMessage))
		{
			settings.HotkeyDisplay = FormatHotkey(1u, 32u);
		}
	}

	public static string FormatHotkey(uint modifiers, uint key)
	{
		string text = string.Empty;
		if ((modifiers & 2) != 0)
		{
			text += "Ctrl + ";
		}
		if ((modifiers & 1) != 0)
		{
			text += "Alt + ";
		}
		if ((modifiers & 4) != 0)
		{
			text += "Shift + ";
		}
		if ((modifiers & 8) != 0)
		{
			text += "Win + ";
		}
		return text + KeyLabel(KeyInterop.KeyFromVirtualKey((int)key));
	}

	private static string KeyLabel(Key key)
	{
		switch (key)
		{
		case Key.D0:
		case Key.D1:
		case Key.D2:
		case Key.D3:
		case Key.D4:
		case Key.D5:
		case Key.D6:
		case Key.D7:
		case Key.D8:
		case Key.D9:
			return ((char)(48 + (key - 34))).ToString();
		case Key.OemMinus:
			return "-";
		case Key.OemPlus:
			return "=";
		case Key.Oem4:
			return "[";
		case Key.Oem6:
			return "]";
		case Key.Oem5:
			return "\\";
		case Key.Oem1:
			return ";";
		case Key.Oem7:
			return "'";
		case Key.OemComma:
			return ",";
		case Key.OemPeriod:
			return ".";
		case Key.Oem2:
			return "/";
		case Key.Oem3:
			return "`";
		case Key.Return:
			return "Enter";
		case Key.Back:
			return "Backspace";
		default:
			return key.ToString();
		}
	}

	protected override async void OnStartup(StartupEventArgs e)
	{
		AptabaseService.Current.Init();
		AptabaseService.Current.TrackEvent("app_started");
		AppDomain.CurrentDomain.UnhandledException += (object s, UnhandledExceptionEventArgs args) =>
		{
			Log($"Unhandled AppDomain Exception: {args.ExceptionObject}");
			if (args.ExceptionObject is Exception exception)
			{
				AptabaseService.Current.TrackError(exception, fatal: true, "crash");
			}
		};
		DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs args) =>
		{
			Log($"Dispatcher Exception: {args.Exception}");
			AptabaseService.Current.TrackError(args.Exception, fatal: false, "unhandled");
			args.Handled = true;
		};
		TaskScheduler.UnobservedTaskException += (object? s, UnobservedTaskExceptionEventArgs args) =>
		{
			Log($"Unobserved Task Exception: {args.Exception}");
			AptabaseService.Current.TrackError(args.Exception, fatal: false, "taskException");
		};
		Log("Starting Fluorite application...");
		if (_singleInstanceService == null)
		{
			_singleInstanceService = new SingleInstanceService();
			if (!_singleInstanceService.CheckIsFirstInstance())
			{
				Log("Secondary instance detected.");
				if (StartupService.IsTrayStartup(e.Args))
				{
					Shutdown();
					return;
				}
				string text = ExtractAddPath(e.Args);
				bool result;
				if (!string.IsNullOrEmpty(text))
				{
					Log("Forwarding add command to running instance: " + text);
					result = _singleInstanceService.SendMessageToFirstInstanceAsync("ADD|" + text).GetAwaiter().GetResult();
				}
				else
				{
					Log("Forwarding show command to running instance.");
					result = _singleInstanceService.SendMessageToFirstInstanceAsync("SHOW").GetAwaiter().GetResult();
				}
				if (result)
				{
					Log("IPC message delivered successfully, secondary instance exiting.");
					Environment.Exit(0);
					return;
				}
				Log("Could not reach existing instance (may have hung or exited). Taking over as primary instance...");
			}
		}
		_singleInstanceService.StartServer();
		_singleInstanceService.FileAddRequested += (string path) =>
		{
			Dispatcher.InvokeAsync((Func<Task>)(async () =>
			{
				if (_mainViewModel != null)
				{
					bool flag2 = await _mainViewModel.AddSingleAppFromPathAsync(path);
					string commandLinePath = Path.GetFileName(path);
					if (string.IsNullOrEmpty(commandLinePath))
					{
						commandLinePath = path;
					}
					if (flag2)
					{
						_trayIconService?.ShowBalloonNotification("Fluorite", "已添加到 Fluorite: " + commandLinePath);
					}
					else
					{
						_trayIconService?.ShowBalloonNotification("Fluorite", "已存在，已跳过: " + commandLinePath);
					}
				}
			}));
		};
		_singleInstanceService.ShowRequested += () =>
		{
			Dispatcher.Invoke(() =>
			{
				Log("[IPC] ShowRequested: opening Launchpad and ensuring foreground");
				if (_launchpadWindow != null && _mainViewModel != null)
				{
					_launchpadWindow.WindowState = WindowState.Normal;
					_launchpadWindow.Topmost = true;
					_mainViewModel.OpenLaunchpad();
					_launchpadWindow.EnsureForeground("ipc-show");
				}
			});
		};
		base.OnStartup(e);
		if (Array.IndexOf(e.Args, "--fontlab") >= 0)
		{
			FontLabWindow fontLabWindow = new FontLabWindow();
			fontLabWindow.Closed += (object? s, EventArgs ev) =>
			{
				Shutdown();
			};
			fontLabWindow.Show();
			return;
		}
		if (!StartupService.IsTrayStartup(e.Args))
		{
			ThemeManager.Apply(ThemeManager.PeekStoredTheme());
			_startupLoadingWindow = new StartupLoadingWindow();
			_startupLoadingWindow.Show();
			await Dispatcher.InvokeAsync(() =>
			{
			}, DispatcherPriority.ContextIdle);
			await Task.Delay(40);
		}
		try
		{
			_storageService = new StorageService();
			((StorageService)_storageService).SaveFailed += (string file) =>
			{
				Dispatcher.BeginInvoke((Action)(() =>
				{
					if (!(DateTime.UtcNow - _lastSaveFailureNotice < TimeSpan.FromSeconds(30.0)))
					{
						_lastSaveFailureNotice = DateTime.UtcNow;
						_trayIconService?.ShowBalloonNotification("Fluorite 保存失败", "未能保存 " + file + "，请检查磁盘空间和目录权限。当前更改尚未保存。重试刚才的操作可再次保存。");
					}
				}));
			};
			_scannerService = new AppScannerService();
			_startupService = new StartupService();
			_startupService.MigrateLegacyRunValue();
			IconService iconService = new IconService();
			AppLauncherService launcherService = new AppLauncherService(_scannerService.SteamScanner);
			SearchService searchService = new SearchService();
			_updates = new UpdateService(_storageService);
			_updates.AutoCheckEnabled = () => _mainViewModel?.Settings.AutoCheckUpdates ?? true;
			WidgetService widgetService = new WidgetService(new SystemMonitorService());
			_mainViewModel = new MainViewModel(_storageService, _scannerService, iconService, launcherService, searchService, widgetService);
			_settingsViewModel = new SettingsViewModel(_storageService, _startupService, _scannerService, _mainViewModel, _updates);
			_launchpadWindow = new LaunchpadWindow(_mainViewModel, _settingsViewModel);
			_launchpadWindow.HoldInitialOpen();
			MainWindow = _launchpadWindow;
			Log("Window created, ensuring handle...");
			nint pageNumber = new WindowInteropHelper(_launchpadWindow).EnsureHandle();
			Log($"Handle created: 0x{((IntPtr)pageNumber).ToInt64():X}");
			_hotkeyService = new HotkeyService();
			_hotkeyService.HotkeyPressed += OnHotkeyPressed;
			_hotkeyWindow = pageNumber;
			bool validated = _hotkeyService.Register(pageNumber, _mainViewModel.Settings.HotkeyModifiers, _mainViewModel.Settings.HotkeyKey, out string errorMessage);
			Log("Hotkey registration: " + (validated ? "SUCCESS" : ("FAILED (" + errorMessage + ")")));
			_trayIconService = new TrayIconService();
			_trayIconService.OpenRequested += () =>
			{
				Dispatcher.Invoke(() =>
				{
					_mainViewModel?.ToggleLaunchpad();
				});
			};
			_trayIconService.SettingsRequested += OpenSettings;
			_trayIconService.RescanRequested += () =>
			{
				Dispatcher.Invoke((Func<Task>)(async () =>
				{
					await _mainViewModel.RescanApplicationsAsync();
				}));
			};
			_trayIconService.AddApplicationRequested += () =>
			{
				Dispatcher.Invoke((Func<Task>)(async () =>
				{
					await _mainViewModel.AddApplicationByPickingAsync();
				}));
			};
			_trayIconService.ToggleStartupRequested += () =>
			{
				Dispatcher.Invoke(() =>
				{
					if (_startupService != null && _mainViewModel != null)
					{
						bool flag2 = !_startupService.IsStartupEnabled();
						_startupService.SetStartup(flag2);
						_mainViewModel.Settings.LaunchAtStartup = flag2;
						_storageService?.SaveSettingsAsync(_mainViewModel.Settings);
					}
				});
			};
			_trayIconService.ExitRequested += () =>
			{
				Dispatcher.Invoke(Shutdown);
			};
			_trayIconService.CheckUpdateRequested += () =>
			{
				Dispatcher.InvokeAsync((Func<Task>)(async () =>
				{
					Log("[UPDATE] tray check: " + await CheckForUpdatesNowAsync(showBalloon: true));
				}));
			};
			_trayIconService.SetStatusContext(() => _startupService?.IsStartupEnabled() ?? false, () => _mainViewModel?.Settings.HotkeyDisplay ?? "Alt + Space");
			_trayIconService.SetUpdateContext(() =>
			{
				if (_updates == null)
				{
					return string.Empty;
				}
				return (!(_updates.Pending != null)) ? ("当前 v" + _updates.CurrentVersion) : "有新版本可用";
			});
			_trayIconService.Initialize(pageNumber);
			_updates.UpdateFound += (UpdateInfo info) =>
			{
				ShowUpdateWhenIdleAsync(info);
			};
			_hotCornerService = new HotCornerService(() => !_mainViewModel.IsOpen && _mainViewModel.Settings.EnableHotCorner, () => _mainViewModel.Settings.HotCorner, () => _mainViewModel.Settings.PauseHotCornerInFullscreen, () => _mainViewModel.AllApps);
			_hotCornerService.CornerTriggered += () =>
			{
				Dispatcher.Invoke(() =>
				{
					if (_mainViewModel != null && !_mainViewModel.IsOpen)
					{
						_mainViewModel.OpenLaunchpad();
					}
				});
			};
			Dispatcher.BeginInvoke((Func<Task>)(async () =>
			{
				try
				{
					_startupLoadingWindow?.SetStatus("正在读取设置…");
					await _settingsViewModel.LoadSettingsAsync();
					_launchpadWindow.MarkSettingsLoaded();
					ApplyConfiguredHotkey();
					_startupLoadingWindow?.SetStatus("正在加载应用与图标…");
					await _mainViewModel.InitializeAsync();
					_mainViewModel.Settings.ShowOpenAppsInDock = false;
					_desktopDockWindow = new DesktopDockWindow(_mainViewModel, _launchpadWindow);
					string commandLinePath = ExtractAddPath(e.Args);
					if (!string.IsNullOrEmpty(commandLinePath))
					{
						Log("Handling initial add from command line: " + commandLinePath);
						await _mainViewModel.AddSingleAppFromPathAsync(commandLinePath);
					}
					_startupLoadingWindow?.SetStatus("正在准备界面…");
					await _launchpadWindow.PrepareStartupAsync();
					if (_startupLoadingWindow != null)
					{
						await _startupLoadingWindow.DismissAsync();
					}
					_startupLoadingWindow = null;
					if (_launchpadWindow.HasStartupOpenRequest ? _mainViewModel.IsOpen : StartupService.ShouldShowInitially(e.Args, _mainViewModel.Settings))
					{
						Log("Opening Launchpad initially...");
						_mainViewModel.OpenLaunchpad();
						_launchpadWindow.Show();
						_launchpadWindow.WindowState = WindowState.Normal;
						_launchpadWindow.Topmost = true;
						_launchpadWindow.EnsureForeground("initial-open");
						_launchpadWindow.ShowPendingStartupSettings();
					}
					else
					{
						Log("Login startup: ready in tray, window stays hidden.");
					}
					Task.Run(async () =>
					{
						await Task.Delay(4000);
						await _updates.StartupAsync();
					});
					if (Array.IndexOf(e.Args, "--settings") >= 0)
					{
						Log("Opening the preferences sheet from the command line");
						_launchpadWindow.PinOpen = true;
						_launchpadWindow.ShowSettingsPanel();
						int settingIndex = Array.IndexOf(e.Args, "--settings");
						if (settingIndex + 1 < e.Args.Length)
						{
							_settingsViewModel.ActiveSection = e.Args[settingIndex + 1];
						}
					}
					if (Array.IndexOf(e.Args, "--folder") >= 0)
					{
						await Task.Delay(1400);
						AppItem appItem = null;
						for (int targetPage = 1; targetPage <= _mainViewModel.TotalPages; targetPage++)
						{
							if (appItem != null)
							{
								break;
							}
							_mainViewModel.CommitPageChange(targetPage);
							foreach (AppItem currentPageApp in _mainViewModel.CurrentPageApps)
							{
								if (currentPageApp.IsFolder)
								{
									appItem = currentPageApp;
									break;
								}
							}
						}
						if (appItem != null)
						{
							Log($"Opening folder '{appItem.Name}' from the command line on page {_mainViewModel.CurrentPage}");
							_launchpadWindow.PinOpen = true;
							_mainViewModel.OpenFolder(appItem);
						}
						else
						{
							Log("--folder: no folder anywhere in the library");
						}
					}
				}
				catch (Exception failure)
				{
					Log($"Error in Dispatcher async initialization: {failure}");
					_startupLoadingWindow?.Close();
					_startupLoadingWindow = null;
					MessageBox.Show("启动未完成，请重新打开 Fluorite。", "Fluorite", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			}));
		}
		catch (Exception value)
		{
			Log($"Error in OnStartup: {value}");
			_startupLoadingWindow?.Close();
			_startupLoadingWindow = null;
			MessageBox.Show("启动未完成，请重新打开 Fluorite。", "Fluorite", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OpenSettings()
	{
		Dispatcher.Invoke(() =>
		{
			if (_launchpadWindow != null && _mainViewModel != null)
			{
				if (!_mainViewModel.IsOpen)
				{
					_mainViewModel.OpenLaunchpad();
				}
				_launchpadWindow.ShowSettingsPanel();
			}
		});
	}

	public static void OpenSettingsPage(string section)
	{
		App app = Application.Current as App;
		if (app?._launchpadWindow == null || app._mainViewModel == null || app._settingsViewModel == null)
		{
			return;
		}
		app.Dispatcher.Invoke(() =>
		{
			if (!app._mainViewModel.IsOpen)
			{
				app._mainViewModel.OpenLaunchpad();
			}
			app._launchpadWindow.ShowSettingsPanel();
			app._settingsViewModel.ActiveSection = section;
		});
	}

	private async Task ShowUpdateWhenIdleAsync(UpdateInfo info)
	{
		await Dispatcher.InvokeAsync((Func<Task>)(async () =>
		{
			UpdateService updates = _updates;
			if (updates != null)
			{
				while (_launchpadWindow != null && !_launchpadWindow.CanPresentModal)
				{
					await Task.Delay(100);
				}
				if (updates.Pending?.Version == info.Version && !updates.IsPostponed(info.Version))
				{
					ShowOwnedDialogOnUi(new UpdateDialog(info, updates));
				}
			}
		})).Task.Unwrap();
	}

	private void ShowOwnedDialogOnUi(Window dialog)
	{
		if (Dispatcher.CheckAccess())
		{
			Show();
		}
		else
		{
			Dispatcher.Invoke(Show);
		}
		void Show()
		{
			if (_launchpadWindow != null)
			{
				_launchpadWindow.ShowOwnedDialog(dialog);
			}
			else
			{
				dialog.ShowDialog();
			}
		}
	}

	public static async Task<string> CheckForUpdatesNowAsync(bool showBalloon = false)
	{
		App app = Application.Current as App;
		UpdateService updates = app?._updates;
		if (app == null || updates == null)
		{
			return "更新服务尚未就绪";
		}
		CheckResult checkResult = await updates.CheckAsync(manual: true);
		Log("[UPDATE] manual check: " + checkResult.Message);
		if (checkResult.Info != null)
		{
			app.ShowOwnedDialogOnUi(new UpdateDialog(checkResult.Info, updates));
		}
		else if (showBalloon)
		{
			app._trayIconService?.ShowBalloonNotification("Fluorite", checkResult.Message);
		}
		return checkResult.Message;
	}

	internal static void SetHotkeyCapture(bool capturing)
	{
		if (Application.Current is App app)
		{
			if (capturing)
			{
				app._hotkeyService?.Unregister();
				return;
			}
			app._ignoreHotkeyUntil = DateTime.UtcNow.AddMilliseconds(300.0);
			app.ApplyConfiguredHotkey();
		}
	}

	private void OnHotkeyPressed()
	{
		Log("Hotkey pressed event received");
		Dispatcher.Invoke(() =>
		{
			SettingsViewModel? settingsViewModel = _settingsViewModel;
			if ((settingsViewModel == null || !settingsViewModel.IsCapturingHotkey) && !(DateTime.UtcNow < _ignoreHotkeyUntil))
			{
				_mainViewModel?.ToggleLaunchpad();
			}
		});
	}

	public void SetSingleInstanceService(SingleInstanceService service)
	{
		_singleInstanceService = service;
	}

	public static string? ExtractAddPath(string[] args)
	{
		if (args == null || args.Length == 0)
		{
			return null;
		}
		int pageNumber = Array.IndexOf(args, "--add");
		if (pageNumber >= 0 && pageNumber + 1 < args.Length)
		{
			return args[pageNumber + 1].Trim('"');
		}
		if (args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal) && (File.Exists(args[0]) || Directory.Exists(args[0])))
		{
			return args[0].Trim('"');
		}
		return null;
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log($"Application exiting with code: {e.ApplicationExitCode}");
		_desktopDockWindow?.Dispose();
		_singleInstanceService?.Dispose();
		_hotkeyService?.Dispose();
		_hotCornerService?.Dispose();
		_trayIconService?.Dispose();
		base.OnExit(e);
	}
}
