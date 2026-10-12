using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launchpad.Models;

public class AppSettings : ObservableObject
{
	[ObservableProperty]
	private string _importedFoldersDirectory = string.Empty;

	[ObservableProperty]
	private string _activeLayoutId = string.Empty;

	[ObservableProperty]
	private string _themePresetId = "";

	[ObservableProperty]
	private bool _launchAtStartup;

	[ObservableProperty]
	private bool _openLaunchpadAtStartup;

	[ObservableProperty]
	private bool _iconsLocked;

	[ObservableProperty]
	private uint _hotkeyModifiers = 1u;

	[ObservableProperty]
	private uint _hotkeyKey = 32u;

	[ObservableProperty]
	private string _hotkeyDisplay = "Alt + Space";

	[ObservableProperty]
	private bool _openOnCurrentMonitor = true;

	[ObservableProperty]
	private bool _closeAfterLaunching = true;

	[ObservableProperty]
	private bool _desktopResidentLaunchpad;

	[ObservableProperty]
	private bool _autoUnpinResidentLaunchpad = true;

	[ObservableProperty]
	private bool _enableResidentPinHotCorner;

	[ObservableProperty]
	private HotCornerPosition _residentPinHotCorner = HotCornerPosition.TopLeft;

	[ObservableProperty]
	private AppTheme _theme = AppTheme.Dark;

	[ObservableProperty]
	private int _columns = 7;

	[ObservableProperty]
	private int _rows = 4;

	[ObservableProperty]
	private bool _scanSteamGames = true;

	[ObservableProperty]
	private bool _autoCheckUpdates = true;

	[ObservableProperty]
	private string _searchPosition = "Center";

	[ObservableProperty]
	private SearchDisplayMode _searchDisplayMode = SearchDisplayMode.AlwaysShow;

	[ObservableProperty]
	private bool _hideSearchBar;

	[ObservableProperty]
	private bool _revealSearchOnTopHover = true;

	[ObservableProperty]
	private bool _hideSettingsButton;

	[ObservableProperty]
	private bool _showTopWidgets = true;

	[ObservableProperty]
	private string _topWidgetsPosition = "Left";

	[ObservableProperty]
	private bool _showTopClock = true;

	[ObservableProperty]
	private bool _showTopSystemMonitor;

	[ObservableProperty]
	private bool _clockShowSeconds = true;

	[ObservableProperty]
	private bool _clock24Hour = true;

	[ObservableProperty]
	private ClockStyle _clockStyle;

	[ObservableProperty]
	private string _clockTextColor = "#F7F7F8";

	[ObservableProperty]
	private bool _clockTransparentBackground = true;

	[ObservableProperty]
	[NotifyPropertyChangedFor("ClockSecondsFontSize")]
	private double _clockFontSize = 20.0;

	[ObservableProperty]
	[NotifyPropertyChangedFor("TopClockDateFontSize")]
	private double _topClockFontSize = 32.0;

	[ObservableProperty]
	private bool _clockLargeMode;

	[ObservableProperty]
	private LargeClockStyle _clockLargeStyle;

	[ObservableProperty]
	[NotifyPropertyChangedFor("ClockLargeSecondsFontSize")]
	private double _clockLargeFontSize = 87.0;

	[ObservableProperty]
	private string _clockFontWeight = "Normal";

	[ObservableProperty]
	private bool _clockShowDate = true;

	[ObservableProperty]
	private BackdropKind _backdrop;

	[ObservableProperty]
	private BackdropSource _backdropSource;

	[ObservableProperty]
	private string _customBackdropPath = "";

	[ObservableProperty]
	private double _backdropBlur = 18.0;

	[ObservableProperty]
	private double _backgroundOpacityPercent = 100.0;

	[ObservableProperty]
	private string _solidBackgroundColor = "";

	[ObservableProperty]
	private double _scrimPercent = 31.0;

	[ObservableProperty]
	private bool _showAppNames = true;

	[ObservableProperty]
	private double _appNameFontSize = 15.0;

	[ObservableProperty]
	private bool _appNameShadow = true;

	[ObservableProperty]
	private IconShape _iconShape;

	[ObservableProperty]
	private LabelInk _labelInk = LabelInk.White;

	private string _iconPack = "";

	[ObservableProperty]
	private bool _iconAutoTile = true;

