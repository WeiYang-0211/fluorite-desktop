using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launchpad.Infrastructure;
using Launchpad.Interop;
using Launchpad.Models;
using Launchpad.Services;

namespace Launchpad.ViewModels;

public class MainViewModel : ObservableObject
{
	private enum DockGridImpact
	{
		None,
		Surgical,
		Full
	}

	private readonly IStorageService _storageService;

	private readonly IAppScannerService _scannerService;

	private readonly IIconService _iconService;

	private readonly IAppLauncherService _launcherService;

	private readonly ISearchService _searchService;

	private readonly IWidgetService _widgetService;

	private readonly DispatcherTimer _searchDebounceTimer;

	private readonly List<AppItem> _allApps = new List<AppItem>();

	private List<AppItem> _filteredApps = new List<AppItem>();

	private CancellationTokenSource? _scanCts;

	private CancellationTokenSource? _iconCts;

	private DateTime _lastScanUtc = DateTime.MinValue;

	[ObservableProperty]
	[NotifyPropertyChangedFor("PageSize")]
	[NotifyPropertyChangedFor("CardTextWidth")]
	private int _columns = 8;

	[ObservableProperty]
	[NotifyPropertyChangedFor("PageSize")]
	private int _rows = 5;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CardWidth")]
	[NotifyPropertyChangedFor("CardHeight")]
	[NotifyPropertyChangedFor("CardTextWidth")]
	[NotifyPropertyChangedFor("IconCornerRadius")]
	[NotifyPropertyChangedFor("FolderCornerRadius")]
	private int _iconSize = 80;

	private Size _gridViewport;

	private AppSettings _settings = new AppSettings();

	private double? _dockPresentedSize;

	private bool _dockShelfReserved;

	[ObservableProperty]
	private bool _isOpen;

	[ObservableProperty]
	private bool _isScanning;

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private int _currentPage = 1;

	[ObservableProperty]
	private int _totalPages = 1;

	[ObservableProperty]
	private string _statusMessage = string.Empty;

	[ObservableProperty]
	private bool _isSearchActive;

	[ObservableProperty]
	private bool _isSearchRevealedByHover;

	[ObservableProperty]
	private bool _isSearchSlidDown;

	[ObservableProperty]
	private AppItem? _activeFolder;

	[ObservableProperty]
	private bool _isFolderOpen;

	[ObservableProperty]
	private ObservableCollection<AppItem> _currentPageApps = new ObservableCollection<AppItem>();

	[ObservableProperty]
	private ObservableCollection<PageIndicatorItem> _pageIndicators = new ObservableCollection<PageIndicatorItem>();

	[ObservableProperty]
	private ObservableCollection<AppItem> _hiddenApps = new ObservableCollection<AppItem>();

	[ObservableProperty]
	[NotifyPropertyChangedFor("IsDockVisible")]
	[NotifyPropertyChangedFor("IsDockEmpty")]
	[NotifyPropertyChangedFor("DockVisibility")]
	[NotifyPropertyChangedFor("DockRowWidth")]
	[NotifyPropertyChangedFor("DockBaseWidth")]
	[NotifyPropertyChangedFor("ViewportMargin")]
	private ObservableCollection<AppItem> _dockApps = new ObservableCollection<AppItem>();

	[ObservableProperty]
	private bool _isItemDragActive;

	[ObservableProperty]
	private bool _isPageSwiping;

	[ObservableProperty]
	private string _clockTime = "--:--";

	[ObservableProperty]
	private string _clockSeconds = "00";

	[ObservableProperty]
	private string _clockDate = string.Empty;

	[ObservableProperty]
	private string _clockFullDate = string.Empty;

	[ObservableProperty]
	private string _clockDayOfWeek = string.Empty;

	[ObservableProperty]
	private string _clockLunar = string.Empty;

	[ObservableProperty]
	private string _clockGreeting = string.Empty;

	[ObservableProperty]
	private double _cpuUsage;

	[ObservableProperty]
	private double _memoryUsage;

	[ObservableProperty]
	private double _totalMemoryGb;

	[ObservableProperty]
	private double _usedMemoryGb;

	[ObservableProperty]
	private string _memoryText = "0 / 0 GB";

	[ObservableProperty]
	private bool _isWidgetBoardOpen;

	[ObservableProperty]
	private bool _hasClockWidgetOnGrid;

	[ObservableProperty]
	private bool _hasMonitorWidgetOnGrid;

	private bool _settingsAdopted;

	private bool _batchSettings;

	private double _gridDeviceScale = 1.0;

	private double _desktopDockDeviceScale = 1.0;

	private readonly RunningAppsMonitor _runningMonitor = new RunningAppsMonitor(Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);

	private HashSet<string> _runningNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private Task _visibleIconsReady = Task.CompletedTask;

	[ObservableProperty]
	private bool _isIconStyleSwitching;

	[ObservableProperty]
	private double _iconStyleSwitchProgress;

	[ObservableProperty]
	private string _iconStyleSwitchStatus = "";

	private int _iconStyleSwitchVersion;

	private readonly List<AppItem> _manualApps = new List<AppItem>();

	[ObservableProperty]
	private string _sceneSwitchFeedback = string.Empty;

	private int _sceneFeedbackVersion;

	[ObservableProperty]
	private string _manualAddFeedback = string.Empty;

	private int _manualFeedbackVersion;

	private readonly HashSet<AppItem> _pendingAppLaunches = new HashSet<AppItem>();

	private bool _storingFolders;

	private List<AppItem> _openWindowEntries = new List<AppItem>();

	private Dictionary<string, OpenAppWindow[]> _openWindowTargets = new Dictionary<string, OpenAppWindow[]>(StringComparer.OrdinalIgnoreCase);

	private string _openWindowSignature = "";

	private int _openWindowVersion;

	private RelayCommand? disableDockCommand;

	private AsyncRelayCommand? rescanApplicationsCommand;

	private AsyncRelayCommand<AppItem?>? toggleDockCommand;

	private AsyncRelayCommand<AppItem?>? hideAppCommand;

	private AsyncRelayCommand? unhideAllCommand;

	private AsyncRelayCommand<AppItem?>? unhideAppCommand;

	private AsyncRelayCommand? addApplicationByPickingCommand;

	private AsyncRelayCommand<AppItem?>? removeManualAppCommand;

	private AsyncRelayCommand<AppItem?>? restoreIconCommand;

	private RelayCommand<AppItem?>? launchAppCommand;

	private RelayCommand<AppItem?>? openFolderCommand;

	private RelayCommand? closeFolderCommand;

	private AsyncRelayCommand<AppItem?>? createFolderCommand;

	private AsyncRelayCommand<AppItem?>? removeFromFolderCommand;

	private AsyncRelayCommand<AppItem?>? dissolveFolderCommand;

	private AsyncRelayCommand<AppItem?>? restoreNameCommand;

	private RelayCommand<AppItem?>? runAsAdminCommand;

	private RelayCommand<AppItem?>? showInFolderCommand;

	private RelayCommand<AppItem?>? requestUninstallCommand;

	private RelayCommand? nextPageCommand;

	private RelayCommand? previousPageCommand;

	private RelayCommand<int>? selectPageCommand;

	private RelayCommand? toggleLaunchpadCommand;

	private RelayCommand? openLaunchpadCommand;

	private RelayCommand? closeLaunchpadCommand;

	private RelayCommand? toggleWidgetBoardCommand;

	private RelayCommand? openWidgetBoardCommand;

	private RelayCommand? closeWidgetBoardCommand;

	private RelayCommand? openDateAndTimeSettingsCommand;

	private RelayCommand? openTaskManagerCommand;

	private AsyncRelayCommand<string?>? toggleWidgetOnGridCommand;

	private AsyncRelayCommand<string?>? addWidgetToGridCommand;

	private AsyncRelayCommand<string?>? moveWidgetToFrontCommand;

	private AsyncRelayCommand<AppItem?>? removeWidgetCommand;

	private RelayCommand? clearSearchCommand;

	public IReadOnlyList<AppItem> AllApps => _allApps;

	public AppSettings Settings => _settings;

	public Thickness PageMargin => new Thickness(_settings.PageMarginX, _settings.PageMarginY, _settings.PageMarginX, _settings.PageMarginY);

	public double DockIconSize
	{
		get
		{
			double? dockPresentedSize = _dockPresentedSize;
			if (!dockPresentedSize.HasValue)
			{
				if (!(_settings.DockIconSize > 0.0))
				{
					return 56.0;
				}
				return _settings.DockIconSize;
			}
			return dockPresentedSize.GetValueOrDefault();
		}
	}

	public double DockGap => _settings.DockIconSpacing;

	public double DockPaddingX => 16.0;

	public double DockPaddingY => 14.0;

	public int MaxDockItems
	{
		get
		{
			double width = ((_gridViewport.Width > 0.0) ? _gridViewport.Width : SystemParameters.WorkArea.Width);
			double dockIconSize = DockIconSize;
			double magnification;
			if (_settings.EnableDockMagnification)
			{
				magnification = ((_settings.DockMagnifyScale > 1.0) ? _settings.DockMagnifyScale : 1.85);
			}
			else
			{
				magnification = 1.0;
			}
			return Math.Max(0, DockCapacity.ForWidth(width, dockIconSize, magnification, DockGap) - ((_settings.ShowDesktopDock || _settings.EnableDesktopDockHotCorner) ? 1 : 0));
		}
	}

	public double DockSlot => DockIconSize + DockGap;

	public double DockBarHeight => DockIconSize + DockPaddingY * 2.0;

	public double DockRowWidth
	{
		get
		{
			if (DockApps.Count <= 0)
			{
				return 0.0;
			}
			return (double)DockApps.Count * DockSlot - DockGap;
		}
	}

	public double DockBaseWidth => DockPaddingX * 2.0 + DockRowWidth;

	public double DockShelfWidth => DockPaddingX * 2.0 + DockIconSize;

	public bool DockShelfReserved
	{
		get
		{
			return _dockShelfReserved;
		}
		set
		{
			if (_dockShelfReserved != value)
			{
				_dockShelfReserved = value;
				OnPropertyChanged("DockShelfReserved");
				OnPropertyChanged("ViewportMargin");
			}
		}
	}

	public Thickness ViewportMargin => new Thickness(_settings.ViewportMarginX, _settings.ViewportMarginTop, _settings.ViewportMarginX, (IsDockVisible || DockShelfReserved) ? (DockBarHeight + 64.0) : (_settings.ViewportMarginTop * 0.5));

	public int PageSize => Columns * Rows;

	public double CardWidth => IconSize + 28;

	public double CardHeight => IconSize + 42;

	public double CardTextWidth
	{
		get
		{
			double num = ((_gridViewport.Width > 0.0) ? (_gridViewport.Width / (double)Math.Max(1, Columns)) : ((double)IconSize + 70.0));
			double val = Math.Max(IconSize, num - 14.0);
			return Math.Min(Math.Clamp((double)IconSize + 68.0, 120.0, 180.0), val);
		}
	}

	public double IconCornerRadius => _settings.IconShape switch
	{
		IconShape.Rounded => (double)IconSize * 0.22, 
		IconShape.Circle => IconSize, 
		_ => 0.0, 
	};

	public double DockIconCornerRadius => _settings.IconShape switch
	{
		IconShape.Rounded => DockIconSize * 0.22, 
		IconShape.Circle => DockIconSize, 
		_ => 0.0, 
	};

	public double FolderCornerRadius
	{
		get
		{
			if (_settings.IconShape == IconShape.Circle)
			{
				return (double)IconSize * 0.5;
			}
			return Math.Max(16.0, (double)IconSize * 0.225);
		}
	}

	public bool IsDockVisible
	{
		get
		{
			if (_settings.EnableDock)
			{
				return DockApps.Count > 0;
			}
			return false;
		}
	}

	public bool IsDockEmpty
	{
		get
		{
			if (_settings.EnableDock)
			{
				return DockApps.Count == 0;
			}
			return false;
		}
	}

	public Visibility DockVisibility
	{
		get
		{
			if (!IsDockVisible)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public long LastDragEndTimeTicks { get; set; }

	public SettingsViewModel.Option<string>[] SearchPositionOptions { get; } = new SettingsViewModel.Option<string>[3]
	{
		new SettingsViewModel.Option<string>("Center", "居中 (默认)"),
		new SettingsViewModel.Option<string>("Left", "左上角"),
		new SettingsViewModel.Option<string>("Right", "右上角")
	};

	public SettingsViewModel.Option<string>[] WidgetPositionOptions { get; } = new SettingsViewModel.Option<string>[3]
	{
		new SettingsViewModel.Option<string>("Right", "右上角 (默认)"),
		new SettingsViewModel.Option<string>("Center", "居中"),
		new SettingsViewModel.Option<string>("Left", "左上角")
	};

	public bool HasCompletedInitialization { get; internal set; }

	public int ManualAppCount => _manualApps.Count;

	public Func<string[]?>? PickApplicationFiles { get; set; }

	private static string UserIconDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fluorite", "icons");

	internal bool IsResidentLaunchpadDismissed { get; private set; }

	public string ImportedFoldersDirectory
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Settings.ImportedFoldersDirectory))
			{
				return Path.GetFullPath(Settings.ImportedFoldersDirectory);
			}
			return Path.Combine(_storageService.StorageDirectory, "ImportedFolders");
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
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageSize);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CardTextWidth);
				_columns = value;
				OnColumnsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Columns);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageSize);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CardTextWidth);
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
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageSize);
				_rows = value;
				OnRowsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Rows);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageSize);
			}
		}
	}

	public int IconSize
	{
		get
		{
			return _iconSize;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_iconSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconSize);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CardWidth);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CardHeight);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CardTextWidth);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconCornerRadius);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FolderCornerRadius);
				_iconSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconSize);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CardWidth);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CardHeight);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CardTextWidth);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconCornerRadius);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FolderCornerRadius);
			}
		}
	}

	public bool IsOpen
	{
		get
		{
			return _isOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOpen);
				_isOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
			}
		}
	}

	public bool IsScanning
	{
		get
		{
			return _isScanning;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isScanning, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsScanning);
				_isScanning = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsScanning);
			}
		}
	}

	public string SearchText
	{
		get
		{
			return _searchText;
		}
		[MemberNotNull("_searchText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_searchText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchText);
				_searchText = value;
				OnSearchTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchText);
			}
		}
	}

	public int CurrentPage
	{
		get
		{
			return _currentPage;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_currentPage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentPage);
				_currentPage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentPage);
			}
		}
	}

	public int TotalPages
	{
		get
		{
			return _totalPages;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_totalPages, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalPages);
				_totalPages = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalPages);
			}
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		[MemberNotNull("_statusMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_statusMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StatusMessage);
				_statusMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StatusMessage);
			}
		}
	}

	public bool IsSearchActive
	{
		get
		{
			return _isSearchActive;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isSearchActive, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSearchActive);
				_isSearchActive = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSearchActive);
			}
		}
	}

	public bool IsSearchRevealedByHover
	{
		get
		{
			return _isSearchRevealedByHover;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isSearchRevealedByHover, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSearchRevealedByHover);
				_isSearchRevealedByHover = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSearchRevealedByHover);
			}
		}
	}

	public bool IsSearchSlidDown
	{
		get
		{
			return _isSearchSlidDown;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isSearchSlidDown, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSearchSlidDown);
				_isSearchSlidDown = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSearchSlidDown);
			}
		}
	}

	public AppItem? ActiveFolder
	{
		get
		{
			return _activeFolder;
		}
		set
		{
			if (!EqualityComparer<AppItem>.Default.Equals(_activeFolder, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ActiveFolder);
				_activeFolder = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ActiveFolder);
			}
		}
	}

	public bool IsFolderOpen
	{
		get
		{
			return _isFolderOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isFolderOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsFolderOpen);
				_isFolderOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsFolderOpen);
			}
		}
	}

	public ObservableCollection<AppItem> CurrentPageApps
	{
		get
		{
			return _currentPageApps;
		}
		[MemberNotNull("_currentPageApps")]
		set
		{
			if (!EqualityComparer<ObservableCollection<AppItem>>.Default.Equals(_currentPageApps, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentPageApps);
				_currentPageApps = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentPageApps);
			}
		}
	}

	public ObservableCollection<PageIndicatorItem> PageIndicators
	{
		get
		{
			return _pageIndicators;
		}
		[MemberNotNull("_pageIndicators")]
		set
		{
			if (!EqualityComparer<ObservableCollection<PageIndicatorItem>>.Default.Equals(_pageIndicators, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageIndicators);
				_pageIndicators = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageIndicators);
			}
		}
	}

	public ObservableCollection<AppItem> HiddenApps
	{
		get
		{
			return _hiddenApps;
		}
		[MemberNotNull("_hiddenApps")]
		set
		{
			if (!EqualityComparer<ObservableCollection<AppItem>>.Default.Equals(_hiddenApps, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HiddenApps);
				_hiddenApps = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HiddenApps);
			}
		}
	}

	public ObservableCollection<AppItem> DockApps
	{
		get
		{
			return _dockApps;
		}
		[MemberNotNull("_dockApps")]
		set
		{
			if (!EqualityComparer<ObservableCollection<AppItem>>.Default.Equals(_dockApps, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockApps);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDockVisible);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDockEmpty);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockVisibility);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockRowWidth);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockBaseWidth);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ViewportMargin);
				_dockApps = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockApps);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDockVisible);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDockEmpty);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockVisibility);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockRowWidth);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockBaseWidth);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ViewportMargin);
			}
		}
	}

	public bool IsItemDragActive
	{
		get
		{
			return _isItemDragActive;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isItemDragActive, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsItemDragActive);
				_isItemDragActive = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsItemDragActive);
			}
		}
	}

	public bool IsPageSwiping
	{
		get
		{
			return _isPageSwiping;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isPageSwiping, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsPageSwiping);
				_isPageSwiping = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsPageSwiping);
			}
		}
	}

	public string ClockTime
	{
		get
		{
			return _clockTime;
		}
		[MemberNotNull("_clockTime")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockTime, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockTime);
				_clockTime = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockTime);
			}
		}
	}

	public string ClockSeconds
	{
		get
		{
			return _clockSeconds;
		}
		[MemberNotNull("_clockSeconds")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockSeconds, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockSeconds);
				_clockSeconds = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockSeconds);
			}
		}
	}

	public string ClockDate
	{
		get
		{
			return _clockDate;
		}
		[MemberNotNull("_clockDate")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockDate, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockDate);
				_clockDate = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockDate);
			}
		}
	}

	public string ClockFullDate
	{
		get
		{
			return _clockFullDate;
		}
		[MemberNotNull("_clockFullDate")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockFullDate, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockFullDate);
				_clockFullDate = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockFullDate);
			}
		}
	}

	public string ClockDayOfWeek
	{
		get
		{
			return _clockDayOfWeek;
		}
		[MemberNotNull("_clockDayOfWeek")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockDayOfWeek, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockDayOfWeek);
				_clockDayOfWeek = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockDayOfWeek);
			}
		}
	}

	public string ClockLunar
	{
		get
		{
			return _clockLunar;
		}
		[MemberNotNull("_clockLunar")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockLunar, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockLunar);
				_clockLunar = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockLunar);
			}
		}
	}

	public string ClockGreeting
	{
		get
		{
			return _clockGreeting;
		}
		[MemberNotNull("_clockGreeting")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_clockGreeting, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ClockGreeting);
				_clockGreeting = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ClockGreeting);
			}
		}
	}

	public double CpuUsage
	{
		get
		{
			return _cpuUsage;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_cpuUsage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CpuUsage);
				_cpuUsage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CpuUsage);
			}
		}
	}

	public double MemoryUsage
	{
		get
		{
			return _memoryUsage;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_memoryUsage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MemoryUsage);
				_memoryUsage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MemoryUsage);
			}
		}
	}

	public double TotalMemoryGb
	{
		get
		{
			return _totalMemoryGb;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_totalMemoryGb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalMemoryGb);
				_totalMemoryGb = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalMemoryGb);
			}
		}
	}

	public double UsedMemoryGb
	{
		get
		{
			return _usedMemoryGb;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_usedMemoryGb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UsedMemoryGb);
				_usedMemoryGb = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UsedMemoryGb);
			}
		}
	}

	public string MemoryText
	{
		get
		{
			return _memoryText;
		}
		[MemberNotNull("_memoryText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_memoryText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MemoryText);
				_memoryText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MemoryText);
			}
		}
	}

	public bool IsWidgetBoardOpen
	{
		get
		{
			return _isWidgetBoardOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isWidgetBoardOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsWidgetBoardOpen);
				_isWidgetBoardOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsWidgetBoardOpen);
			}
		}
	}

	public bool HasClockWidgetOnGrid
	{
		get
		{
			return _hasClockWidgetOnGrid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasClockWidgetOnGrid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasClockWidgetOnGrid);
				_hasClockWidgetOnGrid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasClockWidgetOnGrid);
			}
		}
	}

	public bool HasMonitorWidgetOnGrid
	{
		get
		{
			return _hasMonitorWidgetOnGrid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasMonitorWidgetOnGrid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasMonitorWidgetOnGrid);
				_hasMonitorWidgetOnGrid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasMonitorWidgetOnGrid);
			}
		}
	}

	public bool IsIconStyleSwitching
	{
		get
		{
			return _isIconStyleSwitching;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isIconStyleSwitching, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsIconStyleSwitching);
				_isIconStyleSwitching = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsIconStyleSwitching);
			}
		}
	}

	public double IconStyleSwitchProgress
	{
		get
		{
			return _iconStyleSwitchProgress;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_iconStyleSwitchProgress, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconStyleSwitchProgress);
				_iconStyleSwitchProgress = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconStyleSwitchProgress);
			}
		}
	}

	public string IconStyleSwitchStatus
	{
		get
		{
			return _iconStyleSwitchStatus;
		}
		[MemberNotNull("_iconStyleSwitchStatus")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_iconStyleSwitchStatus, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconStyleSwitchStatus);
				_iconStyleSwitchStatus = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconStyleSwitchStatus);
			}
		}
	}

	public string SceneSwitchFeedback
	{
		get
		{
			return _sceneSwitchFeedback;
		}
		[MemberNotNull("_sceneSwitchFeedback")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_sceneSwitchFeedback, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SceneSwitchFeedback);
				_sceneSwitchFeedback = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SceneSwitchFeedback);
			}
		}
	}

	public string ManualAddFeedback
	{
		get
		{
			return _manualAddFeedback;
		}
		[MemberNotNull("_manualAddFeedback")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_manualAddFeedback, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ManualAddFeedback);
				_manualAddFeedback = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ManualAddFeedback);
			}
		}
	}

	public IRelayCommand DisableDockCommand => disableDockCommand ?? (disableDockCommand = new RelayCommand(DisableDock));

	public IAsyncRelayCommand RescanApplicationsCommand => rescanApplicationsCommand ?? (rescanApplicationsCommand = new AsyncRelayCommand(RescanApplicationsAsync));

	public IAsyncRelayCommand<AppItem?> ToggleDockCommand => toggleDockCommand ?? (toggleDockCommand = new AsyncRelayCommand<AppItem>(ToggleDockAsync));

	public IAsyncRelayCommand<AppItem?> HideAppCommand => hideAppCommand ?? (hideAppCommand = new AsyncRelayCommand<AppItem>(HideApp));

	public IAsyncRelayCommand UnhideAllCommand => unhideAllCommand ?? (unhideAllCommand = new AsyncRelayCommand(UnhideAll));

	public IAsyncRelayCommand<AppItem?> UnhideAppCommand => unhideAppCommand ?? (unhideAppCommand = new AsyncRelayCommand<AppItem>(UnhideApp));

	public IAsyncRelayCommand AddApplicationByPickingCommand => addApplicationByPickingCommand ?? (addApplicationByPickingCommand = new AsyncRelayCommand(AddApplicationByPickingAsync));

	public IAsyncRelayCommand<AppItem?> RemoveManualAppCommand => removeManualAppCommand ?? (removeManualAppCommand = new AsyncRelayCommand<AppItem>(RemoveManualApp));

	public IAsyncRelayCommand<AppItem?> RestoreIconCommand => restoreIconCommand ?? (restoreIconCommand = new AsyncRelayCommand<AppItem>(RestoreIcon));

	public IRelayCommand<AppItem?> LaunchAppCommand => launchAppCommand ?? (launchAppCommand = new RelayCommand<AppItem>(LaunchApp));

	public IRelayCommand<AppItem?> OpenFolderCommand => openFolderCommand ?? (openFolderCommand = new RelayCommand<AppItem>(OpenFolder));

	public IRelayCommand CloseFolderCommand => closeFolderCommand ?? (closeFolderCommand = new RelayCommand(CloseFolder));

	public IAsyncRelayCommand<AppItem?> CreateFolderCommand => createFolderCommand ?? (createFolderCommand = new AsyncRelayCommand<AppItem>(CreateFolder));

	public IAsyncRelayCommand<AppItem?> RemoveFromFolderCommand => removeFromFolderCommand ?? (removeFromFolderCommand = new AsyncRelayCommand<AppItem>(RemoveFromFolder));

	public IAsyncRelayCommand<AppItem?> DissolveFolderCommand => dissolveFolderCommand ?? (dissolveFolderCommand = new AsyncRelayCommand<AppItem>(DissolveFolder));

	public IAsyncRelayCommand<AppItem?> RestoreNameCommand => restoreNameCommand ?? (restoreNameCommand = new AsyncRelayCommand<AppItem>(RestoreName));

	public IRelayCommand<AppItem?> RunAsAdminCommand => runAsAdminCommand ?? (runAsAdminCommand = new RelayCommand<AppItem>(RunAsAdmin));

	public IRelayCommand<AppItem?> ShowInFolderCommand => showInFolderCommand ?? (showInFolderCommand = new RelayCommand<AppItem>(ShowInFolder));

	public IRelayCommand<AppItem?> RequestUninstallCommand => requestUninstallCommand ?? (requestUninstallCommand = new RelayCommand<AppItem>(RequestUninstall));

	public IRelayCommand NextPageCommand => nextPageCommand ?? (nextPageCommand = new RelayCommand(NextPage));

	public IRelayCommand PreviousPageCommand => previousPageCommand ?? (previousPageCommand = new RelayCommand(PreviousPage));

	public IRelayCommand<int> SelectPageCommand => selectPageCommand ?? (selectPageCommand = new RelayCommand<int>(SelectPage));

	public IRelayCommand ToggleLaunchpadCommand => toggleLaunchpadCommand ?? (toggleLaunchpadCommand = new RelayCommand(ToggleLaunchpad));

	public IRelayCommand OpenLaunchpadCommand => openLaunchpadCommand ?? (openLaunchpadCommand = new RelayCommand(OpenLaunchpad));

	public IRelayCommand CloseLaunchpadCommand => closeLaunchpadCommand ?? (closeLaunchpadCommand = new RelayCommand(CloseLaunchpad));

	public IRelayCommand ToggleWidgetBoardCommand => toggleWidgetBoardCommand ?? (toggleWidgetBoardCommand = new RelayCommand(ToggleWidgetBoard));

	public IRelayCommand OpenWidgetBoardCommand => openWidgetBoardCommand ?? (openWidgetBoardCommand = new RelayCommand(OpenWidgetBoard));

	public IRelayCommand CloseWidgetBoardCommand => closeWidgetBoardCommand ?? (closeWidgetBoardCommand = new RelayCommand(CloseWidgetBoard));

	public IRelayCommand OpenDateAndTimeSettingsCommand => openDateAndTimeSettingsCommand ?? (openDateAndTimeSettingsCommand = new RelayCommand(OpenDateAndTimeSettings));

	public IRelayCommand OpenTaskManagerCommand => openTaskManagerCommand ?? (openTaskManagerCommand = new RelayCommand(OpenTaskManager));

	public IAsyncRelayCommand<string?> ToggleWidgetOnGridCommand => toggleWidgetOnGridCommand ?? (toggleWidgetOnGridCommand = new AsyncRelayCommand<string>(ToggleWidgetOnGridAsync));

	public IAsyncRelayCommand<string?> AddWidgetToGridCommand => addWidgetToGridCommand ?? (addWidgetToGridCommand = new AsyncRelayCommand<string>(AddWidgetToGridAsync));

	public IAsyncRelayCommand<string?> MoveWidgetToFrontCommand => moveWidgetToFrontCommand ?? (moveWidgetToFrontCommand = new AsyncRelayCommand<string>(MoveWidgetToFrontAsync));

	public IAsyncRelayCommand<AppItem?> RemoveWidgetCommand => removeWidgetCommand ?? (removeWidgetCommand = new AsyncRelayCommand<AppItem>(RemoveWidgetAsync));

	public IRelayCommand ClearSearchCommand => clearSearchCommand ?? (clearSearchCommand = new RelayCommand(ClearSearch));

	public event Action<double>? DockSizeChangeRequested;

	public event Action? RequestClose;

	public event Action? AppLaunchSucceeded;

	public event Action? RequestOpen;

	public event Action? RequestCloseSettings;

	public event Action<string?>? RequestOpenSettings;

	public event Action<int, int>? RequestPageTransition;

	public event Action<int>? RequestPageStep;

	public event Action<int>? RequestBoundaryNudge;

	public event Action? DockContentsReplaced;

	public event Action? PageContentsReplaced;

	public void SetDockPresentationSize(double size)
	{
		_dockPresentedSize = size;
		OnPropertyChanged("DockIconSize");
		OnPropertyChanged("MaxDockItems");
		OnPropertyChanged("DockSlot");
		OnPropertyChanged("DockBarHeight");
		OnPropertyChanged("DockRowWidth");
		OnPropertyChanged("DockBaseWidth");
		OnPropertyChanged("DockShelfWidth");
		OnPropertyChanged("DockIconCornerRadius");
		OnPropertyChanged("ViewportMargin");
		LayoutDockSlots();
	}

	[RelayCommand]
	private void DisableDock()
	{
		_settings.EnableDock = false;
	}

	public void UpdateWidgetOnGridStatus()
	{
		HasClockWidgetOnGrid = _allApps.Any((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, "Clock", StringComparison.OrdinalIgnoreCase));
		HasMonitorWidgetOnGrid = _allApps.Any((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, "SystemMonitor", StringComparison.OrdinalIgnoreCase));
	}

	public MainViewModel(IStorageService storageService, IAppScannerService scannerService, IIconService iconService, IAppLauncherService launcherService, ISearchService searchService, IWidgetService? widgetService = null)
	{
		_storageService = storageService;
		_scannerService = scannerService;
		_iconService = iconService;
		_launcherService = launcherService;
		_searchService = searchService;
		_widgetService = widgetService ?? new WidgetService(new SystemMonitorService());
		_widgetService.Updated += OnWidgetUpdated;
		_runningMonitor.Changed += OnRunningNamesChanged;
		_searchDebounceTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(25.0)
		};
		_searchDebounceTimer.Tick += (object? s, EventArgs e) =>
		{
			_searchDebounceTimer.Stop();
			RefreshDisplayList(resetPage: true);
		};
	}

	public async Task InitializeAsync()
	{
		_ = 4;
		try
		{
			if (!_settingsAdopted)
			{
				ApplyLayoutSettings(await _storageService.LoadSettingsAsync());
			}
			IconPack iconPack = IconPackService.Find(_settings.IconPack);
			_iconService.SetActivePack(iconPack);
			_iconService.SetAutoTile(_settings.IconAutoTile);
			_iconService.SetFrostedTile(_settings.IconFrostedTile);
			App.Log($"[PACK] startup '{iconPack?.Name ?? "系统图标"}' ({iconPack?.Icons.Count ?? 0} keys)" + (_settings.IconAutoTile ? " +auto-plate" : string.Empty));
			await LoadManualAppsAsync();
			List<AppItem> cachedApps = await _storageService.LoadApplicationsAsync();
			cachedApps.RemoveAll(AppScannerService.IsFluoriteApplication);
			if (cachedApps.Count > 0)
			{
				bool groupedTools = false;
				Application.Current.Dispatcher.Invoke(() =>
				{
					_allApps.Clear();
					List<AppItem> list = new List<AppItem>();
					HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (AppItem item in cachedApps)
					{
						if (item.IsWidget && !string.IsNullOrEmpty(item.WidgetType))
						{
							if (hashSet.Add(item.WidgetType))
							{
								list.Add(item);
							}
						}
						else
						{
							list.Add(item);
						}
					}
					_allApps.AddRange(list);
					groupedTools = SystemToolsGrouping.Group(_allApps, _manualApps);
					ApplySorting();
					RefreshDisplayList();
					UpdateWidgetOnGridStatus();
					StatusMessage = $"{_allApps.Count} applications loaded";
				});
				if (groupedTools)
				{
					await _storageService.SaveApplicationsAsync(_allApps);
				}
				TriggerIconLoading(_allApps);
				Task.Run(async () =>
				{
					_ = 2;
					try
					{
						await Task.Delay(3000);
						while (IsOpen)
						{
							await Task.Delay(1500);
						}
						await RescanApplicationsAsync();
					}
					catch
					{
					}
				});
			}
			else
			{
				IsScanning = true;
				StatusMessage = "Scanning applications...";
				await RescanApplicationsAsync();
			}
		}
		catch (Exception value)
		{
			App.Log($"InitializeAsync error: {value}");
			StatusMessage = "Failed to load applications";
		}
		finally
		{
			IsScanning = false;
			CompleteDockStartupAsync(_visibleIconsReady);
		}
	}

	private async Task CompleteDockStartupAsync(Task iconsReady)
	{
		await iconsReady;
		foreach (AppItem item in DockApps.Where((AppItem a) => a.Icon == null))
		{
			DrawingImage drawingImage = new DrawingImage(new GeometryDrawing(Brushes.SteelBlue, new Pen(Brushes.White, 3.0), new RectangleGeometry(new Rect(4.0, 4.0, 56.0, 56.0), 12.0, 12.0)));
			drawingImage.Freeze();
			item.Icon = drawingImage;
		}
		HasCompletedInitialization = true;
	}

	public void ApplyLayoutSettings(AppSettings settings, bool resetPage = true)
	{
		_settingsAdopted = true;
		if (_settings != settings)
		{
			_settings.PropertyChanged -= OnSettingsPropertyChanged;
			_settings = settings;
			SetDockPresentationSize((settings.DockIconSize > 0.0) ? settings.DockIconSize : 56.0);
			_settings.PropertyChanged += OnSettingsPropertyChanged;
			OnPropertyChanged("Settings");
		}
		Columns = Math.Clamp(settings.Columns, 4, 10);
		Rows = Math.Clamp(settings.Rows, 2, 6);
		settings.PageMarginX = Math.Clamp(settings.PageMarginX, 10.0, 320.0);
		settings.PageMarginY = Math.Clamp(settings.PageMarginY, 10.0, 240.0);
		settings.ViewportMarginX = Math.Clamp(settings.ViewportMarginX, 10.0, 320.0);
		settings.ViewportMarginTop = Math.Clamp(settings.ViewportMarginTop, 10.0, 240.0);
		if (_gridViewport.Width > 0.0)
		{
			ApplyAutoIconSize();
		}
		UpdateIconResolution();
		OnPropertyChanged("PageMargin");
		OnPropertyChanged("ViewportMargin");
		RefreshDisplayList(resetPage);
	}

	public void ApplySettingsBatch(Action edit)
	{
		_batchSettings = true;
		try
		{
			edit();
		}
		finally
		{
			_batchSettings = false;
			_iconService.SetAutoTile(_settings.IconAutoTile);
			_iconService.SetFrostedTile(_settings.IconFrostedTile);
			SwitchIconPack(_settings.IconPack);
			SetDockPresentationSize(_settings.DockIconSize);
			OnPropertyChanged(string.Empty);
		}
	}

	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_batchSettings)
		{
			return;
		}
		string propertyName = e.PropertyName;
		if (propertyName == null)
		{
			return;
		}
		switch (propertyName.Length)
		{
		case 11:
			switch (propertyName[10])
			{
			default:
				return;
			case 'X':
				if (!(propertyName == "PageMarginX"))
				{
					return;
				}
				break;
			case 'Y':
				if (!(propertyName == "PageMarginY"))
				{
					return;
				}
				break;
			}
			OnPropertyChanged("PageMargin");
			if (_gridViewport.Width > 0.0)
			{
				ApplyAutoIconSize();
			}
			break;
		case 15:
		{
			char c = propertyName[0];
			if ((uint)c <= 73u)
			{
				switch (c)
				{
				default:
					return;
				case 'I':
					break;
				case 'D':
					if (propertyName == "DockIconSpacing")
					{
						OnPropertyChanged("DockGap");
						SetDockPresentationSize(DockIconSize);
					}
					return;
				}
				if (!(propertyName == "IconFrostedTile"))
				{
					break;
				}
				goto IL_0263;
			}
			if (c != 'S')
			{
				if (c != 'V' || !(propertyName == "ViewportMarginX"))
				{
					break;
				}
				goto IL_020e;
			}
			if (!(propertyName == "ShowDesktopDock"))
			{
				break;
			}
			goto IL_02fb;
		}
		case 12:
			switch (propertyName[0])
			{
			default:
				return;
			case 'I':
				break;
			case 'D':
				if (propertyName == "DockIconSize")
				{
					double num = ((_settings.DockIconSize > 0.0) ? _settings.DockIconSize : 56.0);
					if (DockSizeChangeRequested != null)
					{
						DockSizeChangeRequested(num);
					}
					else
					{
						SetDockPresentationSize(num);
					}
					UpdateIconResolution();
				}
				return;
			}
			if (!(propertyName == "IconAutoTile"))
			{
				break;
			}
			goto IL_0263;
		case 17:
			if (!(propertyName == "ViewportMarginTop"))
			{
				break;
			}
			goto IL_020e;
		case 9:
			if (propertyName == "IconShape")
			{
				OnPropertyChanged("IconCornerRadius");
				OnPropertyChanged("FolderCornerRadius");
			}
			break;
		case 13:
			if (propertyName == "IconSizeScale" && _gridViewport.Width > 0.0)
			{
				ApplyAutoIconSize();
			}
			break;
		case 8:
			if (propertyName == "IconPack")
			{
				SwitchIconPack(_settings.IconPack);
			}
			break;
		case 10:
			if (propertyName == "EnableDock")
			{
				OnPropertyChanged("IsDockEmpty");
				OnPropertyChanged("IsDockVisible");
				OnPropertyChanged("DockVisibility");
				OnPropertyChanged("ViewportMargin");
				RefreshDisplayCore(resetPage: false, DockGridImpact.Full);
			}
			break;
		case 18:
			if (propertyName == "ShowOpenAppsInDock")
			{
				RebuildDockIfChanged();
			}
			break;
		case 26:
			if (!(propertyName == "EnableDesktopDockHotCorner"))
			{
				break;
			}
			goto IL_02fb;
		case 16:
			if (!(propertyName == "DockMagnifyScale"))
			{
				break;
			}
			goto IL_037c;
		case 23:
			if (!(propertyName == "EnableDockMagnification"))
			{
				break;
			}
			goto IL_037c;
		case 14:
		case 19:
		case 20:
		case 21:
		case 22:
		case 24:
		case 25:
			break;
			IL_037c:
			OnPropertyChanged("MaxDockItems");
			UpdateIconResolution();
			break;
			IL_02fb:
			OnPropertyChanged("MaxDockItems");
			UpdateRunningPolling();
			UpdateIconResolution();
			break;
			IL_020e:
			OnPropertyChanged("ViewportMargin");
			break;
			IL_0263:
			_iconService.SetAutoTile(_settings.IconAutoTile);
			_iconService.SetFrostedTile(_settings.IconFrostedTile);
			SwitchTileIconsAsync();
			App.Log("[TILE] auto-plate " + (_settings.IconAutoTile ? "on" : "off"));
			break;
		}
	}

	public void SetDesktopDockDeviceScale(double scale)
	{
		if (double.IsFinite(scale) && !(scale <= 0.0) && !(Math.Abs(scale - _desktopDockDeviceScale) < 0.001))
		{
			_desktopDockDeviceScale = scale;
			UpdateIconResolution();
		}
	}

	public void SetGridViewportSize(Size size, double deviceScale = 1.0)
	{
		deviceScale = ((double.IsFinite(deviceScale) && deviceScale > 0.0) ? deviceScale : 1.0);
		if (!(Math.Abs(size.Width - _gridViewport.Width) < 1.0) || !(Math.Abs(size.Height - _gridViewport.Height) < 1.0) || !(Math.Abs(deviceScale - _gridDeviceScale) < 0.001))
		{
			_gridDeviceScale = deviceScale;
			_gridViewport = size;
			OnPropertyChanged("MaxDockItems");
			ApplyAutoIconSize();
			OnPropertyChanged("CardTextWidth");
		}
	}

	private void ApplyAutoIconSize()
	{
		if (!(_gridViewport.Width <= 0.0) && !(_gridViewport.Height <= 0.0))
		{
			double num = Math.Max(100.0, _gridViewport.Width - _settings.PageMarginX * 2.0);
			double num2 = Math.Max(100.0, _gridViewport.Height - _settings.PageMarginY * 2.0);
			double num3 = num / (double)Math.Max(1, Columns);
			double num4 = num2 / (double)Math.Max(1, Rows);
			int num5 = (int)Math.Clamp(Math.Min(num3 * 0.26, num4 * 0.32), 36.0, 160.0);
			double num6 = Math.Max(24.0, Math.Min(num3 - 50.0, num4 - 54.0));
			int value = (int)Math.Round((double)num5 * Math.Clamp(_settings.IconSizeScale, 50.0, 250.0) / 100.0);
			IconSize = Math.Clamp(value, 24, (int)num6);
			UpdateIconResolution();
		}
	}

	private void UpdateIconResolution()
	{
		double num = Math.Clamp(_settings.DockIconSize, 36.0, 84.0) * (_settings.EnableDockMagnification ? Math.Clamp(_settings.DockMagnifyScale, 1.0, 2.5) : 1.0);
		double num2 = ((_settings.EnableDock && (_settings.ShowDesktopDock || _settings.EnableDesktopDockHotCorner)) ? Math.Max(_gridDeviceScale, _desktopDockDeviceScale) : _gridDeviceScale);
		int pixelSize = (int)Math.Ceiling(Math.Max((double)IconSize * 1.25 * _gridDeviceScale, num * num2));
		if (!_iconService.SetPixelSize(pixelSize))
		{
			return;
		}
		foreach (AppItem item in AllAppItems())
		{
			item.Icon = null;
		}
		TriggerIconLoading(AllAppItems());
	}

	[RelayCommand]
	public async Task RescanApplicationsAsync()
	{
		await RescanApplicationsCoreAsync(updateWhileOpen: false);
	}

	public Task RescanApplicationsImmediatelyAsync()
	{
		return RescanApplicationsCoreAsync(updateWhileOpen: true);
	}

	private async Task RescanApplicationsCoreAsync(bool updateWhileOpen)
	{
		_scanCts?.Cancel();
		_scanCts = new CancellationTokenSource();
		CancellationToken ct = _scanCts.Token;
		_lastScanUtc = DateTime.UtcNow;
		if (_allApps.Count == 0)
		{
			IsScanning = true;
			StatusMessage = "Scanning applications...";
		}
		try
		{
			List<AppItem> existingList = _allApps.ToList();
			List<AppItem> freshApps = await _scannerService.ScanApplicationsAsync(existingList, _manualApps, _settings.ScanSteamGames, ct);
			ct.ThrowIfCancellationRequested();
			Application.Current.Dispatcher.Invoke(() =>
			{
				HashSet<string> hashSet = new HashSet<string>(from a in existingList
					where !a.IsFolder && !a.IsWidget
					select a.ExecutablePath, StringComparer.OrdinalIgnoreCase);
				HashSet<string> hashSet2 = new HashSet<string>(from a in freshApps
					where !a.IsFolder && !a.IsWidget
					select a.ExecutablePath, StringComparer.OrdinalIgnoreCase);
				if (hashSet.SetEquals(hashSet2) && !SystemToolsGrouping.NeedsGrouping(freshApps, _manualApps))
				{
					App.Log($"[RESCAN] App set identical ({existingList.Count} apps), skipping UI rebuild.");
				}
				else if (IsOpen && !updateWhileOpen)
				{
					App.Log("[RESCAN] App set changed but Launchpad is open; deferring update until close.");
				}
				else
				{
					Dictionary<string, ImageSource> dictionary = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
					foreach (AppItem allApp in _allApps)
					{
						if (allApp.Icon != null && !string.IsNullOrEmpty(allApp.ExecutablePath))
						{
							dictionary.TryAdd(allApp.ExecutablePath, allApp.Icon);
						}
					}
					List<AppItem> list = existingList.Where((AppItem a) => a.IsWidget).ToList();
					_allApps.Clear();
					foreach (AppItem item in freshApps)
					{
						if (dictionary.TryGetValue(item.ExecutablePath, out var value))
						{
							item.Icon = value;
						}
						_allApps.Add(item);
					}
					HashSet<string> hashSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (AppItem item2 in list)
					{
						if (!string.IsNullOrEmpty(item2.WidgetType) && hashSet3.Add(item2.WidgetType))
						{
							_allApps.Add(item2);
						}
					}
					UpdateWidgetOnGridStatus();
					RestoreUserFolders(existingList, freshApps);
					SystemToolsGrouping.Group(_allApps, _manualApps);
					ApplySorting();
					RefreshDisplayList();
					StatusMessage = $"{_allApps.Count} applications";
				}
			});
			await _storageService.SaveApplicationsAsync(_allApps);
			App.Log($"Scan complete: {freshApps.Count} apps, {freshApps.Count((AppItem a) => a.Source == "Manual")} manual");
			TriggerIconLoading(_allApps);
		}
		catch (OperationCanceledException)
		{
			App.Log("App scanning was cancelled.");
		}
		catch (Exception ex2)
		{
			App.Log("Error during application scan: " + ex2.Message);
			StatusMessage = "Scan completed with errors";
		}
		finally
		{
			IsScanning = false;
		}
	}

	private void RestoreUserFolders(List<AppItem> previousApps, List<AppItem> freshApps)
	{
		List<AppItem> list = previousApps.Where((AppItem a) => a.IsFolder && a.Children.Count > 0).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Dictionary<string, AppItem> dictionary = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
		foreach (AppItem freshApp in freshApps)
		{
			if (!freshApp.IsFolder && !string.IsNullOrEmpty(freshApp.ExecutablePath) && !dictionary.ContainsKey(freshApp.ExecutablePath))
			{
				dictionary[freshApp.ExecutablePath] = freshApp;
			}
		}
		foreach (AppItem item in list)
		{
			AppItem appItem = new AppItem
			{
				Id = item.Id,
				Name = item.Name,
				Category = item.Category,
				CustomName = item.CustomName,
				IsHidden = item.IsHidden,
				IsFolder = true,
				SortIndex = item.SortIndex
			};
			foreach (AppItem child in item.Children)
			{
				if (!string.IsNullOrEmpty(child.ExecutablePath) && dictionary.TryGetValue(child.ExecutablePath, out var value))
				{
					value.FolderId = appItem.Id;
					appItem.Children.Add(value);
				}
			}
			if (appItem.Children.Count > 0)
			{
				_allApps.Add(appItem);
			}
		}
	}

	public async Task<bool> MergeIntoFolderAsync(AppItem target, AppItem source)
	{
		if (target == null || source == null || target.Id == source.Id)
		{
			return false;
		}
		if (target.IsFolder)
		{
			if (source.IsFolder)
			{
				return false;
			}
			if (!target.Children.Any((AppItem c) => c.Id == source.Id))
			{
				source.FolderId = target.Id;
				target.Children.Add(source);
			}
		}
		else
		{
			if (source.IsFolder)
			{
				return false;
			}
			AppItem appItem = new AppItem
			{
				Name = SuggestFolderName(target.DisplayName, source.DisplayName),
				IsFolder = true,
				SortIndex = target.SortIndex
			};
			int num = _allApps.IndexOf(target);
			if (num >= 0)
			{
				_allApps.Insert(num, appItem);
			}
			else
			{
				_allApps.Add(appItem);
			}
			target.FolderId = appItem.Id;
			source.FolderId = appItem.Id;
			appItem.Children.Add(target);
			appItem.Children.Add(source);
		}
		await _storageService.SaveApplicationsAsync(_allApps);
		RefreshDisplayList();
		return true;
	}

	private static string SuggestFolderName(string first, string second)
	{
		int i = 0;
		for (int num = Math.Min(first.Length, second.Length); i < num && first[i] == second[i]; i++)
		{
		}
		string text = first.Substring(0, i).Trim();
		bool flag = i == 0 || i == first.Length || i == second.Length || char.IsWhiteSpace(first[i]) || char.IsWhiteSpace(second[i]);
		if (!((text.Length >= 3) & flag))
		{
			return "新建文件夹";
		}
		return text;
	}

	public void FlushSearchNow()
	{
		if (_searchDebounceTimer.IsEnabled)
		{
			_searchDebounceTimer.Stop();
			RefreshDisplayList(resetPage: true);
		}
	}

	public void RefreshHiddenApps()
	{
		List<AppItem> list = _allApps.Where((AppItem a) => a.IsHidden).OrderBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		int num = Math.Min(HiddenApps.Count, list.Count);
		for (int num2 = 0; num2 < num; num2++)
		{
			if (HiddenApps[num2] != list[num2])
			{
				HiddenApps[num2] = list[num2];
			}
		}
		while (HiddenApps.Count > list.Count)
		{
			HiddenApps.RemoveAt(HiddenApps.Count - 1);
		}
		for (int num3 = HiddenApps.Count; num3 < list.Count; num3++)
		{
			HiddenApps.Add(list[num3]);
		}
	}

	public void RefreshDisplayList(bool resetPage = false)
	{
		RefreshDisplayCore(resetPage, DockGridImpact.Full);
	}

	private void RefreshDisplayCore(bool resetPage, DockGridImpact dockImpact)
	{
		if (dockImpact == DockGridImpact.None)
		{
			RebuildDockIfChanged();
			return;
		}
		List<AppItem> filteredApps = _allApps.Where((AppItem a) => !a.IsHidden && string.IsNullOrEmpty(a.FolderId) && !a.IsDock).ToList();
		if (string.IsNullOrWhiteSpace(SearchText))
		{
			IsSearchActive = false;
			_filteredApps = filteredApps;
			RefreshHiddenApps();
		}
		else
		{
			IsSearchActive = true;
			List<AppItem> sourceApps = _allApps.Where((AppItem a) => !a.IsHidden && !a.IsFolder).ToList();
			_filteredApps = _searchService.Search(SearchText, sourceApps).ToList();
		}
		if (resetPage || CurrentPage < 1)
		{
			CurrentPage = 1;
		}
		UpdatePagination();
		RefreshCurrentPageItems(dockImpact == DockGridImpact.Surgical);
		if (!IsSearchActive)
		{
			RebuildDockIfChanged();
		}
	}

	private void RebuildDockIfChanged()
	{
		List<AppItem> list = DockMembers();
		List<AppItem> list2 = OpenDockEntries(list).ToList();
		foreach (AppItem openWindowEntry in _openWindowEntries)
		{
			openWindowEntry.StartsOpenWindowSection = false;
		}
		if (list2.Count > 0 && list.Count > 0)
		{
			list2[0].StartsOpenWindowSection = true;
		}
		list.AddRange(list2);
		bool flag = false;
		bool isDockVisible = IsDockVisible;
		for (int i = 0; i < list.Count; i++)
		{
			int num = IndexOfDockItem(DockApps, list[i]);
			if (num < 0)
			{
				DockApps.Insert(i, list[i]);
				flag = true;
				continue;
			}
			if (num != i)
			{
				DockApps.Move(num, i);
				flag = true;
			}
			if (DockApps[i] != list[i])
			{
				list[i].DockLeft = DockApps[i].DockLeft;
				DockApps[i] = list[i];
				flag = true;
			}
		}
		for (int num2 = DockApps.Count - 1; num2 >= 0; num2--)
		{
			if (IndexOfDockItem(list, DockApps[num2]) < 0)
			{
				DockApps.RemoveAt(num2);
				flag = true;
			}
		}
		if (!flag)
		{
			App.Log($"[DBG][DOCK-SWAP] held n={list.Count}");
			return;
		}
		App.Log($"[DBG][DOCK-SWAP] diff old={DockApps.Count} new={list.Count} new=[{string.Join(",", list.Select((AppItem a) => a.Name))}]");
		LayoutDockSlots();
		ApplyRunningMarkers();
		UpdateRunningPolling();
		RaiseDockShapeChanged(isDockVisible);
		DockContentsReplaced?.Invoke();
	}

	private void LayoutDockSlots()
	{
		double dockSlot = DockSlot;
		int count = DockApps.Count;
		double num = (double)count * dockSlot / 2.0;
		for (int i = 0; i < count; i++)
		{
			DockApps[i].DockLeft = (double)i * dockSlot - num;
		}
	}

	private static int IndexOfDockItem(IList<AppItem> items, AppItem app)
	{
		if (string.IsNullOrEmpty(app.Id))
		{
			return items.IndexOf(app);
		}
		for (int i = 0; i < items.Count; i++)
		{
			if (string.Equals(items[i].Id, app.Id, StringComparison.Ordinal))
			{
				return i;
			}
		}
		return -1;
	}

	private void RaiseDockShapeChanged(bool dockWasVisible)
	{
		OnPropertyChanged("IsDockEmpty");
		OnPropertyChanged("IsDockVisible");
		OnPropertyChanged("DockVisibility");
		OnPropertyChanged("DockRowWidth");
		OnPropertyChanged("DockBaseWidth");
		if (IsDockVisible != dockWasVisible)
		{
			OnPropertyChanged("ViewportMargin");
		}
	}

	private static string? RunningNameOf(AppItem app)
	{
		if (app.IsFolder)
		{
			return null;
		}
		string text = app.ExecutablePath?.Trim().Trim('"') ?? string.Empty;
		if (text.Length == 0)
		{
			return null;
		}
		if (string.Equals(app.Source, "UWP", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (text.Contains("://", StringComparison.Ordinal))
		{
			return null;
		}
		try
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Environment.ExpandEnvironmentVariables(text));
			return string.IsNullOrEmpty(fileNameWithoutExtension) ? null : fileNameWithoutExtension;
		}
		catch
		{
			return null;
		}
	}

	private void OnRunningNamesChanged(HashSet<string> names)
	{
		_runningNames = names;
		ApplyRunningMarkers();
	}

	private void ApplyRunningMarkers()
	{
		foreach (AppItem dockApp in DockApps)
		{
			if (!dockApp.IsOpenWindowEntry)
			{
				string text = RunningNameOf(dockApp);
				bool flag = text != null && _runningNames.Contains(text);
				if (dockApp.IsRunning != flag)
				{
					dockApp.IsRunning = flag;
					App.Log($"[RUN-MARK] {(flag ? "on " : "off")} {dockApp.DisplayName} as {text ?? "(no process name: not a win32 program)"}");
				}
			}
		}
	}

	private void UpdateRunningPolling()
	{
		_runningMonitor.SetPolling((IsOpen || _settings.ShowDesktopDock || _settings.EnableDesktopDockHotCorner) && IsDockVisible);
	}

	private void OnWidgetUpdated()
	{
		ClockTime = _widgetService.ClockTime;
		ClockSeconds = _widgetService.ClockSeconds;
		ClockDate = _widgetService.ClockDate;
		ClockFullDate = _widgetService.ClockFullDate;
		ClockDayOfWeek = _widgetService.ClockDayOfWeek;
		ClockLunar = _widgetService.ClockLunar;
		ClockGreeting = _widgetService.ClockGreeting;
		CpuUsage = _widgetService.CpuPercent;
		MemoryUsage = _widgetService.MemoryPercent;
		TotalMemoryGb = _widgetService.TotalMemoryGb;
		UsedMemoryGb = _widgetService.UsedMemoryGb;
		MemoryText = _widgetService.MemoryText;
	}

	private void UpdateWidgetPolling()
	{
		bool active = IsOpen || IsWidgetBoardOpen;
		_widgetService.SetActive(active);
	}

	private void ApplySorting()
	{
		List<AppItem> collection = (from a in _allApps
			orderby a.IsDock descending, a.SortIndex
			select a).ThenBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		_allApps.Clear();
		_allApps.AddRange(collection);
	}

	private void SwitchIconPack(string packId)
	{
		IconPack iconPack = IconPackService.Find(packId);
		_iconService.SetActivePack(iconPack);
		foreach (AppItem item in AllAppItems())
		{
			item.Icon = null;
		}
		TriggerIconLoading(AllAppItems());
		App.Log($"[PACK] switched to '{iconPack?.Name ?? "系统图标"}' ({iconPack?.Icons.Count ?? 0} keys)");
	}

	private IEnumerable<AppItem> AllAppItems()
	{
		foreach (AppItem app in _allApps)
		{
			yield return app;
			if (!app.IsFolder)
			{
				continue;
			}
			foreach (AppItem child in app.Children)
			{
				yield return child;
			}
		}
	}

	public async Task WaitForVisibleIconsAsync()
	{
		await Task.WhenAny(_visibleIconsReady, Task.Delay(2000));
	}

	private async Task SwitchTileIconsAsync()
	{
		int version = ++_iconStyleSwitchVersion;
		_iconCts?.Cancel();
		_iconCts = new CancellationTokenSource();
		CancellationToken ct = _iconCts.Token;
		IsIconStyleSwitching = true;
		IconStyleSwitchProgress = 0.0;
		IconStyleSwitchStatus = "正在切换…";
		try
		{
			await Task.Delay(100, ct);
			HashSet<AppItem> hashSet = new HashSet<AppItem>(DockApps.Concat(CurrentPageApps));
			List<AppItem> apps = (from a in AllAppItems()
				where !a.IsFolder
				select a).Distinct().OrderByDescending(hashSet.Contains).ToList();
			for (int i = 0; i < apps.Count; i += 8)
			{
				AppItem[] chunk = apps.Skip(i).Take(8).ToArray();
				ImageSource[] array = await Task.WhenAll(chunk.Select((AppItem a) => _iconService.GetIconAsync(a, ct)));
				ct.ThrowIfCancellationRequested();
				for (int num = 0; num < chunk.Length; num++)
				{
					if (array[num] != null)
					{
						chunk[num].Icon = array[num];
					}
				}
				int num2 = i + chunk.Length;
				IconStyleSwitchProgress = 100.0 * (double)num2 / (double)apps.Count;
				IconStyleSwitchStatus = $"正在切换… {num2}/{apps.Count}";
			}
			IconStyleSwitchProgress = 100.0;
			IconStyleSwitchStatus = "切换完成";
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception value)
		{
			App.Log($"[TILE] switch failed: {value}");
			IconStyleSwitchStatus = "切换未完成，请重新选择样式";
		}
		finally
		{
			if (version == _iconStyleSwitchVersion)
			{
				IsIconStyleSwitching = false;
			}
		}
	}

	private void TriggerIconLoading(IEnumerable<AppItem> apps)
	{
		_iconCts?.Cancel();
		_iconCts = new CancellationTokenSource();
		CancellationToken ct = _iconCts.Token;
		HashSet<AppItem> hashSet = new HashSet<AppItem>();
		foreach (AppItem dockApp in DockApps)
		{
			hashSet.Add(dockApp);
		}
		foreach (AppItem currentPageApp in CurrentPageApps)
		{
			hashSet.Add(currentPageApp);
			if (!currentPageApp.IsFolder)
			{
				continue;
			}
			foreach (AppItem previewChild in currentPageApp.PreviewChildren)
			{
				hashSet.Add(previewChild);
			}
		}
		List<AppItem> appList = apps.Where((AppItem a) => a.Icon == null).OrderByDescending(hashSet.Contains).ToList();
		int onScreenCount = appList.TakeWhile(hashSet.Contains).Count();
		TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_visibleIconsReady = ready.Task;
		Task.Run(async () =>
		{
			_ = 2;
			try
			{
				await _iconService.PreloadIconsAsync(appList.Take(onScreenCount).ToArray(), onScreenCount, ct);
				ready.TrySetResult();
				await Task.Delay(600, ct);
				await _iconService.PreloadIconsAsync(appList.Skip(onScreenCount).ToArray(), 0, ct);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				App.Log("Error preloading icons: " + ex2.Message);
			}
			finally
			{
				ready.TrySetResult();
			}
		});
	}

	private void UpdatePagination()
	{
		TotalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredApps.Count / (double)PageSize));
		if (CurrentPage > TotalPages)
		{
			CurrentPage = TotalPages;
		}
		if (CurrentPage < 1)
		{
			CurrentPage = 1;
		}
		while (PageIndicators.Count > TotalPages)
		{
			PageIndicators.RemoveAt(PageIndicators.Count - 1);
		}
		while (PageIndicators.Count < TotalPages)
		{
			PageIndicators.Add(new PageIndicatorItem
			{
				PageIndex = PageIndicators.Count + 1,
				IsSelected = false
			});
		}
		for (int i = 0; i < PageIndicators.Count; i++)
		{
			PageIndicators[i].IsSelected = PageIndicators[i].PageIndex == CurrentPage;
		}
	}

	public void RefreshCurrentPageItems(bool reconcileInPlace = false)
	{
		List<AppItem> list = _filteredApps.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
		if (IsSearchActive)
		{
			SyncCurrentPageItems(list);
		}
		else if (!reconcileInPlace || !ReconcilePageInPlace(list))
		{
			CurrentPageApps = new ObservableCollection<AppItem>(list);
			App.Log($"[DBG][PAGE-SWAP] Replaced CurrentPageApps instance with {list.Count} items (inPlace failed)");
		}
		foreach (PageIndicatorItem pageIndicator in PageIndicators)
		{
			pageIndicator.IsSelected = pageIndicator.PageIndex == CurrentPage;
		}
		PageContentsReplaced?.Invoke();
	}

	private void SyncCurrentPageItems(List<AppItem> desired)
	{
		ObservableCollection<AppItem> currentPageApps = CurrentPageApps;
		if (currentPageApps == null)
		{
			CurrentPageApps = new ObservableCollection<AppItem>(desired);
			return;
		}
		if (currentPageApps.Count == desired.Count)
		{
			bool flag = true;
			for (int i = 0; i < desired.Count; i++)
			{
				if (currentPageApps[i] != desired[i])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return;
			}
		}
		int num = Math.Min(currentPageApps.Count, desired.Count);
		for (int j = 0; j < num; j++)
		{
			if (currentPageApps[j] != desired[j])
			{
				currentPageApps[j] = desired[j];
			}
		}
		while (currentPageApps.Count > desired.Count)
		{
			currentPageApps.RemoveAt(currentPageApps.Count - 1);
		}
		for (int k = currentPageApps.Count; k < desired.Count; k++)
		{
			currentPageApps.Add(desired[k]);
		}
	}

	private bool ReconcilePageInPlace(List<AppItem> desired)
	{
		ObservableCollection<AppItem> currentPageApps = CurrentPageApps;
		List<AppItem> list = new List<AppItem>(currentPageApps.Count);
		foreach (AppItem item2 in currentPageApps)
		{
			if (desired.Contains(item2))
			{
				list.Add(item2);
			}
		}
		int num = currentPageApps.Count - list.Count;
		if (num > 1)
		{
			return false;
		}
		List<(int, AppItem)> list2 = new List<(int, AppItem)>();
		for (int i = 0; i < desired.Count; i++)
		{
			if (i >= list.Count || list[i] != desired[i])
			{
				list.Insert(i, desired[i]);
				list2.Add((i, desired[i]));
				if (list2.Count > 1)
				{
					return false;
				}
			}
		}
		if (num + list2.Count > 2)
		{
			return false;
		}
		for (int num2 = currentPageApps.Count - 1; num2 >= 0; num2--)
		{
			if (!desired.Contains(currentPageApps[num2]))
			{
				currentPageApps.RemoveAt(num2);
			}
		}
		foreach (var (val, item) in list2)
		{
			currentPageApps.Insert(Math.Min(val, currentPageApps.Count), item);
		}
		App.Log($"[DBG][PAGE-RECONCILE] in-place ok removals={num} arrivals={list2.Count} n={currentPageApps.Count}");
		return true;
	}

	public void RollToPage(int newPage, ObservableCollection<AppItem> primaryApps)
	{
		if (newPage < 1 || newPage > TotalPages)
		{
			return;
		}
		CurrentPageApps = primaryApps;
		CurrentPage = newPage;
		foreach (PageIndicatorItem pageIndicator in PageIndicators)
		{
			pageIndicator.IsSelected = pageIndicator.PageIndex == newPage;
		}
	}

	public void UpdateSwipeProgress(int currentPage, int targetPage, double progress)
	{
		if (PageIndicators.Count <= 1)
		{
			return;
		}
		progress = Math.Clamp(progress, 0.0, 1.0);
		for (int i = 0; i < PageIndicators.Count; i++)
		{
			PageIndicatorItem pageIndicatorItem = PageIndicators[i];
			if (pageIndicatorItem.PageIndex == currentPage)
			{
				pageIndicatorItem.SetLive(14.0 - 8.0 * progress, 0.95 - 0.6 * progress);
			}
			else if (pageIndicatorItem.PageIndex == targetPage)
			{
				pageIndicatorItem.SetLive(6.0 + 8.0 * progress, 0.35 + 0.6 * progress);
			}
			else
			{
				pageIndicatorItem.SetLive(6.0, 0.35);
			}
		}
	}

	public void ResetIndicatorProgress()
	{
		foreach (PageIndicatorItem pageIndicator in PageIndicators)
		{
			bool flag = pageIndicator.PageIndex == CurrentPage;
			pageIndicator.TweenTo(flag ? 14.0 : 6.0, flag ? 0.95 : 0.35, 170.0);
		}
	}

	[RelayCommand]
	public Task ToggleDockAsync(AppItem? app)
	{
		if (app != null)
		{
			if (!app.IsDock)
			{
				return AddToDockAsync(app);
			}
			return RemoveFromDockAsync(app);
		}
		return Task.CompletedTask;
	}

	private List<AppItem> DockMembers()
	{
		return (from a in _allApps
			where a.IsDock && !a.IsHidden && !a.IsFolder && string.IsNullOrEmpty(a.FolderId)
			orderby a.DockIndex
			select a).ThenBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static void RenumberDock(List<AppItem> members)
	{
		for (int i = 0; i < members.Count; i++)
		{
			members[i].DockIndex = i;
		}
	}

	public async Task AddToDockAsync(AppItem app, int atIndex = -1, bool deferSave = false)
	{
		if (app.IsOpenWindowEntry || app == null || app.IsFolder)
		{
			return;
		}
		List<AppItem> list = DockMembers();
		int num = ((atIndex < 0 || atIndex > list.Count) ? list.Count : atIndex);
		bool isDock = app.IsDock;
		if (app.IsDock)
		{
			int num2 = list.FindIndex((AppItem a) => a.Id == app.Id);
			if (num2 < 0 || num2 == num)
			{
				return;
			}
			list.RemoveAt(num2);
			list.Insert(Math.Min(num, list.Count), app);
		}
		else
		{
			if (list.Count >= MaxDockItems)
			{
				StatusMessage = "当前屏幕的 Dock 空间已满，可缩小图标或减小间距后继续添加";
				return;
			}
			list.Insert(num, app);
			app.IsDock = true;
		}
		RenumberDock(list);
		_settings.EnableDock = true;
		DockGridImpact dockImpact = ((!IsSearchActive && !isDock) ? DockGridImpact.Surgical : DockGridImpact.None);
		await CommitDockChangeAsync($"[DOCK] + {app.Name} at {num}", !deferSave, dockImpact);
	}

	public async Task RemoveFromDockAsync(AppItem app, int gridSlot = -1, bool deferSave = false)
	{
		if (app == null)
		{
			return;
		}
		string matchId = app.Id;
		string matchPath = app.ExecutablePath;
		foreach (AppItem allApp in _allApps)
		{
			if ((!string.IsNullOrEmpty(matchId) && allApp.Id == matchId) || (!string.IsNullOrEmpty(matchPath) && string.Equals(allApp.ExecutablePath, matchPath, StringComparison.OrdinalIgnoreCase)))
			{
				allApp.IsDock = false;
			}
		}
		app.IsDock = false;
		RenumberDock(DockMembers());
		if (DockMembers().Count == 0)
		{
			_settings.EnableDock = false;
		}
		int num = ((gridSlot < 0 || IsSearchActive) ? (-1) : ((CurrentPage - 1) * PageSize + gridSlot));
		if (num >= 0)
		{
			List<AppItem> list = _filteredApps.Where((AppItem a) => a != app && (string.IsNullOrEmpty(matchId) || a.Id != matchId) && (string.IsNullOrEmpty(matchPath) || !string.Equals(a.ExecutablePath, matchPath, StringComparison.OrdinalIgnoreCase))).ToList();
			list.Insert(Math.Clamp(num, 0, list.Count), app);
			_filteredApps = list;
			CommitRootOrderToFile();
		}
		else
		{
			int num2 = (from a in _allApps
				where !a.IsHidden && string.IsNullOrEmpty(a.FolderId) && !a.IsDock
				select a.SortIndex).DefaultIfEmpty(0).Min();
			app.SortIndex = num2 - 1;
		}
		await CommitDockChangeAsync($"[DOCK] - {app.Name} slot={num}", !deferSave, (!IsSearchActive) ? DockGridImpact.Surgical : DockGridImpact.None);
	}

	public Task SaveDockStateAsync()
	{
		return _storageService.SaveApplicationsAsync(_allApps);
	}

	private async Task CommitDockChangeAsync(string logMessage, bool saveNow = true, DockGridImpact dockImpact = DockGridImpact.Full)
	{
		App.Log(logMessage);
		Stopwatch sw = Stopwatch.StartNew();
		if (dockImpact == DockGridImpact.Full)
		{
			ApplySorting();
		}
		double tSort = sw.Elapsed.TotalMilliseconds;
		RefreshDisplayCore(resetPage: false, dockImpact);
		double tRefresh = sw.Elapsed.TotalMilliseconds - tSort;
		if (saveNow)
		{
			await _storageService.SaveApplicationsAsync(_allApps);
		}
		App.Log($"[DBG][DOCK-COMMIT] impact={dockImpact} sort={tSort:F1} refresh={tRefresh:F1} save={sw.Elapsed.TotalMilliseconds - tSort - tRefresh:F1} ms page={CurrentPageApps.Count}");
	}

	[RelayCommand]
	public async Task HideApp(AppItem? app)
	{
		if (app != null)
		{
			await RemoveApplicationsAsync(new AppItem[1] { app });
		}
	}

	public async Task RemoveApplicationsAsync(IEnumerable<AppItem> apps)
	{
		AppItem[] array = apps.Distinct().ToArray();
		foreach (AppItem app in array)
		{
			app.IsHidden = true;
			app.IsDock = false;
			App.Log("Removing from launcher: " + app.Name);
			if (!string.IsNullOrEmpty(app.FolderId))
			{
				AppItem appItem = _allApps.FirstOrDefault((AppItem f) => f.Id == app.FolderId);
				appItem?.Children.Remove(app);
				app.FolderId = null;
				if (appItem != null && appItem.Children.Count == 0)
				{
					_allApps.Remove(appItem);
				}
				if (appItem != null && ActiveFolder == appItem)
				{
					CloseFolder();
				}
			}
		}
		await _storageService.SaveApplicationsAsync(_allApps);
		RefreshDisplayList();
	}

	[RelayCommand]
	public async Task UnhideAll()
	{
		List<AppItem> list = _allApps.Where((AppItem a) => a.IsHidden).ToList();
		if (list.Count == 0)
		{
			return;
		}
		foreach (AppItem item in list)
		{
			item.IsHidden = false;
		}
		App.Log($"Restored {list.Count} hidden applications");
		await _storageService.SaveApplicationsAsync(_allApps);
		RefreshDisplayList();
	}

	[RelayCommand]
	public async Task UnhideApp(AppItem? app)
	{
		if (app != null)
		{
			app.IsHidden = false;
			App.Log("Unhiding app: " + app.Name);
			await _storageService.SaveApplicationsAsync(_allApps);
			RefreshDisplayList();
		}
	}

	public async Task LoadManualAppsAsync()
	{
		_manualApps.Clear();
		List<AppItem> manualApps = _manualApps;
		manualApps.AddRange(await _storageService.LoadManualAppsAsync());
		OnPropertyChanged("ManualAppCount");
	}

	public async Task RestoreWorkspaceAsync(WorkspaceBundle bundle, AppSettings? restoredSettings = null)
	{
		_scanCts?.Cancel();
		List<AppItem> restored = bundle.Applications.Select(PersistenceSnapshot.App).ToList();
		List<AppItem> source = restored.Concat(restored.Where((AppItem a) => a.IsFolder).SelectMany((AppItem a) => a.Children)).ToList();
		HashSet<string> paths = new HashSet<string>(source.Select((AppItem a) => a.ExecutablePath), StringComparer.OrdinalIgnoreCase);
		HashSet<string> ids = new HashSet<string>(source.Select((AppItem a) => a.Id));
		int num = restored.Select((AppItem a) => a.SortIndex).DefaultIfEmpty(0).Max() + 1;
		foreach (AppItem item in from a in AllAppItems()
			where !a.IsFolder && !a.IsWidget && !paths.Contains(a.ExecutablePath) && !ids.Contains(a.Id)
			select a)
		{
			AppItem appItem = PersistenceSnapshot.App(item);
			appItem.FolderId = null;
			appItem.SortIndex = num++;
			restored.Add(appItem);
		}
		Dictionary<string, AppItem> dictionary = (from a in restored
			group a by a.Id).ToDictionary((IGrouping<string, AppItem> g) => g.Key, (IGrouping<string, AppItem> g) => g.First());
		foreach (AppItem item2 in restored.Where((AppItem a) => a.IsFolder))
		{
			for (int num2 = 0; num2 < item2.Children.Count; num2++)
			{
				if (dictionary.TryGetValue(item2.Children[num2].Id, out var value))
				{
					item2.Children[num2] = value;
				}
			}
		}
		HashSet<string> manualPaths = bundle.ManualApps.Select((AppItem a) => a.ExecutablePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		List<AppItem> second = _manualApps.Where((AppItem a) => !manualPaths.Contains(a.ExecutablePath)).ToList();
		List<AppItem> manual = bundle.ManualApps.Concat(second).Select(PersistenceSnapshot.App).ToList();
		if (!(await _storageService.SaveWorkspaceAsync(restored, manual, bundle.Folders, restoredSettings ?? Settings)))
		{
			throw new IOException("布局保存失败，原布局已保留，请检查磁盘空间和目录权限");
		}
		if (IsFolderOpen)
		{
			CloseFolder();
		}
		_allApps.Clear();
		_allApps.AddRange(restored);
		_manualApps.Clear();
		_manualApps.AddRange(manual);
		SearchText = string.Empty;
		ApplySorting();
		RefreshDisplayList(resetPage: true);
		TriggerIconLoading(_allApps);
		UpdateWidgetOnGridStatus();
		OnPropertyChanged("ManualAppCount");
	}

	public void ShowSceneSwitchFeedback(string message)
	{
		SceneSwitchFeedback = message;
		ClearSceneSwitchFeedbackAsync(++_sceneFeedbackVersion);
	}

	private async Task ClearSceneSwitchFeedbackAsync(int version)
	{
		await Task.Delay(2200);
		if (version == _sceneFeedbackVersion)
		{
			SceneSwitchFeedback = string.Empty;
		}
	}

	private async Task DismissManualAddFeedbackAsync(int version)
	{
		await Task.Delay(3000);
		if (version == _manualFeedbackVersion)
		{
			ManualAddFeedback = string.Empty;
		}
	}

	[RelayCommand]
	public async Task AddApplicationByPickingAsync()
	{
		string[] array = PickApplicationFiles?.Invoke();
		if (array == null)
		{
			StatusMessage = "已取消添加";
			return;
		}
		StatusMessage = "正在添加应用…";
		StatusMessage = await AddManualAppsAsync(array);
		ManualAddFeedback = StatusMessage;
		DismissManualAddFeedbackAsync(++_manualFeedbackVersion);
	}

	public async Task<string> AddManualAppsAsync(IEnumerable<string> paths)
	{
		int added = 0;
		int skipped = 0;
		string addedPath = null;
		foreach (string path in paths)
		{
			AppItem app = _scannerService.CreateAppFromPath(path);
			bool flag = app != null;
			if (flag)
			{
				flag = await ReAddRemovedApplicationAsync(app);
			}
			if (flag)
			{
				added++;
				addedPath = app.ExecutablePath;
				continue;
			}
			bool flag2 = app != null && (_manualApps.Any((AppItem m) => string.Equals(m.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase)) || _allApps.Any((AppItem a) => string.Equals(a.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase)));
			if ((app == null) | flag2)
			{
				skipped++;
				continue;
			}
			_manualApps.Add(app);
			added++;
			addedPath = app.ExecutablePath;
		}
		if (added > 0)
		{
			await _storageService.SaveManualAppsAsync(_manualApps);
		}
		OnPropertyChanged("ManualAppCount");
		await RescanApplicationsCoreAsync(updateWhileOpen: true);
		if (addedPath != null)
		{
			SearchText = string.Empty;
			RefreshDisplayList();
			int num = _filteredApps.FindIndex((AppItem a) => string.Equals(a.ExecutablePath, addedPath, StringComparison.OrdinalIgnoreCase));
			if (num >= 0)
			{
				CurrentPage = num / PageSize + 1;
				RefreshCurrentPageItems();
			}
		}
		App.Log($"Manual add: {added} added, {skipped} skipped");
		if (added == 0)
		{
			return "没有可添加的项目（路径无法解析，或已在列表里）";
		}
		return (skipped > 0) ? $"已添加 {added} 个项目，跳过 {skipped} 个已存在或无法解析的路径" : $"已添加 {added} 个项目";
	}

	public async Task<bool> AddSingleAppFromPathAsync(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		string text = (StatusMessage = await AddManualAppsAsync(new string[1] { path }));
		return text.StartsWith("已添加", StringComparison.Ordinal);
	}

	public async Task AddDroppedItemsAsync(IEnumerable<string> paths)
	{
		StatusMessage = await AddManualAppsAsync(paths);
		ManualAddFeedback = StatusMessage;
		DismissManualAddFeedbackAsync(++_manualFeedbackVersion);
	}

	internal void ReportFileOperationFeedback(string message)
	{
		StatusMessage = message;
		ManualAddFeedback = message;
		DismissManualAddFeedbackAsync(++_manualFeedbackVersion);
	}

	private async Task<bool> ReAddRemovedApplicationAsync(AppItem parsed)
	{
		List<AppItem> list = (from a in _allApps.Concat(_manualApps)
			where string.Equals(a.ExecutablePath, parsed.ExecutablePath, StringComparison.OrdinalIgnoreCase)
			select a).Distinct().ToList();
		if (!list.Any((AppItem a) => a.IsHidden))
		{
			return false;
		}
		foreach (AppItem item in list)
		{
			item.IsHidden = false;
			item.IsDock = false;
			item.FolderId = null;
			item.SortIndex = (from a in _allApps
				where !a.IsHidden
				select a.SortIndex).DefaultIfEmpty(0).Max() + 1;
		}
		if (!_manualApps.Any((AppItem a) => string.Equals(a.ExecutablePath, parsed.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
		{
			_manualApps.Add(list[0]);
		}
		await _storageService.SaveApplicationsAsync(_allApps);
		await _storageService.SaveManualAppsAsync(_manualApps);
		StatusMessage = "已添加「" + parsed.DisplayName + "」";
		return true;
	}

	[RelayCommand]
	public async Task RemoveManualApp(AppItem? app)
	{
		if (app != null && !(app.Source != "Manual"))
		{
			_manualApps.RemoveAll((AppItem m) => string.Equals(m.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase));
			App.Log("Removed manual app: " + app.Name);
			await _storageService.SaveManualAppsAsync(_manualApps);
			OnPropertyChanged("ManualAppCount");
			await RemoveApplicationsAsync(new AppItem[1] { app });
			StatusMessage = "已删除手动添加项「" + app.DisplayName + "」，未卸载原程序";
			ManualAddFeedback = StatusMessage;
			DismissManualAddFeedbackAsync(++_manualFeedbackVersion);
		}
	}

	public async Task ApplyIconFileAsync(AppItem app, string sourceFile)
	{
		try
		{
			Directory.CreateDirectory(UserIconDirectory);
			string target = Path.Combine(UserIconDirectory, app.Id + Path.GetExtension(sourceFile));
			File.Copy(sourceFile, target, overwrite: true);
			app.IconOverridePath = target;
			await ReloadIconAsync(app);
			App.Log("[ICON] " + app.Name + " -> " + target);
		}
		catch (Exception ex)
		{
			App.Log("[ICON] change failed for " + app.Name + ": " + ex.Message);
		}
	}

	[RelayCommand]
	public async Task RestoreIcon(AppItem? app)
	{
		if (app != null && app.HasIconOverride)
		{
			try
			{
				File.Delete(app.IconOverridePath);
			}
			catch (Exception ex)
			{
				App.Log("[ICON] could not delete " + app.IconOverridePath + ": " + ex.Message);
			}
			app.IconOverridePath = null;
			await ReloadIconAsync(app);
		}
	}

	private async Task ReloadIconAsync(AppItem app)
	{
		ImageSource imageSource = await _iconService.GetIconAsync(app);
		if (imageSource != null)
		{
			app.Icon = imageSource;
		}
		await _storageService.SaveApplicationsAsync(_allApps);
	}

	[RelayCommand]
	public void LaunchApp(AppItem? app)
	{
		if (app == null)
		{
			return;
		}
		if (app.IsOpenWindowEntry)
		{
			RestoreOpenWindow(app);
		}
		else if (DateTime.UtcNow.Ticks - LastDragEndTimeTicks < TimeSpan.FromMilliseconds(450.0).Ticks)
		{
			App.Log("LaunchApp suppressed because drag operation just completed");
		}
		else if (app.IsWidget)
		{
			if (string.Equals(app.WidgetType, "Clock", StringComparison.OrdinalIgnoreCase))
			{
				OpenDateAndTimeSettings();
			}
			else if (string.Equals(app.WidgetType, "SystemMonitor", StringComparison.OrdinalIgnoreCase))
			{
				OpenTaskManager();
			}
		}
		else if (app.IsFolder)
		{
			OpenFolder(app);
		}
		else
		{
			App.Log("Launching app: " + app.Name);
			CompleteAppLaunchAsync(app);
		}
	}

	private async Task CompleteAppLaunchAsync(AppItem app)
	{
		if (!_pendingAppLaunches.Add(app))
		{
			return;
		}
		try
		{
			var (flag, text) = await _launcherService.LaunchAsync(app);
			if (flag)
			{
				AptabaseService.Current.TrackEvent("app_launched");
				AppLaunchSucceeded?.Invoke();
				_runningMonitor.RefreshSoon();
				if (_settings.CloseAfterLaunching && !_settings.DesktopResidentLaunchpad)
				{
					CloseLaunchpad();
				}
			}
			else
			{
				StatusMessage = "Launch failed: " + text;
			}
		}
		catch (Exception ex)
		{
			StatusMessage = "启动失败：" + ex.Message;
		}
		finally
		{
			_pendingAppLaunches.Remove(app);
		}
	}

	public bool LaunchAppWithFiles(AppItem? app, IEnumerable<string> filePaths)
	{
		if (app == null || app.IsFolder)
		{
			return false;
		}
		List<string> list = filePaths?.Where((string f) => !string.IsNullOrWhiteSpace(f)).ToList();
		if (list == null || list.Count == 0)
		{
			LaunchApp(app);
			return true;
		}
		App.Log($"Launching app: {app.Name} with {list.Count} file(s)");
		if (_launcherService.LaunchWithFiles(app, list, out string errorMessage))
		{
			AptabaseService.Current.TrackEvent("app_launched_with_files");
			AppLaunchSucceeded?.Invoke();
			_runningMonitor.RefreshSoon();
			if (_settings.CloseAfterLaunching && !_settings.DesktopResidentLaunchpad)
			{
				CloseLaunchpad();
			}
			return true;
		}
		StatusMessage = "Launch failed: " + errorMessage;
		return false;
	}

	[RelayCommand]
	public void OpenFolder(AppItem? folder)
	{
		if (folder != null)
		{
			ActiveFolder = folder;
			IsFolderOpen = true;
		}
	}

	[RelayCommand]
	public void CloseFolder()
	{
		IsFolderOpen = false;
		ActiveFolder = null;
	}

	[RelayCommand]
	public async Task CreateFolder(AppItem? app)
	{
		if (app != null && !app.IsFolder && !app.IsInFolder)
		{
			AppItem appItem = new AppItem
			{
				Name = "新建文件夹",
				IsFolder = true,
				SortIndex = app.SortIndex
			};
			app.FolderId = appItem.Id;
			appItem.Children.Add(app);
			int num = _allApps.IndexOf(app);
			if (num >= 0)
			{
				_allApps.Insert(num, appItem);
			}
			else
			{
				_allApps.Add(appItem);
			}
			await _storageService.SaveApplicationsAsync(_allApps);
			RefreshDisplayList();
		}
	}

	[RelayCommand]
	public async Task RemoveFromFolder(AppItem? app)
	{
		if (app == null || string.IsNullOrEmpty(app.FolderId))
		{
			return;
		}
		AppItem appItem = _allApps.FirstOrDefault((AppItem f) => f.Id == app.FolderId);
		if (appItem != null)
		{
			appItem.Children.Remove(app);
			app.FolderId = null;
			if (appItem.Children.Count <= 1)
			{
				foreach (AppItem item in appItem.Children.ToList())
				{
					item.FolderId = null;
				}
				appItem.Children.Clear();
				_allApps.Remove(appItem);
				if (ActiveFolder == appItem)
				{
					CloseFolder();
				}
			}
		}
		await _storageService.SaveApplicationsAsync(_allApps);
		RefreshDisplayList();
	}

	[RelayCommand]
	public async Task DissolveFolder(AppItem? folder)
	{
		if (folder == null || !folder.IsFolder)
		{
			return;
		}
		foreach (AppItem item in folder.Children.ToList())
		{
			item.FolderId = null;
		}
		folder.Children.Clear();
		_allApps.Remove(folder);
		if (ActiveFolder == folder)
		{
			CloseFolder();
		}
		App.Log("Dissolved folder " + folder.Name);
		await _storageService.SaveApplicationsAsync(_allApps);
		RefreshDisplayList();
	}

	public async Task ApplyAppRenameAsync(AppItem? app, string? newName)
	{
		if (app != null && !app.IsFolder && !string.IsNullOrWhiteSpace(newName))
		{
			string text = newName.Trim();
			if (text == app.Name)
			{
				text = string.Empty;
			}
			if (!(text == app.CustomName))
			{
				app.CustomName = ((text.Length == 0) ? null : text);
				App.Log(string.IsNullOrEmpty(app.CustomName) ? ("Restored default name for " + app.Name) : ("Renamed tile to " + app.CustomName));
				await _storageService.SaveApplicationsAsync(_allApps);
				RefreshDisplayList();
			}
		}
	}

	[RelayCommand]
	public async Task RestoreName(AppItem? app)
	{
		if (app != null && app.HasCustomName)
		{
			await ApplyAppRenameAsync(app, app.Name);
		}
	}

	public async Task ApplyFolderRenameAsync(AppItem? folder, string? newName)
	{
		if (folder != null && folder.IsFolder && !string.IsNullOrWhiteSpace(newName) && !(newName == folder.Name))
		{
			folder.Name = newName.Trim();
			App.Log("Renamed folder to " + folder.Name);
			await _storageService.SaveApplicationsAsync(_allApps);
			RefreshDisplayList();
		}
	}

	[RelayCommand]
	public void RunAsAdmin(AppItem? app)
	{
		if (app != null)
		{
			if (_launcherService.RunAsAdmin(app, out string errorMessage))
			{
				CloseLaunchpad();
			}
			else
			{
				StatusMessage = "Run as admin failed: " + errorMessage;
			}
		}
	}

	[RelayCommand]
	public void ShowInFolder(AppItem? app)
	{
		if (app != null)
		{
			if (_launcherService.ShowInFolder(app, out string errorMessage))
			{
				CloseLaunchpad();
			}
			else
			{
				StatusMessage = "Show in folder failed: " + errorMessage;
			}
		}
	}

	[RelayCommand]
	public void RequestUninstall(AppItem? app)
	{
		if (app != null)
		{
			if (_launcherService.RequestUninstall(app, out string errorMessage))
			{
				CloseLaunchpad();
			}
			else
			{
				StatusMessage = "Uninstall failed: " + errorMessage;
			}
		}
	}

	public async Task ReorderAppAsync(AppItem source, AppItem target)
	{
		if (source == null || target == null || source.Id == target.Id)
		{
			return;
		}
		App.Log("ReorderAppAsync: moving " + source.Name + " to " + target.Name);
		if (IsFolderOpen && ActiveFolder != null)
		{
			int num = ActiveFolder.Children.IndexOf(source);
			int num2 = ActiveFolder.Children.IndexOf(target);
			if (num >= 0 && num2 >= 0)
			{
				ActiveFolder.Children.RemoveAt(num);
				ActiveFolder.Children.Insert(num2, source);
				await _storageService.SaveApplicationsAsync(_allApps);
				return;
			}
		}
		if (target.IsFolder && !source.IsFolder)
		{
			source.FolderId = target.Id;
			if (!target.Children.Any((AppItem c) => c.Id == source.Id))
			{
				target.Children.Add(source);
			}
			RefreshDisplayList();
			await _storageService.SaveApplicationsAsync(_allApps);
			return;
		}
		int num3 = _allApps.FindIndex((AppItem a) => a.Id == source.Id);
		int num4 = _allApps.FindIndex((AppItem a) => a.Id == target.Id);
		if (num3 >= 0 && num4 >= 0)
		{
			AppItem item = _allApps[num3];
			_allApps.RemoveAt(num3);
			_allApps.Insert(num4, item);
			for (int num5 = 0; num5 < _allApps.Count; num5++)
			{
				_allApps[num5].SortIndex = num5;
			}
			RefreshDisplayList();
			await _storageService.SaveApplicationsAsync(_allApps);
		}
	}

	public async Task PersistCurrentOrderAsync()
	{
		int num = (CurrentPage - 1) * PageSize;
		for (int i = 0; i < CurrentPageApps.Count; i++)
		{
			int num2 = num + i;
			if (num2 < _filteredApps.Count)
			{
				_filteredApps[num2] = CurrentPageApps[i];
			}
		}
		if (!IsSearchActive)
		{
			CommitRootOrderToFile();
		}
		await _storageService.SaveApplicationsAsync(_allApps);
	}

	private void CommitRootOrderToFile()
	{
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<AppItem> list = new List<AppItem>();
		foreach (AppItem filteredApp in _filteredApps)
		{
			if (filteredApp != null && (string.IsNullOrEmpty(filteredApp.Id) || seenIds.Add(filteredApp.Id)) && (filteredApp.IsFolder || filteredApp.IsWidget || string.IsNullOrEmpty(filteredApp.ExecutablePath) || seenPaths.Add(filteredApp.ExecutablePath)))
			{
				list.Add(filteredApp);
			}
		}
		_filteredApps = list;
		List<AppItem> collection = _allApps.Where((AppItem a) => (a.IsHidden || !string.IsNullOrEmpty(a.FolderId) || a.IsDock) && (string.IsNullOrEmpty(a.Id) || !seenIds.Contains(a.Id)) && (a.IsFolder || a.IsWidget || string.IsNullOrEmpty(a.ExecutablePath) || !seenPaths.Contains(a.ExecutablePath))).ToList();
		_allApps.Clear();
		_allApps.AddRange(_filteredApps);
		_allApps.AddRange(collection);
		for (int num = 0; num < _allApps.Count; num++)
		{
			_allApps[num].SortIndex = num;
		}
	}

	public void MoveAppToPage(AppItem app, int targetPage)
	{
		if (targetPage < 1 || targetPage > TotalPages)
		{
			return;
		}
		int num = _filteredApps.FindIndex((AppItem a) => a.Id == app.Id);
		if (num >= 0)
		{
			int num2 = Math.Min((targetPage - 1) * PageSize, _filteredApps.Count - 1);
			if (num2 != num)
			{
				_filteredApps.RemoveAt(num);
				_filteredApps.Insert(num2, app);
			}
		}
	}

	public List<AppItem> GetPageItems(int pageIndex)
	{
		if (pageIndex < 1 || pageIndex > TotalPages)
		{
			return new List<AppItem>();
		}
		return _filteredApps.Skip((pageIndex - 1) * PageSize).Take(PageSize).ToList();
	}

	internal void LogPageIconState(int page)
	{
		List<AppItem> pageItems = GetPageItems(page);
		App.Log($"[ICON-PAGE] page={page} count={pageItems.Count} pending={pageItems.Count((AppItem a) => a.Icon == null)} " + string.Join("; ", pageItems.Select((AppItem a) => a.Name + ":" + a.Source + ":" + ((a.Icon is BitmapSource bitmapSource) ? $"{bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}" : "missing"))));
	}

	public void CommitPageChange(int newPage)
	{
		if (newPage >= 1 && newPage <= TotalPages)
		{
			CurrentPage = newPage;
			RefreshCurrentPageItems();
		}
	}

	[RelayCommand]
	public void NextPage()
	{
		if (RequestPageStep != null)
		{
			RequestPageStep(1);
		}
		else if (CurrentPage < TotalPages)
		{
			int num = CurrentPage + 1;
			if (RequestPageTransition != null)
			{
				RequestPageTransition(num, 1);
			}
			else
			{
				CommitPageChange(num);
			}
		}
		else if (TotalPages > 1)
		{
			RequestBoundaryNudge?.Invoke(1);
		}
	}

	[RelayCommand]
	public void PreviousPage()
	{
		if (RequestPageStep != null)
		{
			RequestPageStep(-1);
		}
		else if (CurrentPage > 1)
		{
			int num = CurrentPage - 1;
			if (RequestPageTransition != null)
			{
				RequestPageTransition(num, -1);
			}
			else
			{
				CommitPageChange(num);
			}
		}
		else if (TotalPages > 1)
		{
			RequestBoundaryNudge?.Invoke(-1);
		}
	}

	[RelayCommand]
	public void SelectPage(int targetPage)
	{
		if (targetPage >= 1 && targetPage <= TotalPages)
		{
			int arg = ((targetPage > CurrentPage) ? 1 : (-1));
			if (RequestPageTransition != null)
			{
				RequestPageTransition(targetPage, arg);
			}
			else
			{
				CommitPageChange(targetPage);
			}
		}
	}

	[RelayCommand]
	public void ToggleLaunchpad()
	{
		if (IsOpen)
		{
			HideLaunchpad();
		}
		else
		{
			OpenLaunchpad();
		}
	}

	[RelayCommand]
	public void OpenLaunchpad()
	{
		IsResidentLaunchpadDismissed = false;
		IsOpen = true;
		UpdateRunningPolling();
		UpdateWidgetPolling();
		RequestOpen?.Invoke();
		AptabaseService.Current.TrackEvent("launchpad_opened");
	}

	[RelayCommand]
	public void CloseLaunchpad()
	{
		if (!_settings.DesktopResidentLaunchpad)
		{
			CloseLaunchpadCore();
		}
	}

	public void HideLaunchpad()
	{
		IsResidentLaunchpadDismissed = _settings.DesktopResidentLaunchpad;
		CloseLaunchpadCore();
	}

	private void CloseLaunchpadCore()
	{
		_manualFeedbackVersion++;
		ManualAddFeedback = string.Empty;
		IsOpen = false;
		IsWidgetBoardOpen = false;
		UpdateRunningPolling();
		UpdateWidgetPolling();
		RequestClose?.Invoke();
		ScheduleRescanWhileHidden();
	}

	[RelayCommand]
	public void ToggleWidgetBoard()
	{
		IsWidgetBoardOpen = false;
		RequestOpenSettings?.Invoke("小组件");
	}

	[RelayCommand]
	public void OpenWidgetBoard()
	{
		IsWidgetBoardOpen = false;
		RequestOpenSettings?.Invoke("小组件");
	}

	[RelayCommand]
	public void CloseWidgetBoard()
	{
		IsWidgetBoardOpen = false;
		RequestCloseSettings?.Invoke();
	}

	[RelayCommand]
	public void OpenDateAndTimeSettings()
	{
		try
		{
			Process.Start(new ProcessStartInfo("ms-settings:dateandtime")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			App.Log("Failed to open date & time settings: " + ex.Message);
		}
	}

	[RelayCommand]
	public void OpenTaskManager()
	{
		try
		{
			Process.Start(new ProcessStartInfo("taskmgr.exe")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			App.Log("Failed to open task manager: " + ex.Message);
		}
	}

	[RelayCommand]
	public async Task ToggleWidgetOnGridAsync(string? widgetType)
	{
		if (string.IsNullOrWhiteSpace(widgetType))
		{
			return;
		}
		string type = widgetType.Trim();
		if (_allApps.Any((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase)))
		{
			foreach (AppItem item in _allApps.Where((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				_allApps.Remove(item);
			}
			ApplySorting();
			RefreshDisplayList();
			UpdateWidgetOnGridStatus();
			await _storageService.SaveApplicationsAsync(_allApps);
			string text = (type.Equals("Clock", StringComparison.OrdinalIgnoreCase) ? "时钟" : "系统监控");
			StatusMessage = "已从桌面网格移除 " + text;
			App.Log("[WIDGET] Removed widget " + type + " from grid.");
		}
		else
		{
			await AddWidgetToGridAsync(type);
		}
	}

	[RelayCommand]
	public async Task AddWidgetToGridAsync(string? widgetType)
	{
		if (string.IsNullOrWhiteSpace(widgetType))
		{
			return;
		}
		string type = widgetType.Trim();
		AppItem existing = _allApps.FirstOrDefault((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase));
		if (existing != null)
		{
			List<AppItem> list = _allApps.Where((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase) && a.Id != existing.Id).ToList();
			if (list.Count > 0)
			{
				foreach (AppItem item2 in list)
				{
					_allApps.Remove(item2);
				}
				await _storageService.SaveApplicationsAsync(_allApps);
			}
			UpdateWidgetOnGridStatus();
			int num = _filteredApps.FindIndex((AppItem a) => a.Id == existing.Id);
			if (num >= 0)
			{
				int newPage = num / PageSize + 1;
				CommitPageChange(newPage);
			}
			string value = (type.Equals("Clock", StringComparison.OrdinalIgnoreCase) ? "时钟" : "系统监控");
			StatusMessage = $"{value}已在桌面网格（第 {CurrentPage} 页）";
			return;
		}
		string name = (type.Equals("Clock", StringComparison.OrdinalIgnoreCase) ? "时钟" : "系统监控");
		int num2 = (from a in _allApps
			where !a.IsDock
			select a.SortIndex).DefaultIfEmpty(0).Min();
		AppItem item = new AppItem
		{
			Id = Guid.NewGuid().ToString(),
			Name = name,
			ExecutablePath = $"widget:{type.ToLowerInvariant()}:{Guid.NewGuid():N}",
			IsWidget = true,
			WidgetType = type,
			SortIndex = num2 - 1
		};
		_allApps.Add(item);
		ApplySorting();
		RefreshDisplayList();
		UpdateWidgetOnGridStatus();
		await _storageService.SaveApplicationsAsync(_allApps);
		App.Log("[WIDGET] Added widget " + type + " to grid.");
		CommitPageChange(1);
		StatusMessage = "已添加 " + name + " 到桌面网格首位（第 1 页）";
	}

	[RelayCommand]
	public async Task MoveWidgetToFrontAsync(string? widgetType)
	{
		if (string.IsNullOrWhiteSpace(widgetType))
		{
			return;
		}
		string type = widgetType.Trim();
		AppItem appItem = _allApps.FirstOrDefault((AppItem a) => a.IsWidget && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase));
		if (appItem != null)
		{
			int num = (from a in _allApps
				where !a.IsDock
				select a.SortIndex).DefaultIfEmpty(0).Min();
			appItem.SortIndex = num - 1;
			ApplySorting();
			RefreshDisplayList();
			await _storageService.SaveApplicationsAsync(_allApps);
			CommitPageChange(1);
			string text = (type.Equals("Clock", StringComparison.OrdinalIgnoreCase) ? "时钟" : "系统监控");
			StatusMessage = "已将 " + text + " 置顶至桌面网格首位（第 1 页）";
		}
	}

	[RelayCommand]
	public async Task RemoveWidgetAsync(AppItem? item)
	{
		if (item == null || !item.IsWidget)
		{
			return;
		}
		string type = item.WidgetType ?? string.Empty;
		foreach (AppItem item2 in _allApps.Where((AppItem a) => a.Id == item.Id || (a.IsWidget && !string.IsNullOrEmpty(type) && string.Equals(a.WidgetType, type, StringComparison.OrdinalIgnoreCase))).ToList())
		{
			_allApps.Remove(item2);
		}
		ApplySorting();
		RefreshDisplayList();
		UpdateWidgetOnGridStatus();
		await _storageService.SaveApplicationsAsync(_allApps);
		App.Log("[WIDGET] Removed widget " + item.Name + " from grid.");
	}

	private void ScheduleRescanWhileHidden()
	{
		if (DateTime.UtcNow - _lastScanUtc < TimeSpan.FromSeconds(20.0))
		{
			return;
		}
		Task.Run(async () =>
		{
			_ = 1;
			try
			{
				await Task.Delay(900);
				if (!IsOpen)
				{
					await RescanApplicationsAsync();
				}
			}
			catch
			{
			}
		});
	}

	[RelayCommand]
	public void ClearSearch()
	{
		SearchText = string.Empty;
	}

	public async Task StoreFoldersAsync(IEnumerable<string> sources)
	{
		if (_storingFolders)
		{
			ReportFileOperationFeedback("正在收纳文件夹，请稍候。");
			return;
		}
		_storingFolders = true;
		int stored = 0;
		try
		{
			string storage = ImportedFoldersDirectory;
			string[] paths = sources.Select((string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			string[] array = paths;
			for (int num = 0; num < array.Length; num++)
			{
				FolderStorageService.PlanDestination(array[num], storage);
			}
			paths = paths.Where((string p) => !paths.Any((string parent) => !string.Equals(parent, p, StringComparison.OrdinalIgnoreCase) && p.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))).ToArray();
			string[] array2 = paths;
			foreach (string source in array2)
			{
				ReportFileOperationFeedback("正在收纳 " + Path.GetFileName(source) + "…");
				AppItem[] trackedItems = AllAppItems().Concat(_manualApps).Distinct().ToArray();
				string destination = await FolderStorageService.MoveAsync(source, storage);
				AppItem[] array3 = trackedItems;
				foreach (AppItem appItem in array3)
				{
					string executablePath = appItem.ExecutablePath;
					if (string.Equals(executablePath, source, StringComparison.OrdinalIgnoreCase) || executablePath.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
					{
						string text = executablePath;
						int length = source.Length;
						appItem.ExecutablePath = destination + text.Substring(length, text.Length - length);
						if (!string.IsNullOrEmpty(appItem.IconPath) && (string.Equals(appItem.IconPath, source, StringComparison.OrdinalIgnoreCase) || appItem.IconPath.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
						{
							text = appItem.IconPath;
							length = source.Length;
							appItem.IconPath = destination + text.Substring(length, text.Length - length);
						}
						appItem.Source = "Manual";
					}
				}
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				array3 = trackedItems;
				foreach (AppItem item in array3)
				{
					if ((string.Equals(item.ExecutablePath, destination, StringComparison.OrdinalIgnoreCase) || item.ExecutablePath.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) && hashSet.Add(item.ExecutablePath))
					{
						_manualApps.RemoveAll((AppItem a) => string.Equals(a.ExecutablePath, item.ExecutablePath, StringComparison.OrdinalIgnoreCase));
						_manualApps.Add(item);
					}
				}
				await AddManualAppsAsync(new string[1] { destination });
				bool flag = await _storageService.SaveManualAppsAsync(_manualApps);
				if (flag)
				{
					flag = await _storageService.SaveApplicationsAsync(_allApps);
				}
				if (!flag)
				{
					throw new IOException("文件已移到 " + destination + "，但入口保存失败，请保留此路径并重新添加。");
				}
				stored++;
			}
			ReportFileOperationFeedback($"已收纳 {stored} 个文件夹。收纳目录：{storage}");
		}
		catch (OperationCanceledException)
		{
			ReportFileOperationFeedback($"已取消收纳，已完成 {stored} 个。请检查原目录和收纳目录：{ImportedFoldersDirectory}");
		}
		catch (Exception ex2)
		{
			ReportFileOperationFeedback($"收纳未完成（已完成 {stored} 个）：{ex2.Message}");
		}
		finally
		{
			_storingFolders = false;
		}
	}

	internal async Task SetOpenWindowsAsync(IReadOnlyList<OpenAppWindow> windows)
	{
		string signature = string.Join("|", windows.Select((OpenAppWindow w) => $"{w.Handle}:{w.ProcessId}:{w.Executable}:{w.ApplicationId}:{w.Title}"));
		if (signature == _openWindowSignature)
		{
			_openWindowVersion++;
			return;
		}
		int version = ++_openWindowVersion;
		Dictionary<string, OpenAppWindow[]> groups = windows.GroupBy((OpenAppWindow w) => w.Executable, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, OpenAppWindow> g) => g.Key, (IGrouping<string, OpenAppWindow> g) => g.ToArray(), StringComparer.OrdinalIgnoreCase);
		IEnumerable<string> enumerable = _openWindowEntries.Select((AppItem a) => a.ExecutablePath).Where(groups.ContainsKey).Concat(groups.Keys.Where((string value) => !_openWindowEntries.Any((AppItem a) => a.ExecutablePath.Equals(value, StringComparison.OrdinalIgnoreCase))));
		List<AppItem> entries = new List<AppItem>();
		foreach (string path in enumerable)
		{
			AppItem entry = _openWindowEntries.FirstOrDefault((AppItem a) => a.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase));
			if (entry == null)
			{
				AppItem appItem = AllAppItems().FirstOrDefault((AppItem a) => !a.IsFolder && !a.IsWidget && Path.IsPathFullyQualified(a.ExecutablePath) && ExistingAppWindow.MatchesExecutable(a.ExecutablePath, path));
				entry = new AppItem
				{
					Id = "open-window:" + path.ToLowerInvariant(),
					Name = (appItem?.DisplayName ?? Path.GetFileNameWithoutExtension(path)),
					ExecutablePath = path,
					IconPath = path,
					Source = "OpenWindow",
					IsOpenWindowEntry = true,
					IsRunning = true,
					Icon = appItem?.Icon,
					OpenWindowApplicationId = groups[path][0].ApplicationId
				};
				AppItem appItem2 = entry;
				if (appItem2.Icon == null)
				{
					AppItem appItem3 = appItem2;
					appItem3.Icon = await _iconService.GetIconAsync(entry);
				}
				appItem2 = entry;
				if (appItem2.Icon == null)
				{
					AppItem appItem3 = appItem2;
					appItem3.Icon = await Task.Run(() => ShellIcons.GetFileIcon(path));
				}
			}
			entries.Add(entry);
		}
		if (version == _openWindowVersion)
		{
			_openWindowSignature = signature;
			_openWindowEntries = entries;
			_openWindowTargets = groups;
			RebuildDockIfChanged();
		}
	}

	private IEnumerable<AppItem> OpenDockEntries(List<AppItem> pinned)
	{
		if (_settings.ShowOpenAppsInDock)
		{
			return _openWindowEntries.Where((AppItem open) => !pinned.Any((AppItem fixedApp) => MatchesFixedDockWindow(fixedApp, open)));
		}
		return Enumerable.Empty<AppItem>();
	}

	internal static bool MatchesFixedDockWindow(AppItem pinned, AppItem open)
	{
		string text;
		if (!pinned.ExecutablePath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			text = pinned.ExecutablePath;
		}
		else
		{
			string executablePath = pinned.ExecutablePath;
			int length = "shell:AppsFolder\\".Length;
			text = executablePath.Substring(length, executablePath.Length - length);
		}
		string text2 = text;
		if (string.IsNullOrEmpty(open.OpenWindowApplicationId) || !text2.Equals(open.OpenWindowApplicationId, StringComparison.OrdinalIgnoreCase))
		{
			return ExistingAppWindow.MatchesExecutable(pinned.ExecutablePath, open.ExecutablePath);
		}
		return true;
	}

	internal IReadOnlyList<OpenAppWindow> OpenWindowsFor(AppItem app)
	{
		if (!_openWindowTargets.TryGetValue(app.ExecutablePath, out OpenAppWindow[] value))
		{
			return Array.Empty<OpenAppWindow>();
		}
		return value;
	}

	internal void RestoreSpecificOpenWindow(OpenAppWindow window)
	{
		if (ExistingAppWindow.TryActivateTrackedWindow(window.Handle, window.ProcessId))
		{
			AppLaunchSucceeded?.Invoke();
			if (_settings.CloseAfterLaunching && !_settings.DesktopResidentLaunchpad)
			{
				CloseLaunchpad();
			}
		}
	}

	private void RestoreOpenWindow(AppItem app)
	{
		if (!_openWindowTargets.TryGetValue(app.ExecutablePath, out OpenAppWindow[] value))
		{
			return;
		}
		OpenAppWindow[] array = value;
		foreach (OpenAppWindow openAppWindow in array)
		{
			if (ExistingAppWindow.TryActivateTrackedWindow(openAppWindow.Handle, openAppWindow.ProcessId))
			{
				AppLaunchSucceeded?.Invoke();
				if (_settings.CloseAfterLaunching && !_settings.DesktopResidentLaunchpad)
				{
					CloseLaunchpad();
				}
				return;
			}
		}
		StatusMessage = "窗口已关闭或暂时无法恢复。";
	}

	private void OnColumnsChanged(int value)
	{
		if (IsOpen)
		{
			RefreshDisplayList();
		}
	}

	private void OnRowsChanged(int value)
	{
		if (IsOpen)
		{
			RefreshDisplayList();
		}
	}

	private void OnSearchTextChanged(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			_searchDebounceTimer.Stop();
			RefreshDisplayList(resetPage: true);
		}
		else
		{
			_searchDebounceTimer.Stop();
			_searchDebounceTimer.Start();
		}
	}
}