	[ObservableProperty]
	private bool _iconFrostedTile;

	[ObservableProperty]
	private double _iconSizeScale = 110.0;

	[ObservableProperty]
	private double _pageMarginX = 154.0;

	[ObservableProperty]
	private double _pageMarginY = 22.0;

	[ObservableProperty]
	private double _viewportMarginX = 120.0;

	[ObservableProperty]
	private double _viewportMarginTop = 50.0;

	[ObservableProperty]
	private MotionTempo _motionTempo = MotionTempo.Fast;

	[ObservableProperty]
	private MotionCurve _motionCurve;

	[ObservableProperty]
	private double _pageCommitPercent = 10.0;

	[ObservableProperty]
	private bool _snapBackWhenShort = true;

	[ObservableProperty]
	private bool _enableHotCorner = true;

	[ObservableProperty]
	private HotCornerPosition _hotCorner;

	[ObservableProperty]
	private bool _pauseHotCornerInFullscreen = true;

	[ObservableProperty]
	private bool _enableClickSpring = true;

	[ObservableProperty]
	private bool _enableIconTilt;

	[ObservableProperty]
	private bool _enableAuroraBackground;

	[ObservableProperty]
	private string _auroraPalette = "Aurora";

	[ObservableProperty]
	[NotifyPropertyChangedFor("MusicWidgetDisplayMode")]
	private bool _showMusicWidget;

	[ObservableProperty]
	[NotifyPropertyChangedFor("MusicWidgetDisplayMode")]
	private bool _hideMusicWidgetWhenIdle;

	[ObservableProperty]
	private bool _enableMusicSpectrum;

	[ObservableProperty]
	private string _musicOrbEdge = "Left";

	[ObservableProperty]
	private double _musicOrbOffset = 1.0;

	[ObservableProperty]
	private bool _enableDock = true;

	[ObservableProperty]
	private bool _showDesktopDock;

	[ObservableProperty]
	private bool _showOpenAppsInDock;

	[ObservableProperty]
	private string _desktopDockBackgroundStyle = "Classic";

	[ObservableProperty]
	private double _desktopDockBackgroundOpacity = 100.0;

	private double _desktopDockBottomOffset = 8.0;

	[ObservableProperty]
	private bool _enableDesktopDockHotCorner;

	[ObservableProperty]
	private HotCornerPosition _desktopDockHotCorner = HotCornerPosition.BottomRight;

	[ObservableProperty]
	private double _dockIconSize = 56.0;

	public const double MinDockIconSpacing = 0.0;

	public const double MaxDockIconSpacing = 40.0;

	private double _dockIconSpacing = 15.0;

	[ObservableProperty]
	private bool _enableDockMagnification = true;

	[ObservableProperty]
	private double _dockMagnifyScale = 1.75;

	[ObservableProperty]
	private bool _showRunningIndicators = true;

	[JsonIgnore]
	public double ClockSecondsFontSize => ClockFontSize * 0.35;

	[JsonIgnore]
	public double TopClockDateFontSize => TopClockFontSize * (3510.0 / 4095.0);

	[JsonIgnore]
	public double ClockLargeSecondsFontSize => ClockLargeFontSize * 0.25;

	[JsonIgnore]
	public double ScrimOpacity => Math.Clamp(ScrimPercent, 0.0, 80.0) / 100.0;

	public string IconPack
	{
		get
		{
			return _iconPack ?? string.Empty;
		}
		set
		{
			SetProperty(ref _iconPack, value ?? string.Empty, "IconPack");
		}
	}

	[JsonIgnore]
	public int IconTileMode
	{
		get
		{
			if (IconAutoTile)
			{
				if (!IconFrostedTile)
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
		set
		{
			if (value >= 0 && value <= 2)
			{
				IconFrostedTile = value == 2;
				if (value != 2 || IconFrostedTile)
				{
					IconAutoTile = value != 0;
				}
			}
		}
	}

	public bool ReduceMotion
	{
		get
		{
			return false;
		}
		set
		{
		}
	}

	[JsonIgnore]
	public string MusicWidgetDisplayMode
	{
		get
		{
			if (ShowMusicWidget)
			{
				if (!HideMusicWidgetWhenIdle)
				{
					return "Always";
				}
				return "WhenAvailable";
			}
			return "Off";
		}
		set
		{
			bool flag;
			switch (value)
			{
			case "Off":
			case "WhenAvailable":
			case "Always":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				HideMusicWidgetWhenIdle = value == "WhenAvailable";
				ShowMusicWidget = value != "Off";
			}
		}
	}

	public double DesktopDockBottomOffset
	{
		get
		{
			return _desktopDockBottomOffset;
		}
		set
		{
			SetProperty(ref _desktopDockBottomOffset, double.IsFinite(value) ? Math.Clamp(value, 0.0, 200.0) : 8.0, "DesktopDockBottomOffset");
		}
	}

	public double DockIconSpacing
	{
		get
		{
			return _dockIconSpacing;
		}
		set
		{
			SetProperty(ref _dockIconSpacing, double.IsFinite(value) ? Math.Clamp(value, 0.0, 40.0) : 15.0, "DockIconSpacing");
		}
	}

	public string ImportedFoldersDirectory
	{
		get
		{
			return _importedFoldersDirectory;
		}
		[MemberNotNull("_importedFoldersDirectory")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_importedFoldersDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ImportedFoldersDirectory);
				_importedFoldersDirectory = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ImportedFoldersDirectory);
			}
		}
	}

	public string ActiveLayoutId
	{
		get
		{
			return _activeLayoutId;
		}
		[MemberNotNull("_activeLayoutId")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_activeLayoutId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ActiveLayoutId);
				_activeLayoutId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ActiveLayoutId);
			}
		}
	}

	public string ThemePresetId
	{
		get
		{
			return _themePresetId;
		}
		[MemberNotNull("_themePresetId")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_themePresetId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ThemePresetId);
				_themePresetId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ThemePresetId);
			}
		}
	}

	public bool LaunchAtStartup
	{
		get
		{
			return _launchAtStartup;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_launchAtStartup, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchAtStartup);
				_launchAtStartup = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchAtStartup);
			}
		}
	}

	public bool OpenLaunchpadAtStartup
	{
		get
		{
			return _openLaunchpadAtStartup;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_openLaunchpadAtStartup, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OpenLaunchpadAtStartup);
				_openLaunchpadAtStartup = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OpenLaunchpadAtStartup);
			}
		}
	}

	public bool IconsLocked
	{
		get
		{
			return _iconsLocked;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_iconsLocked, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconsLocked);
				_iconsLocked = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconsLocked);
			}
		}
	}

	public uint HotkeyModifiers
	{
		get
		{
			return _hotkeyModifiers;
		}
		set
		{
			if (!EqualityComparer<uint>.Default.Equals(_hotkeyModifiers, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotkeyModifiers);
				_hotkeyModifiers = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotkeyModifiers);
			}
		}
	}

	public uint HotkeyKey
	{
		get
		{
			return _hotkeyKey;
		}
		set
		{
			if (!EqualityComparer<uint>.Default.Equals(_hotkeyKey, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotkeyKey);
				_hotkeyKey = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotkeyKey);
			}
		}
	}

	public string HotkeyDisplay
	{
		get
		{
			return _hotkeyDisplay;
		}
		[MemberNotNull("_hotkeyDisplay")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_hotkeyDisplay, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotkeyDisplay);
				_hotkeyDisplay = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotkeyDisplay);
			}
		}
	}

	public bool OpenOnCurrentMonitor
	{
		get
		{
			return _openOnCurrentMonitor;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_openOnCurrentMonitor, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OpenOnCurrentMonitor);
				_openOnCurrentMonitor = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OpenOnCurrentMonitor);
			}
		}
	}

	public bool CloseAfterLaunching
	{
		get
		{
			return _closeAfterLaunching;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_closeAfterLaunching, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CloseAfterLaunching);
				_closeAfterLaunching = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CloseAfterLaunching);
			}
		}
	}

	public bool DesktopResidentLaunchpad
	{
		get
		{
			return _desktopResidentLaunchpad;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_desktopResidentLaunchpad, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DesktopResidentLaunchpad);
				_desktopResidentLaunchpad = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DesktopResidentLaunchpad);
			}
		}
	}

	public bool AutoUnpinResidentLaunchpad
	{
		get
		{
			return _autoUnpinResidentLaunchpad;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_autoUnpinResidentLaunchpad, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AutoUnpinResidentLaunchpad);
				_autoUnpinResidentLaunchpad = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AutoUnpinResidentLaunchpad);
			}
		}
	}

	public bool EnableResidentPinHotCorner
	{
		get
		{
			return _enableResidentPinHotCorner;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableResidentPinHotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableResidentPinHotCorner);
				_enableResidentPinHotCorner = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableResidentPinHotCorner);
			}
		}
	}

	public HotCornerPosition ResidentPinHotCorner
	{
		get
		{
			return _residentPinHotCorner;
		}
		set
		{
			if (!EqualityComparer<HotCornerPosition>.Default.Equals(_residentPinHotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ResidentPinHotCorner);
				_residentPinHotCorner = value;
				OnResidentPinHotCornerChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ResidentPinHotCorner);
			}
		}
	}

	public AppTheme Theme
	{
		get
		{
			return _theme;
		}
		set
		{
			if (!EqualityComparer<AppTheme>.Default.Equals(_theme, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Theme);
				_theme = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Theme);
			}
		}
	}

	public int Columns
	{
		get
		{
			return _columns;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_columns, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Columns);
				_columns = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Columns);
			}
		}
	}

	public int Rows
	{
		get
		{
			return _rows;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_rows, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Rows);
				_rows = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Rows);
			}
		}
	}

	public bool ScanSteamGames
	{
		get
		{
			return _scanSteamGames;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_scanSteamGames, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ScanSteamGames);
				_scanSteamGames = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ScanSteamGames);
			}
		}
	}

	public bool AutoCheckUpdates
	{
		get
		{
			return _autoCheckUpdates;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_autoCheckUpdates, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AutoCheckUpdates);
				_autoCheckUpdates = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AutoCheckUpdates);
			}
		}
	}

	public string SearchPosition
	{
		get
		{
			return _searchPosition;
		}
		[MemberNotNull("_searchPosition")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_searchPosition, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchPosition);
				_searchPosition = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchPosition);
			}
		}
	}

	public SearchDisplayMode SearchDisplayMode
	{
		get
		{
			return _searchDisplayMode;
		}
		set
		{
			if (!EqualityComparer<SearchDisplayMode>.Default.Equals(_searchDisplayMode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchDisplayMode);
				_searchDisplayMode = value;
				OnSearchDisplayModeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchDisplayMode);
			}
		}
	}

	public bool HideSearchBar
	{
		get
		{
			return _hideSearchBar;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hideSearchBar, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HideSearchBar);
				_hideSearchBar = value;
				OnHideSearchBarChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HideSearchBar);
			}
		}
	}

	public bool RevealSearchOnTopHover
	{
		get
		{
			return _revealSearchOnTopHover;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_revealSearchOnTopHover, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RevealSearchOnTopHover);
				_revealSearchOnTopHover = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RevealSearchOnTopHover);
			}
		}
	}

	public bool HideSettingsButton
	{
		get
		{
			return _hideSettingsButton;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hideSettingsButton, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HideSettingsButton);
				_hideSettingsButton = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HideSettingsButton);
			}
		}
	}

	public bool ShowTopWidgets
	{
		get
		{
			return _showTopWidgets;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showTopWidgets, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowTopWidgets);
				_showTopWidgets = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowTopWidgets);
			}
		}
	}

	public string TopWidgetsPosition
	{
		get
		{
			return _topWidgetsPosition;
		}
		[MemberNotNull("_topWidgetsPosition")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_topWidgetsPosition, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TopWidgetsPosition);
				_topWidgetsPosition = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TopWidgetsPosition);
			}
		}
	}

	public bool ShowTopClock
	{
		get
		{
			return _showTopClock;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showTopClock, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowTopClock);
				_showTopClock = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowTopClock);
			}
		}
	}

	public bool ShowTopSystemMonitor
	{
		get
		{
			return _showTopSystemMonitor;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showTopSystemMonitor, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowTopSystemMonitor);
				_showTopSystemMonitor = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowTopSystemMonitor);
			}
		}
	}

	public bool ClockShowSeconds
	{
		get
		{
			return _clockShowSeconds;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_clockShowSeconds, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockShowSeconds);
				_clockShowSeconds = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockShowSeconds);
			}
		}
	}

	public bool Clock24Hour
	{
		get
		{
			return _clock24Hour;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_clock24Hour, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Clock24Hour);
				_clock24Hour = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Clock24Hour);
			}
		}
	}

	public ClockStyle ClockStyle
	{
		get
		{
			return _clockStyle;
		}
		set
		{
			if (!EqualityComparer<ClockStyle>.Default.Equals(_clockStyle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockStyle);
				_clockStyle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockStyle);
			}
		}
	}

	public string ClockTextColor
	{
		get
		{
			return _clockTextColor;
		}
		[MemberNotNull("_clockTextColor")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockTextColor, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockTextColor);
				_clockTextColor = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockTextColor);
			}
		}
	}

	public bool ClockTransparentBackground
	{
		get
		{
			return _clockTransparentBackground;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_clockTransparentBackground, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockTransparentBackground);
				_clockTransparentBackground = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockTransparentBackground);
			}
		}
	}

	public double ClockFontSize
	{
		get
		{
			return _clockFontSize;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_clockFontSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockFontSize);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockSecondsFontSize);
				_clockFontSize = value;
				OnClockFontSizeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockFontSize);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockSecondsFontSize);
			}
		}
	}

	public double TopClockFontSize
	{
		get
		{
			return _topClockFontSize;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_topClockFontSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TopClockFontSize);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TopClockDateFontSize);
				_topClockFontSize = value;
				OnTopClockFontSizeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TopClockFontSize);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TopClockDateFontSize);
			}
		}
	}

	public bool ClockLargeMode
	{
		get
		{
			return _clockLargeMode;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_clockLargeMode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockLargeMode);
				_clockLargeMode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockLargeMode);
			}
		}
	}

	public LargeClockStyle ClockLargeStyle
	{
		get
		{
			return _clockLargeStyle;
		}
		set
		{
			if (!EqualityComparer<LargeClockStyle>.Default.Equals(_clockLargeStyle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockLargeStyle);
				_clockLargeStyle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockLargeStyle);
			}
		}
	}

	public double ClockLargeFontSize
	{
		get
		{
			return _clockLargeFontSize;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_clockLargeFontSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockLargeFontSize);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockLargeSecondsFontSize);
				_clockLargeFontSize = value;
				OnClockLargeFontSizeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockLargeFontSize);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockLargeSecondsFontSize);
			}
		}
	}

	public string ClockFontWeight
	{
		get
		{
			return _clockFontWeight;
		}
		[MemberNotNull("_clockFontWeight")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockFontWeight, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockFontWeight);
				_clockFontWeight = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockFontWeight);
			}
		}
	}

	public bool ClockShowDate
	{
		get
		{
			return _clockShowDate;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_clockShowDate, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockShowDate);
				_clockShowDate = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockShowDate);
			}
		}
	}

	public BackdropKind Backdrop
	{
		get
		{
			return _backdrop;
		}
		set
		{
			if (!EqualityComparer<BackdropKind>.Default.Equals(_backdrop, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Backdrop);
				_backdrop = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Backdrop);
			}
		}
	}

	public BackdropSource BackdropSource
	{
		get
		{
			return _backdropSource;
		}
		set
		{
			if (!EqualityComparer<BackdropSource>.Default.Equals(_backdropSource, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackdropSource);
				_backdropSource = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackdropSource);
			}
		}
	}

	public string CustomBackdropPath
	{
		get
		{
			return _customBackdropPath;
		}
		[MemberNotNull("_customBackdropPath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_customBackdropPath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CustomBackdropPath);
				_customBackdropPath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CustomBackdropPath);
			}
		}
	}

	public double BackdropBlur
	{
		get
		{
			return _backdropBlur;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_backdropBlur, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackdropBlur);
				_backdropBlur = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackdropBlur);
			}
		}
	}

	public double BackgroundOpacityPercent
	{
		get
		{
			return _backgroundOpacityPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_backgroundOpacityPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackgroundOpacityPercent);
				_backgroundOpacityPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackgroundOpacityPercent);
			}
		}
	}

	public string SolidBackgroundColor
	{
		get
		{
			return _solidBackgroundColor;
		}
		[MemberNotNull("_solidBackgroundColor")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_solidBackgroundColor, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SolidBackgroundColor);
				_solidBackgroundColor = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SolidBackgroundColor);
			}
		}
	}

	public double ScrimPercent
	{
		get
		{
			return _scrimPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_scrimPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ScrimPercent);
				_scrimPercent = value;
				OnScrimPercentChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ScrimPercent);
			}
		}
	}

	public bool ShowAppNames
	{
		get
		{
			return _showAppNames;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showAppNames, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowAppNames);
				_showAppNames = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowAppNames);
			}
		}
	}

	public double AppNameFontSize
	{
		get
		{
			return _appNameFontSize;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_appNameFontSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AppNameFontSize);
				_appNameFontSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AppNameFontSize);
			}
		}
	}

	public bool AppNameShadow
	{
		get
		{
			return _appNameShadow;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_appNameShadow, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AppNameShadow);
				_appNameShadow = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AppNameShadow);
			}
		}
	}

	public IconShape IconShape
	{
		get
		{
			return _iconShape;
		}
		set
		{
			if (!EqualityComparer<IconShape>.Default.Equals(_iconShape, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconShape);
				_iconShape = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconShape);
			}
		}
	}

	public LabelInk LabelInk
	{
		get
		{
			return _labelInk;
		}
		set
		{
			if (!EqualityComparer<LabelInk>.Default.Equals(_labelInk, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LabelInk);
				_labelInk = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LabelInk);
			}
		}
	}

	public bool IconAutoTile
	{
		get
		{
			return _iconAutoTile;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_iconAutoTile, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconAutoTile);
				_iconAutoTile = value;
				OnIconAutoTileChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconAutoTile);
			}
		}
	}

	public bool IconFrostedTile
	{
		get
		{
			return _iconFrostedTile;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_iconFrostedTile, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconFrostedTile);
				_iconFrostedTile = value;
				OnIconFrostedTileChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconFrostedTile);
			}
		}
	}

	public double IconSizeScale
	{
		get
		{
			return _iconSizeScale;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_iconSizeScale, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconSizeScale);
				_iconSizeScale = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconSizeScale);
			}
		}
	}

	public double PageMarginX
	{
		get
		{
			return _pageMarginX;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_pageMarginX, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageMarginX);
				_pageMarginX = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageMarginX);
			}
		}
	}

	public double PageMarginY
	{
		get
		{
			return _pageMarginY;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_pageMarginY, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageMarginY);
				_pageMarginY = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageMarginY);
			}
		}
	}

	public double ViewportMarginX
	{
		get
		{
			return _viewportMarginX;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_viewportMarginX, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ViewportMarginX);
				_viewportMarginX = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ViewportMarginX);
			}
		}
	}

	public double ViewportMarginTop
	{
		get
		{
			return _viewportMarginTop;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_viewportMarginTop, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ViewportMarginTop);
				_viewportMarginTop = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ViewportMarginTop);
			}
		}
	}

	public MotionTempo MotionTempo
	{
		get
		{
			return _motionTempo;
		}
		set
		{
			if (!EqualityComparer<MotionTempo>.Default.Equals(_motionTempo, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MotionTempo);
				_motionTempo = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MotionTempo);
			}
		}
	}

	public MotionCurve MotionCurve
	{
		get
		{
			return _motionCurve;
		}
		set
		{
			if (!EqualityComparer<MotionCurve>.Default.Equals(_motionCurve, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MotionCurve);
				_motionCurve = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MotionCurve);
			}
		}
	}

	public double PageCommitPercent
	{
		get
		{
			return _pageCommitPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_pageCommitPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageCommitPercent);
				_pageCommitPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageCommitPercent);
			}
		}
	}

	public bool SnapBackWhenShort
	{
		get
		{
			return _snapBackWhenShort;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_snapBackWhenShort, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SnapBackWhenShort);
				_snapBackWhenShort = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SnapBackWhenShort);
			}
		}
	}

	public bool EnableHotCorner
	{
		get
		{
			return _enableHotCorner;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableHotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableHotCorner);
				_enableHotCorner = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableHotCorner);
			}
		}
	}

	public HotCornerPosition HotCorner
	{
		get
		{
			return _hotCorner;
		}
		set
		{
			if (!EqualityComparer<HotCornerPosition>.Default.Equals(_hotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotCorner);
				_hotCorner = value;
				OnHotCornerChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotCorner);
			}
		}
	}

	public bool PauseHotCornerInFullscreen
	{
		get
		{
			return _pauseHotCornerInFullscreen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_pauseHotCornerInFullscreen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PauseHotCornerInFullscreen);
				_pauseHotCornerInFullscreen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PauseHotCornerInFullscreen);
			}
		}
	}

	public bool EnableClickSpring
	{
		get
		{
			return _enableClickSpring;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableClickSpring, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableClickSpring);
				_enableClickSpring = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableClickSpring);
			}
		}
	}

	public bool EnableIconTilt
	{
		get
		{
			return _enableIconTilt;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableIconTilt, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableIconTilt);
				_enableIconTilt = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableIconTilt);
			}
		}
	}

	public bool EnableAuroraBackground
	{
		get
		{
			return _enableAuroraBackground;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableAuroraBackground, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableAuroraBackground);
				_enableAuroraBackground = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableAuroraBackground);
			}
		}
	}

	public string AuroraPalette
	{
		get
		{
			return _auroraPalette;
		}
		[MemberNotNull("_auroraPalette")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_auroraPalette, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AuroraPalette);
				_auroraPalette = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AuroraPalette);
			}
		}
	}

	public bool ShowMusicWidget
	{
		get
		{
			return _showMusicWidget;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showMusicWidget, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowMusicWidget);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MusicWidgetDisplayMode);
				_showMusicWidget = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowMusicWidget);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MusicWidgetDisplayMode);
			}
		}
	}

	public bool HideMusicWidgetWhenIdle
	{
		get
		{
			return _hideMusicWidgetWhenIdle;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hideMusicWidgetWhenIdle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HideMusicWidgetWhenIdle);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MusicWidgetDisplayMode);
				_hideMusicWidgetWhenIdle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HideMusicWidgetWhenIdle);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MusicWidgetDisplayMode);
			}
		}
	}

	public bool EnableMusicSpectrum
	{
		get
		{
			return _enableMusicSpectrum;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableMusicSpectrum, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableMusicSpectrum);
				_enableMusicSpectrum = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableMusicSpectrum);
			}
		}
	}

	public string MusicOrbEdge
	{
		get
		{
			return _musicOrbEdge;
		}
		[MemberNotNull("_musicOrbEdge")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_musicOrbEdge, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MusicOrbEdge);
				_musicOrbEdge = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MusicOrbEdge);
			}
		}
	}

	public double MusicOrbOffset
	{
		get
		{
			return _musicOrbOffset;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_musicOrbOffset, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MusicOrbOffset);
				_musicOrbOffset = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MusicOrbOffset);
			}
		}
	}

	public bool EnableDock
	{
		get
		{
			return _enableDock;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableDock, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableDock);
				_enableDock = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableDock);
			}
		}
	}

	public bool ShowDesktopDock
	{
		get
		{
			return _showDesktopDock;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showDesktopDock, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowDesktopDock);
				_showDesktopDock = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowDesktopDock);
			}
		}
	}

	public bool ShowOpenAppsInDock
	{
		get
		{
			return _showOpenAppsInDock;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showOpenAppsInDock, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowOpenAppsInDock);
				_showOpenAppsInDock = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowOpenAppsInDock);
			}
		}
	}

	public string DesktopDockBackgroundStyle
	{
		get
		{
			return _desktopDockBackgroundStyle;
		}
		[MemberNotNull("_desktopDockBackgroundStyle")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_desktopDockBackgroundStyle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DesktopDockBackgroundStyle);
				_desktopDockBackgroundStyle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DesktopDockBackgroundStyle);
			}
		}
	}

	public double DesktopDockBackgroundOpacity
	{
		get
		{
			return _desktopDockBackgroundOpacity;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_desktopDockBackgroundOpacity, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DesktopDockBackgroundOpacity);
				_desktopDockBackgroundOpacity = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DesktopDockBackgroundOpacity);
			}
		}
	}

	public bool EnableDesktopDockHotCorner
	{
		get
		{
			return _enableDesktopDockHotCorner;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableDesktopDockHotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableDesktopDockHotCorner);
				_enableDesktopDockHotCorner = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableDesktopDockHotCorner);
			}
		}
	}

	public HotCornerPosition DesktopDockHotCorner
	{
		get
		{
			return _desktopDockHotCorner;
		}
		set
		{
			if (!EqualityComparer<HotCornerPosition>.Default.Equals(_desktopDockHotCorner, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DesktopDockHotCorner);
				_desktopDockHotCorner = value;
				OnDesktopDockHotCornerChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DesktopDockHotCorner);
			}
		}
	}

	public double DockIconSize
	{
		get
		{
			return _dockIconSize;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_dockIconSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockIconSize);
				_dockIconSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockIconSize);
			}
		}
	}

	public bool EnableDockMagnification
	{
		get
		{
			return _enableDockMagnification;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_enableDockMagnification, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnableDockMagnification);
				_enableDockMagnification = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnableDockMagnification);
			}
		}
	}

	public double DockMagnifyScale
	{
		get
		{
			return _dockMagnifyScale;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_dockMagnifyScale, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockMagnifyScale);
				_dockMagnifyScale = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockMagnifyScale);
			}
		}
	}

	public bool ShowRunningIndicators
	{
		get
		{
			return _showRunningIndicators;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_showRunningIndicators, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShowRunningIndicators);
				_showRunningIndicators = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShowRunningIndicators);
			}
		}
	}

	private void ResolveResidentPinCorner()
	{
		if (ResidentPinHotCorner != DesktopDockHotCorner)
		{
			return;
		}
		for (int i = 0; i < 4; i++)
		{
			if (i != (int)DesktopDockHotCorner)
			{
				ResidentPinHotCorner = (HotCornerPosition)i;
				break;
			}
		}
	}

	public void ResetToDefault()
	{
		AppSettings obj = new AppSettings();
		PropertyInfo[] properties = typeof(AppSettings).GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
			{
				bool flag;
				switch (propertyInfo.Name)
				{
				case "HotkeyModifiers":
				case "HotkeyKey":
				case "HotkeyDisplay":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					propertyInfo.SetValue(this, propertyInfo.GetValue(obj));
				}
			}
		}
	}

	private void OnResidentPinHotCornerChanged(HotCornerPosition value)
	{
		ResolveResidentPinCorner();
	}

	private void OnSearchDisplayModeChanged(SearchDisplayMode value)
	{
		HideSearchBar = value != SearchDisplayMode.AlwaysShow;
	}

	private void OnHideSearchBarChanged(bool value)
	{
		if (!value && SearchDisplayMode != SearchDisplayMode.AlwaysShow)
		{
			SearchDisplayMode = SearchDisplayMode.AlwaysShow;
		}
	}

	private void OnClockFontSizeChanged(double value)
	{
		double num = (double.IsFinite(value) ? Math.Clamp(value, 20.0, 72.0) : 32.0);
		if (num != value)
		{
			ClockFontSize = num;
		}
	}

	private void OnTopClockFontSizeChanged(double value)
	{
		double num = (double.IsFinite(value) ? Math.Clamp(value, 12.0, 32.0) : 14.0);
		if (num != value)
		{
			TopClockFontSize = num;
		}
	}

	private void OnClockLargeFontSizeChanged(double value)
	{
		double num = (double.IsFinite(value) ? Math.Clamp(value, 48.0, 180.0) : 96.0);
		if (num != value)
		{
			ClockLargeFontSize = num;
		}
	}

	private void OnScrimPercentChanged(double value)
	{
		OnPropertyChanged("ScrimOpacity");
	}

	private void OnIconAutoTileChanged(bool value)
	{
		OnPropertyChanged("IconTileMode");
	}

	private void OnIconFrostedTileChanged(bool value)
	{
		OnPropertyChanged("IconTileMode");
	}

	private void OnHotCornerChanged(HotCornerPosition value)
	{
		if (DesktopDockHotCorner == value)
		{
			DesktopDockHotCorner = (HotCornerPosition)((int)(value + 1) % 4);
		}
		ResolveResidentPinCorner();
	}

	private void OnDesktopDockHotCornerChanged(HotCornerPosition value)
	{
		if (HotCorner == value)
		{
			HotCorner = (HotCornerPosition)((int)(value + 1) % 4);
		}
		ResolveResidentPinCorner();
	}
}
