using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
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
using Launchpad.Services.Update;
using Launchpad.Views;
using Microsoft.Win32;

namespace Launchpad.ViewModels;

public class SettingsViewModel : ObservableObject
{
	public sealed record Option<T>(T Value, string Display)
	{
		public bool Pro => Display.EndsWith(" · PRO", StringComparison.Ordinal);

		public string Label
		{
			get
			{
				if (!Pro)
				{
					return Display;
				}
				string display = Display;
				return display.Substring(0, display.Length - 6);
			}
		}
	}

	public sealed record IconPreset(string Name, IconShape Shape, bool ShowNames, double FontSize);

	public sealed record IconPackChoice(string Id, string Display, string Tag, string Description, IReadOnlyList<ImageSource> Previews);

	[ObservableProperty]
	private bool _isManagingApplications;

	[ObservableProperty]
	private string _removalSearch = string.Empty;

	private readonly IStorageService _storageService;

	private readonly IStartupService _startupService;

	private readonly IAppScannerService _scannerService;

	private readonly MainViewModel _mainViewModel;

	private readonly UpdateService _updates;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HotkeyCaptureText")]
	private AppSettings _settings;

	[ObservableProperty]
	private string _statusFeedback = string.Empty;

	private readonly DispatcherTimer _persistTimer;

	private bool _suppressLive;

	private bool _startupDirty;

	private static readonly Dictionary<string, string> SectionHints = new Dictionary<string, string>
	{
		["通用"] = "开机自启、全局快捷键、多显示器行为与检查更新",
		["程序坞"] = "桌面 Dock、触发角与图标外观",
		["外观与图标"] = "主题配色、背景壁纸、图标包、形状与文字样式",
		["布局与网格"] = "每页行列数量、网格密度与屏幕边缘留白",
		["小组件"] = "时钟与性能监控；修改自动生效",
		["动效与手势"] = "翻页动画速度、物理弹性曲线、翻页判定与触发角",
		["应用管理"] = "应用重扫描、Steam 游戏发现、自建快捷方式与隐藏应用",
		["账户"] = "账户信息、许可证与设备授权"
	};

	[ObservableProperty]
	private string _activeSection = "通用";

	[ObservableProperty]
	[NotifyPropertyChangedFor("HotkeyCaptureText")]
	private bool _isCapturingHotkey;

	private IconPreset? _selectedPreset;

	private bool _applyingPreset;

	private static IReadOnlyList<ImageSource>? _systemPreviewsCache;

	private static readonly Dictionary<string, IReadOnlyList<ImageSource>> _packPreviewsCache = new Dictionary<string, IReadOnlyList<ImageSource>>(StringComparer.OrdinalIgnoreCase);

	[ObservableProperty]
	private string _licenseStatusText = "未激活";

	[ObservableProperty]
	private bool _isPro;

	[ObservableProperty]
	private string _licenseDetailText = string.Empty;

	[ObservableProperty]
	private string _deviceText = string.Empty;

	[ObservableProperty]
	private string _boundCodeText = string.Empty;

	[ObservableProperty]
	private bool _hasBoundCode;

	[ObservableProperty]
	private string _revertedText = string.Empty;

	[ObservableProperty]
	private bool _hasReverted;

	[ObservableProperty]
	private string _activationCode = string.Empty;

	[ObservableProperty]
	private string _inviteInput = string.Empty;

	[ObservableProperty]
	private string _inviteCodeText = "—";

	[ObservableProperty]
	private string _inviteSummaryText = string.Empty;

	[ObservableProperty]
	private string _versionText = string.Empty;

	[ObservableProperty]
	private string _updateSummaryText = string.Empty;

	[ObservableProperty]
	private string _proPriceDisplay = "查看价格";

	private int _copyInProgress;

	[ObservableProperty]
	private bool _isBackupHistoryExpanded;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("SwitchLayoutCommand", new string[] { "DeleteLayoutCommand", "UpdateLayoutCommand" })]
	private WorkspaceEntry? _selectedLayout;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor("RestoreSnapshotCommand")]
	private WorkspaceEntry? _selectedBackup;

	[ObservableProperty]
	private string _layoutName = "工作";

	[ObservableProperty]
	private string _snapshotName = "我的桌面";

	[ObservableProperty]
	private string _workspaceFeedback = string.Empty;

	[ObservableProperty]
	private bool _workspaceBusy;

	[ObservableProperty]
	private bool _isAddingLayout;

	private string? _renamingLayoutId;

	private RelayCommand? clearRemovalSearchCommand;

	private RelayCommand? beginRemoveApplicationsCommand;

	private RelayCommand? cancelRemoveApplicationsCommand;

	private RelayCommand? selectVisibleApplicationsCommand;

	private AsyncRelayCommand? removeSelectedApplicationsCommand;

	private RelayCommand<string?>? applySolidColorPresetCommand;

	private RelayCommand? chooseSolidBackgroundColorCommand;

	private RelayCommand? chooseBackdropImageCommand;

	private RelayCommand? clearBackdropImageCommand;

	private RelayCommand? copyFeedbackEmailCommand;

	private AsyncRelayCommand? checkUpdatesCommand;

	private AsyncRelayCommand<AppItem?>? restoreHiddenAppCommand;

	private AsyncRelayCommand? restoreAllHiddenCommand;

	private AsyncRelayCommand? rescanApplicationsCommand;

	private AsyncRelayCommand? addApplicationCommand;

	private AsyncRelayCommand? resetDatabaseCommand;

	private AsyncRelayCommand? addClockWidgetCommand;

	private AsyncRelayCommand? addSystemMonitorWidgetCommand;

	private AsyncRelayCommand<string?>? moveWidgetToFrontCommand;

	private RelayCommand? openDateAndTimeSettingsCommand;

	private RelayCommand? openTaskManagerCommand;

	private RelayCommand? openWidgetBoardCommand;

	private AsyncRelayCommand? resetSettingsCommand;

	private AsyncRelayCommand<ThemeSuite?>? selectPresetWallpaperCommand;

	private RelayCommand? toggleBackupHistoryExpandedCommand;

	private RelayCommand? beginAddLayoutCommand;

	private RelayCommand? cancelAddLayoutCommand;

	private RelayCommand<WorkspaceEntry?>? renameLayoutEntryCommand;

	private AsyncRelayCommand? saveLayoutCommand;

	private AsyncRelayCommand<WorkspaceEntry?>? useLayoutEntryCommand;

	private AsyncRelayCommand<WorkspaceEntry?>? updateLayoutEntryCommand;

	private AsyncRelayCommand<WorkspaceEntry?>? deleteLayoutEntryCommand;

	private AsyncRelayCommand? switchLayoutCommand;

	private AsyncRelayCommand? updateLayoutCommand;

	private AsyncRelayCommand? deleteLayoutCommand;

	private AsyncRelayCommand? saveSnapshotCommand;

	private AsyncRelayCommand? restoreSnapshotCommand;

	private AsyncRelayCommand<WorkspaceEntry?>? restoreHistoryEntryCommand;

	private AsyncRelayCommand? exportBasicSettingsCommand;

	private AsyncRelayCommand? exportCompleteBackupCommand;

	private AsyncRelayCommand? importWorkspaceCommand;

	private AsyncRelayCommand<ThemeSuite?>? applyThemeSuiteCommand;

	public ObservableCollection<AppRemovalChoice> RemovalChoices { get; } = new ObservableCollection<AppRemovalChoice>();

	public ObservableCollection<AppRemovalChoice> VisibleRemovalChoices { get; } = new ObservableCollection<AppRemovalChoice>();

	public string RemovalSelectionText => $"已选 {RemovalChoices.Count((AppRemovalChoice x) => x.IsSelected)} 个";

	public MainViewModel IconPresentation => _mainViewModel;

	public int[] ColumnOptions { get; } = new int[6] { 5, 6, 7, 8, 9, 10 };

	public int[] RowOptions { get; } = new int[5] { 2, 3, 4, 5, 6 };

	public string[] Sections { get; } = new string[8] { "通用", "程序坞", "外观与图标", "布局与网格", "小组件", "动效与手势", "应用管理", "账户" };

	public bool ShowAdvancedSettings => true;

	public Option<string>[] SimpleClockOptions { get; } = new Option<string>[3]
	{
		new Option<string>("Off", "关闭"),
		new Option<string>("Compact", "顶部小字"),
		new Option<string>("Large", "居中大字")
	};

	public bool IsClockColorEditable
	{
		get
		{
			if (Settings.ClockLargeMode)
			{
				return Settings.ClockLargeStyle != LargeClockStyle.Mac;
			}
			return true;
		}
	}

	public string SimpleClockMode
	{
		get
		{
			if (Settings.ShowTopClock)
			{
				if (!Settings.ClockLargeMode)
				{
					return "Compact";
				}
				return "Large";
			}
			return "Off";
		}
		set
		{
			Settings.ShowTopClock = value != "Off";
			Settings.ClockLargeMode = value == "Large";
			if (value != "Off")
			{
				Settings.ShowTopWidgets = true;
			}
			OnPropertyChanged("SimpleClockMode");
		}
	}

	public Option<double>[] SimpleClockSizes { get; } = new Option<double>[3]
	{
		new Option<double>(80.0, "小"),
		new Option<double>(112.0, "中"),
		new Option<double>(152.0, "大")
	};

	public double SimpleClockSize
	{
		get
		{
			return (Settings.ClockLargeFontSize >= 132.0) ? 152 : ((Settings.ClockLargeFontSize >= 96.0) ? 112 : 80);
		}
		set
		{
			Settings.ClockLargeFontSize = value;
			OnPropertyChanged("SimpleClockSize");
		}
	}

	public Option<int>[] SimpleGridOptions { get; } = new Option<int>[3]
	{
		new Option<int>(20, "宽松"),
		new Option<int>(24, "标准"),
		new Option<int>(30, "紧凑")
	};

	public int SimpleGridLayout
	{
		get
		{
			int columns = Settings.Columns;
			int rows = Settings.Rows;
			switch (columns)
			{
			case 5:
				if (rows != 4)
				{
					break;
				}
				return 20;
			case 6:
				switch (rows)
				{
				case 4:
					return 24;
				case 5:
					return 30;
				}
				break;
			}
			return 0;
		}
		set
		{
			if ((value == 20 || value == 24 || value == 30) ? true : false)
			{
				Settings.Columns = ((value == 20) ? 5 : 6);
				Settings.Rows = ((value == 30) ? 5 : 4);
				OnPropertyChanged("SimpleGridLayout");
			}
		}
	}

	public string SimpleGridDescription => $"当前 {Settings.Columns} 列 × {Settings.Rows} 行；选择预设可直接切换";

	public string ActiveSectionHint
	{
		get
		{
			if (!ShowAdvancedSettings)
			{
				return ActiveSection switch
				{
					"通用" => "启动、快捷键与常用操作", 
					"外观" => "选择喜欢的主题、背景和排列", 
					"时钟" => "选一种显示方式，调整大小即可", 
					"应用" => "添加应用或更新应用列表", 
					"账户" => "专业版与账户信息", 
					_ => string.Empty, 
				};
			}
			return SectionHints.GetValueOrDefault(ActiveSection, string.Empty);
		}
	}

	public string HotkeyCaptureText
	{
		get
		{
			if (!IsCapturingHotkey)
			{
				return Settings.HotkeyDisplay;
			}
			return "按下新快捷键…";
		}
	}

	public Option<MotionTempo>[] MotionTempoOptions { get; } = new Option<MotionTempo>[4]
	{
		new Option<MotionTempo>(MotionTempo.Instant, "立即"),
		new Option<MotionTempo>(MotionTempo.Fast, "快速"),
		new Option<MotionTempo>(MotionTempo.Normal, "标准"),
		new Option<MotionTempo>(MotionTempo.Relaxed, "舒缓")
	};

	public Option<MotionCurve>[] MotionCurveOptions { get; } = new Option<MotionCurve>[3]
	{
		new Option<MotionCurve>(MotionCurve.AppleSpring, "自然弹性"),
		new Option<MotionCurve>(MotionCurve.EaseOut, "缓出"),
		new Option<MotionCurve>(MotionCurve.Linear, "匀速")
	};

	public Option<HotCornerPosition>[] HotCornerOptions { get; } = new Option<HotCornerPosition>[4]
	{
		new Option<HotCornerPosition>(HotCornerPosition.BottomLeft, "左下"),
		new Option<HotCornerPosition>(HotCornerPosition.BottomRight, "右下"),
		new Option<HotCornerPosition>(HotCornerPosition.TopLeft, "左上"),
		new Option<HotCornerPosition>(HotCornerPosition.TopRight, "右上")
	};

	public Option<HotCornerPosition>[] LaunchpadHotCornerOptions => HotCornerOptions.Where((Option<HotCornerPosition> o) => o.Value != Settings.DesktopDockHotCorner).ToArray();

	public Option<HotCornerPosition>[] DesktopDockHotCornerOptions => HotCornerOptions.Where((Option<HotCornerPosition> o) => o.Value != Settings.HotCorner && (!Settings.EnableResidentPinHotCorner || !Settings.DesktopResidentLaunchpad || o.Value != Settings.ResidentPinHotCorner)).ToArray();

	public Option<HotCornerPosition>[] ResidentPinHotCornerOptions => HotCornerOptions.Where((Option<HotCornerPosition> o) => o.Value != Settings.DesktopDockHotCorner).ToArray();

	public Option<BackdropKind>[] BackdropOptions { get; } = new Option<BackdropKind>[4]
	{
		new Option<BackdropKind>(BackdropKind.Frosted, "毛玻璃"),
		new Option<BackdropKind>(BackdropKind.Clear, "清晰"),
		new Option<BackdropKind>(BackdropKind.Solid, "纯色"),
		new Option<BackdropKind>(BackdropKind.None, "无")
	};

	public Option<BackdropSource>[] BackdropSourceOptions => new Option<BackdropSource>[2]
	{
		new Option<BackdropSource>(BackdropSource.Wallpaper, "桌面壁纸"),
		new Option<BackdropSource>(BackdropSource.CustomImage, IsPresetWallpaper ? "官方预设" : "自定义背景")
	};

	public Option<LabelInk>[] LabelInkOptions { get; } = new Option<LabelInk>[2]
	{
		new Option<LabelInk>(LabelInk.White, "白字"),
		new Option<LabelInk>(LabelInk.Black, "黑字")
	};

	public Action<bool>? SetDeactivationBlock { get; set; }

	public Option<string>[] SolidColorPresets { get; } = new Option<string>[8]
	{
		new Option<string>("#000000", "黑色"),
		new Option<string>("#FFFFFF", "白色"),
		new Option<string>("#252C37", "石墨灰"),
		new Option<string>("#18243B", "午夜蓝"),
		new Option<string>("#24443C", "松柏绿"),
		new Option<string>("#42364F", "烟紫"),
		new Option<string>("#E8DFD0", "暖米色"),
		new Option<string>("#DCE6E8", "雾蓝")
	};

	public bool HasCustomBackdrop
	{
		get
		{
			if (!string.IsNullOrEmpty(Settings?.CustomBackdropPath))
			{
				return File.Exists(Settings?.CustomBackdropPath);
			}
			return false;
		}
	}

	public string CustomBackdropName
	{
		get
		{
			if (!string.IsNullOrEmpty(Settings?.CustomBackdropPath) && File.Exists(Settings?.CustomBackdropPath))
			{
				return Path.GetFileName(Settings.CustomBackdropPath);
			}
			return "未设置";
		}
	}

	public Option<AppTheme>[] ThemeOptions { get; } = new Option<AppTheme>[4]
	{
		new Option<AppTheme>(AppTheme.System, "自动"),
		new Option<AppTheme>(AppTheme.Dark, "深色"),
		new Option<AppTheme>(AppTheme.Light, "浅色"),
		new Option<AppTheme>(AppTheme.HighContrast, "高对比")
	};

	public Option<IconShape>[] IconShapeOptions { get; } = new Option<IconShape>[3]
	{
		new Option<IconShape>(IconShape.Original, "原样"),
		new Option<IconShape>(IconShape.Rounded, "圆角"),
		new Option<IconShape>(IconShape.Circle, "圆形")
	};

	public IconPreset[] IconPresetOptions { get; } = new IconPreset[5]
	{
		new IconPreset("标准", IconShape.Original, ShowNames: true, 12.0),
		new IconPreset("紧凑", IconShape.Original, ShowNames: true, 11.0),
		new IconPreset("圆角", IconShape.Rounded, ShowNames: true, 12.0),
		new IconPreset("圆形", IconShape.Circle, ShowNames: true, 12.0),
		new IconPreset("纯图标", IconShape.Rounded, ShowNames: false, 12.0)
	};

	public IconPreset? SelectedIconPreset
	{
		get
		{
			return _selectedPreset;
		}
		set
		{
			if (!_applyingPreset && (object)value != _selectedPreset)
			{
				_selectedPreset = value;
				OnPropertyChanged("SelectedIconPreset");
				if (!(value == null))
				{
					_applyingPreset = true;
					Settings.IconShape = value.Shape;
					Settings.ShowAppNames = value.ShowNames;
					Settings.AppNameFontSize = value.FontSize;
					_applyingPreset = false;
				}
			}
		}
	}

	public IconPackChoice[] IconPackOptions { get; private set; } = Array.Empty<IconPackChoice>();

	public string SelectedIconPackId
	{
		get
		{
			return Settings?.IconPack ?? string.Empty;
		}
		set
		{
			if (value != null && Settings != null && Settings.IconPack != value)
			{
				Settings.IconPack = value;
				OnPropertyChanged("SelectedIconPackId");
			}
		}
	}

	public ObservableCollection<AppItem> HiddenApps => _mainViewModel.HiddenApps;

	public int ManualAppCount => _mainViewModel.ManualAppCount;

	public MainViewModel MainViewModel => _mainViewModel;

	public Option<SearchDisplayMode>[] SearchDisplayModeOptions { get; } = new Option<SearchDisplayMode>[3]
	{
		new Option<SearchDisplayMode>(SearchDisplayMode.SlideOnType, "击键灵动滑出 (推荐)"),
		new Option<SearchDisplayMode>(SearchDisplayMode.AlwaysShow, "常驻顶栏"),
		new Option<SearchDisplayMode>(SearchDisplayMode.AlwaysHidden, "常驻隐藏")
	};

	public Option<string>[] SearchPositionOptions { get; } = new Option<string>[3]
	{
		new Option<string>("Center", "居中 (默认)"),
		new Option<string>("Left", "左上角"),
		new Option<string>("Right", "右上角")
	};

	public Option<string>[] WidgetPositionOptions { get; } = new Option<string>[3]
	{
		new Option<string>("Right", "右上角 (默认)"),
		new Option<string>("Center", "居中"),
		new Option<string>("Left", "左上角")
	};

	public Option<LargeClockStyle>[] LargeClockStyleOptions { get; } = new Option<LargeClockStyle>[6]
	{
		new Option<LargeClockStyle>(LargeClockStyle.Minimal, "留白数字"),
		new Option<LargeClockStyle>(LargeClockStyle.Flip, "机械翻页 · PRO"),
		new Option<LargeClockStyle>(LargeClockStyle.Glass, "雾面玻璃 · PRO"),
		new Option<LargeClockStyle>(LargeClockStyle.Paper, "奶油纸卡 · PRO"),
		new Option<LargeClockStyle>(LargeClockStyle.Digital, "墨色数码 · PRO"),
		new Option<LargeClockStyle>(LargeClockStyle.Mac, "磨砂晶透 · PRO")
	};

	public Option<ClockStyle>[] ClockStyleOptions { get; } = new Option<ClockStyle>[5]
	{
		new Option<ClockStyle>(ClockStyle.Classic, "经典卡片"),
		new Option<ClockStyle>(ClockStyle.Minimal, "留白数字"),
		new Option<ClockStyle>(ClockStyle.Digital, "墨色数码 · PRO"),
		new Option<ClockStyle>(ClockStyle.Elegant, "奶油衬线 · PRO"),
		new Option<ClockStyle>(ClockStyle.Vibrant, "暮色渐变 · PRO")
	};

	public Option<string>[] ClockColorOptions { get; } = new Option<string>[6]
	{
		new Option<string>("", "样式默认"),
		new Option<string>("#F7F7F8", "白色"),
		new Option<string>("#20242C", "墨黑"),
		new Option<string>("#A3D2BD", "薄荷"),
		new Option<string>("#FFD59E", "暖金"),
		new Option<string>("#83B7CC", "冰蓝")
	};

	public Option<string>[] ClockFontWeightOptions { get; } = new Option<string>[4]
	{
		new Option<string>("Light", "纤细"),
		new Option<string>("Normal", "常规"),
		new Option<string>("SemiBold", "中粗"),
		new Option<string>("Bold", "粗体")
	};

	public Option<string>[] MusicWidgetDisplayChoices { get; } = new Option<string>[3]
	{
		new Option<string>("Off", "关闭"),
		new Option<string>("Always", "始终显示"),
		new Option<string>("WhenAvailable", "有歌曲时显示")
	};

	public ThemeSuite[] ThemeSuites { get; } = new ThemeSuite[6]
	{
		new ThemeSuite("custom", "默认", "桌面壁纸 · 原始图标 · 自由配置", "#546A81", "#252C37", LargeClockStyle.Minimal, AppTheme.System, Pro: false),
		new ThemeSuite("default", "青柠折光", "Reflection 3 · 原始图标 · 简洁时钟", "#546A81", "#252C37", LargeClockStyle.Minimal, AppTheme.Dark, Pro: true),
		new ThemeSuite("midnight", "幻彩镜面", "Reflection 1 · 翻页时钟 · 圆角图标", "#152337", "#070A10", LargeClockStyle.Flip, AppTheme.Dark, Pro: true),
		new ThemeSuite("aurora", "紫色折光", "Reflection 4 · 玻璃时钟 · 通透程序坞", "#164D62", "#442B66", LargeClockStyle.Glass, AppTheme.Dark, Pro: true),
		new ThemeSuite("cream", "蜜桃镜面", "Reflection 2 · 纸卡时钟 · 柔和图标", "#E8DCC9", "#AA8C78", LargeClockStyle.Paper, AppTheme.Light, Pro: true),
		new ThemeSuite("ink", "羚羊峡谷", "Antelope Canyon · 数码时钟 · 紧凑程序坞", "#353A42", "#10141B", LargeClockStyle.Digital, AppTheme.Dark, Pro: true)
	};

	public Option<string>[] BackgroundSourceChoices { get; } = new Option<string>[4]
	{
		new Option<string>("Desktop", "桌面壁纸"),
		new Option<string>("Official", "官方预设"),
		new Option<string>("Custom", "自定义背景"),
		new Option<string>("Solid", "纯色")
	};

	public string SelectedBackgroundSource
	{
		get
		{
			if (Settings.Backdrop != BackdropKind.Solid)
			{
				if (Settings.BackdropSource == BackdropSource.CustomImage)
				{
					if (!IsPresetWallpaper)
					{
						return "Custom";
					}
					return "Official";
				}
				return "Desktop";
			}
			return "Solid";
		}
		set
		{
			bool flag = value == SelectedBackgroundSource;
			if (!flag)
			{
				bool flag2;
				switch (value)
				{
				case "Desktop":
				case "Official":
				case "Custom":
				case "Solid":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = !flag2;
			}
			if (flag)
			{
				return;
			}
			if (value == "Official" && !RequireWorkspacePro())
			{
				OnPropertyChanged("SelectedBackgroundSource");
				return;
			}
			if (value == "Solid")
			{
				Settings.Backdrop = BackdropKind.Solid;
				NotifyCustomBackdropChanged();
				return;
			}
			if (Settings.Backdrop == BackdropKind.Solid)
			{
				Settings.Backdrop = BackdropKind.Frosted;
			}
			if (value == "Official")
			{
				SelectPresetWallpaperCommand.Execute(WallpaperPresets[0]);
				return;
			}
			if (value == "Custom" && IsPresetWallpaper)
			{
				Settings.CustomBackdropPath = string.Empty;
			}
			Settings.BackdropSource = ((!(value == "Desktop")) ? BackdropSource.CustomImage : BackdropSource.Wallpaper);
			NotifyCustomBackdropChanged();
		}
	}

	public ThemeSuite[] WallpaperPresets => ThemeSuites.Where((ThemeSuite s) => s.Pro).ToArray();

	public bool IsPresetWallpaper
	{
		get
		{
			if (!string.IsNullOrEmpty(Settings.CustomBackdropPath) && string.Equals(Path.GetDirectoryName(Path.GetFullPath(Settings.CustomBackdropPath)), Path.GetFullPath(Path.Combine(_storageService.StorageDirectory, "theme-backgrounds")), StringComparison.OrdinalIgnoreCase))
			{
				return WallpaperPresets.Any((ThemeSuite s) => string.Equals(s.WallpaperFile, Path.GetFileName(Settings.CustomBackdropPath), StringComparison.OrdinalIgnoreCase));
			}
			return false;
		}
	}

	public ObservableCollection<WorkspaceEntry> LayoutPresets { get; } = new ObservableCollection<WorkspaceEntry>();

	public ObservableCollection<WorkspaceEntry> BackupHistory { get; } = new ObservableCollection<WorkspaceEntry>();

	public ObservableCollection<WorkspaceEntry> VisibleBackupHistory { get; } = new ObservableCollection<WorkspaceEntry>();

	public bool HasMoreBackupHistory => BackupHistory.Count > 3;

	public int RemainingBackupHistoryCount => Math.Max(0, BackupHistory.Count - 3);

	public string BackupHistoryExpandText
	{
		get
		{
			if (!IsBackupHistoryExpanded)
			{
				return $"查看更早的快照（展开剩余 {RemainingBackupHistoryCount} 条） ⌵";
			}
			return "收起更早的快照 ︿";
		}
	}

	private WorkspaceLibrary Library => new WorkspaceLibrary(_storageService.StorageDirectory);

	public string CurrentSceneText
	{
		get
		{
			string text = LayoutPresets.FirstOrDefault((WorkspaceEntry e) => e.Id == Settings.ActiveLayoutId)?.Name;
			if (text == null)
			{
				return "当前：默认桌面";
			}
			return "当前：" + text;
		}
	}

	public string SceneEditorTitle
	{
		get
		{
			if (_renamingLayoutId != null)
			{
				return "修改名称";
			}
			return "新增场景";
		}
	}

	public bool IsManagingApplications
	{
		get
		{
			return _isManagingApplications;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isManagingApplications, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsManagingApplications);
				_isManagingApplications = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsManagingApplications);
			}
		}
	}

	public string RemovalSearch
	{
		get
		{
			return _removalSearch;
		}
		[MemberNotNull("_removalSearch")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_removalSearch, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RemovalSearch);
				_removalSearch = value;
				OnRemovalSearchChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RemovalSearch);
			}
		}
	}

	public AppSettings Settings
	{
		get
		{
			return _settings;
		}
		[MemberNotNull("_settings")]
		set
		{
			if (!EqualityComparer<AppSettings>.Default.Equals(_settings, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Settings);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotkeyCaptureText);
				_settings = value;
				OnSettingsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Settings);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotkeyCaptureText);
			}
		}
	}

	public string StatusFeedback
	{
		get
		{
			return _statusFeedback;
		}
		[MemberNotNull("_statusFeedback")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_statusFeedback, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StatusFeedback);
				_statusFeedback = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StatusFeedback);
			}
		}
	}

	public string ActiveSection
	{
		get
		{
			return _activeSection;
		}
		[MemberNotNull("_activeSection")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_activeSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ActiveSection);
				_activeSection = value;
				OnActiveSectionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ActiveSection);
			}
		}
	}

	public bool IsCapturingHotkey
	{
		get
		{
			return _isCapturingHotkey;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isCapturingHotkey, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsCapturingHotkey);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HotkeyCaptureText);
				_isCapturingHotkey = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsCapturingHotkey);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HotkeyCaptureText);
			}
		}
	}

	public string LicenseStatusText
	{
		get
		{
			return _licenseStatusText;
		}
		[MemberNotNull("_licenseStatusText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_licenseStatusText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LicenseStatusText);
				_licenseStatusText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LicenseStatusText);
			}
		}
	}

	public bool IsPro
	{
		get
		{
			return _isPro;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isPro, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsPro);
				_isPro = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsPro);
			}
		}
	}

	public string LicenseDetailText
	{
		get
		{
			return _licenseDetailText;
		}
		[MemberNotNull("_licenseDetailText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_licenseDetailText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LicenseDetailText);
				_licenseDetailText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LicenseDetailText);
			}
		}
	}

	public string DeviceText
	{
		get
		{
			return _deviceText;
		}
		[MemberNotNull("_deviceText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_deviceText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DeviceText);
				_deviceText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DeviceText);
			}
		}
	}

	public string BoundCodeText
	{
		get
		{
			return _boundCodeText;
		}
		[MemberNotNull("_boundCodeText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_boundCodeText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BoundCodeText);
				_boundCodeText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BoundCodeText);
			}
		}
	}

	public bool HasBoundCode
	{
		get
		{
			return _hasBoundCode;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasBoundCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasBoundCode);
				_hasBoundCode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasBoundCode);
			}
		}
	}

	public string RevertedText
	{
		get
		{
			return _revertedText;
		}
		[MemberNotNull("_revertedText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_revertedText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RevertedText);
				_revertedText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RevertedText);
			}
		}
	}

	public bool HasReverted
	{
		get
		{
			return _hasReverted;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasReverted, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasReverted);
				_hasReverted = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasReverted);
			}
		}
	}

	public string ActivationCode
	{
		get
		{
			return _activationCode;
		}
		[MemberNotNull("_activationCode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_activationCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ActivationCode);
				_activationCode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ActivationCode);
			}
		}
	}

	public string InviteInput
	{
		get
		{
			return _inviteInput;
		}
		[MemberNotNull("_inviteInput")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_inviteInput, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InviteInput);
				_inviteInput = value;
				OnInviteInputChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InviteInput);
			}
		}
	}

	public string InviteCodeText
	{
		get
		{
			return _inviteCodeText;
		}
		[MemberNotNull("_inviteCodeText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_inviteCodeText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InviteCodeText);
				_inviteCodeText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InviteCodeText);
			}
		}
	}

	public string InviteSummaryText
	{
		get
		{
			return _inviteSummaryText;
		}
		[MemberNotNull("_inviteSummaryText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_inviteSummaryText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InviteSummaryText);
				_inviteSummaryText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InviteSummaryText);
			}
		}
	}

	public string VersionText
	{
		get
		{
			return _versionText;
		}
		[MemberNotNull("_versionText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_versionText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VersionText);
				_versionText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VersionText);
			}
		}
	}

	public string UpdateSummaryText
	{
		get
		{
			return _updateSummaryText;
		}
		[MemberNotNull("_updateSummaryText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_updateSummaryText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UpdateSummaryText);
				_updateSummaryText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UpdateSummaryText);
			}
		}
	}

	public string ProPriceDisplay
	{
		get
		{
			return _proPriceDisplay;
		}
		[MemberNotNull("_proPriceDisplay")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_proPriceDisplay, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ProPriceDisplay);
				_proPriceDisplay = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ProPriceDisplay);
			}
		}
	}

	public bool IsBackupHistoryExpanded
	{
		get
		{
			return _isBackupHistoryExpanded;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isBackupHistoryExpanded, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsBackupHistoryExpanded);
				_isBackupHistoryExpanded = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsBackupHistoryExpanded);
			}
		}
	}

	public WorkspaceEntry? SelectedLayout
	{
		get
		{
			return _selectedLayout;
		}
		set
		{
			if (!EqualityComparer<WorkspaceEntry>.Default.Equals(_selectedLayout, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLayout);
				_selectedLayout = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLayout);
				SwitchLayoutCommand.NotifyCanExecuteChanged();
				DeleteLayoutCommand.NotifyCanExecuteChanged();
				UpdateLayoutCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public WorkspaceEntry? SelectedBackup
	{
		get
		{
			return _selectedBackup;
		}
		set
		{
			if (!EqualityComparer<WorkspaceEntry>.Default.Equals(_selectedBackup, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedBackup);
				_selectedBackup = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedBackup);
				RestoreSnapshotCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public string LayoutName
	{
		get
		{
			return _layoutName;
		}
		[MemberNotNull("_layoutName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_layoutName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LayoutName);
				_layoutName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LayoutName);
			}
		}
	}

	public string SnapshotName
	{
		get
		{
			return _snapshotName;
		}
		[MemberNotNull("_snapshotName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_snapshotName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SnapshotName);
				_snapshotName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SnapshotName);
			}
		}
	}

	public string WorkspaceFeedback
	{
		get
		{
			return _workspaceFeedback;
		}
		[MemberNotNull("_workspaceFeedback")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_workspaceFeedback, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.WorkspaceFeedback);
				_workspaceFeedback = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.WorkspaceFeedback);
			}
		}
	}

	public bool WorkspaceBusy
	{
		get
		{
			return _workspaceBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_workspaceBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.WorkspaceBusy);
				_workspaceBusy = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.WorkspaceBusy);
			}
		}
	}

	public bool IsAddingLayout
	{
		get
		{
			return _isAddingLayout;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isAddingLayout, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsAddingLayout);
				_isAddingLayout = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsAddingLayout);
			}
		}
	}

	public IRelayCommand ClearRemovalSearchCommand => clearRemovalSearchCommand ?? (clearRemovalSearchCommand = new RelayCommand(ClearRemovalSearch));

	public IRelayCommand BeginRemoveApplicationsCommand => beginRemoveApplicationsCommand ?? (beginRemoveApplicationsCommand = new RelayCommand(BeginRemoveApplications));

	public IRelayCommand CancelRemoveApplicationsCommand => cancelRemoveApplicationsCommand ?? (cancelRemoveApplicationsCommand = new RelayCommand(CancelRemoveApplications));

	public IRelayCommand SelectVisibleApplicationsCommand => selectVisibleApplicationsCommand ?? (selectVisibleApplicationsCommand = new RelayCommand(SelectVisibleApplications));

	public IAsyncRelayCommand RemoveSelectedApplicationsCommand => removeSelectedApplicationsCommand ?? (removeSelectedApplicationsCommand = new AsyncRelayCommand(RemoveSelectedApplicationsAsync));

	public IRelayCommand<string?> ApplySolidColorPresetCommand => applySolidColorPresetCommand ?? (applySolidColorPresetCommand = new RelayCommand<string>(ApplySolidColorPreset));

	public IRelayCommand ChooseSolidBackgroundColorCommand => chooseSolidBackgroundColorCommand ?? (chooseSolidBackgroundColorCommand = new RelayCommand(ChooseSolidBackgroundColor));

	public IRelayCommand ChooseBackdropImageCommand => chooseBackdropImageCommand ?? (chooseBackdropImageCommand = new RelayCommand(ChooseBackdropImage));

	public IRelayCommand ClearBackdropImageCommand => clearBackdropImageCommand ?? (clearBackdropImageCommand = new RelayCommand(ClearBackdropImage));

	public IRelayCommand CopyFeedbackEmailCommand => copyFeedbackEmailCommand ?? (copyFeedbackEmailCommand = new RelayCommand(CopyFeedbackEmail));

	public IAsyncRelayCommand CheckUpdatesCommand => checkUpdatesCommand ?? (checkUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync));

	public IAsyncRelayCommand<AppItem?> RestoreHiddenAppCommand => restoreHiddenAppCommand ?? (restoreHiddenAppCommand = new AsyncRelayCommand<AppItem>(RestoreHiddenApp));

	public IAsyncRelayCommand RestoreAllHiddenCommand => restoreAllHiddenCommand ?? (restoreAllHiddenCommand = new AsyncRelayCommand(RestoreAllHidden));

	public IAsyncRelayCommand RescanApplicationsCommand => rescanApplicationsCommand ?? (rescanApplicationsCommand = new AsyncRelayCommand(RescanApplicationsAsync));

	public IAsyncRelayCommand AddApplicationCommand => addApplicationCommand ?? (addApplicationCommand = new AsyncRelayCommand(AddApplicationAsync));

	public IAsyncRelayCommand ResetDatabaseCommand => resetDatabaseCommand ?? (resetDatabaseCommand = new AsyncRelayCommand(ResetDatabaseAsync));

	public IAsyncRelayCommand AddClockWidgetCommand => addClockWidgetCommand ?? (addClockWidgetCommand = new AsyncRelayCommand(AddClockWidgetAsync));

	public IAsyncRelayCommand AddSystemMonitorWidgetCommand => addSystemMonitorWidgetCommand ?? (addSystemMonitorWidgetCommand = new AsyncRelayCommand(AddSystemMonitorWidgetAsync));

	public IAsyncRelayCommand<string?> MoveWidgetToFrontCommand => moveWidgetToFrontCommand ?? (moveWidgetToFrontCommand = new AsyncRelayCommand<string>(MoveWidgetToFrontAsync));

	public IRelayCommand OpenDateAndTimeSettingsCommand => openDateAndTimeSettingsCommand ?? (openDateAndTimeSettingsCommand = new RelayCommand(OpenDateAndTimeSettings));

	public IRelayCommand OpenTaskManagerCommand => openTaskManagerCommand ?? (openTaskManagerCommand = new RelayCommand(OpenTaskManager));

	public IRelayCommand OpenWidgetBoardCommand => openWidgetBoardCommand ?? (openWidgetBoardCommand = new RelayCommand(OpenWidgetBoard));

	public IAsyncRelayCommand ResetSettingsCommand => resetSettingsCommand ?? (resetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync));

	public IAsyncRelayCommand<ThemeSuite?> SelectPresetWallpaperCommand => selectPresetWallpaperCommand ?? (selectPresetWallpaperCommand = new AsyncRelayCommand<ThemeSuite>(SelectPresetWallpaperAsync));

	public IRelayCommand ToggleBackupHistoryExpandedCommand => toggleBackupHistoryExpandedCommand ?? (toggleBackupHistoryExpandedCommand = new RelayCommand(ToggleBackupHistoryExpanded));

	public IRelayCommand BeginAddLayoutCommand => beginAddLayoutCommand ?? (beginAddLayoutCommand = new RelayCommand(BeginAddLayout));

	public IRelayCommand CancelAddLayoutCommand => cancelAddLayoutCommand ?? (cancelAddLayoutCommand = new RelayCommand(CancelAddLayout));

	public IRelayCommand<WorkspaceEntry?> RenameLayoutEntryCommand => renameLayoutEntryCommand ?? (renameLayoutEntryCommand = new RelayCommand<WorkspaceEntry>(RenameLayoutEntry));

	public IAsyncRelayCommand SaveLayoutCommand => saveLayoutCommand ?? (saveLayoutCommand = new AsyncRelayCommand(SaveLayoutAsync));

	public IAsyncRelayCommand<WorkspaceEntry?> UseLayoutEntryCommand => useLayoutEntryCommand ?? (useLayoutEntryCommand = new AsyncRelayCommand<WorkspaceEntry>(UseLayoutEntryAsync));

	public IAsyncRelayCommand<WorkspaceEntry?> UpdateLayoutEntryCommand => updateLayoutEntryCommand ?? (updateLayoutEntryCommand = new AsyncRelayCommand<WorkspaceEntry>(UpdateLayoutEntryAsync));

	public IAsyncRelayCommand<WorkspaceEntry?> DeleteLayoutEntryCommand => deleteLayoutEntryCommand ?? (deleteLayoutEntryCommand = new AsyncRelayCommand<WorkspaceEntry>(DeleteLayoutEntryAsync));

	public IAsyncRelayCommand SwitchLayoutCommand => switchLayoutCommand ?? (switchLayoutCommand = new AsyncRelayCommand(SwitchLayoutAsync, HasSelectedLayout));

	public IAsyncRelayCommand UpdateLayoutCommand => updateLayoutCommand ?? (updateLayoutCommand = new AsyncRelayCommand(UpdateLayoutAsync, HasSelectedLayout));

	public IAsyncRelayCommand DeleteLayoutCommand => deleteLayoutCommand ?? (deleteLayoutCommand = new AsyncRelayCommand(DeleteLayoutAsync, HasSelectedLayout));

	public IAsyncRelayCommand SaveSnapshotCommand => saveSnapshotCommand ?? (saveSnapshotCommand = new AsyncRelayCommand(SaveSnapshotAsync));

	public IAsyncRelayCommand RestoreSnapshotCommand => restoreSnapshotCommand ?? (restoreSnapshotCommand = new AsyncRelayCommand(RestoreSnapshotAsync, HasSelectedBackup));

	public IAsyncRelayCommand<WorkspaceEntry?> RestoreHistoryEntryCommand => restoreHistoryEntryCommand ?? (restoreHistoryEntryCommand = new AsyncRelayCommand<WorkspaceEntry>(RestoreHistoryEntryAsync));

	public IAsyncRelayCommand ExportBasicSettingsCommand => exportBasicSettingsCommand ?? (exportBasicSettingsCommand = new AsyncRelayCommand(ExportBasicSettingsAsync));

	public IAsyncRelayCommand ExportCompleteBackupCommand => exportCompleteBackupCommand ?? (exportCompleteBackupCommand = new AsyncRelayCommand(ExportCompleteBackupAsync));

	public IAsyncRelayCommand ImportWorkspaceCommand => importWorkspaceCommand ?? (importWorkspaceCommand = new AsyncRelayCommand(ImportWorkspaceAsync));

	public IAsyncRelayCommand<ThemeSuite?> ApplyThemeSuiteCommand => applyThemeSuiteCommand ?? (applyThemeSuiteCommand = new AsyncRelayCommand<ThemeSuite>(ApplyThemeSuiteAsync));

	[RelayCommand]
	private void ClearRemovalSearch()
	{
		RemovalSearch = string.Empty;
	}

	private void FilterRemovalChoices()
	{
		VisibleRemovalChoices.Clear();
		foreach (AppRemovalChoice item in RemovalChoices.Where((AppRemovalChoice x) => x.App.DisplayName.Contains(RemovalSearch.Trim(), StringComparison.CurrentCultureIgnoreCase)))
		{
			VisibleRemovalChoices.Add(item);
		}
	}

	[RelayCommand]
	private void BeginRemoveApplications()
	{
		RemovalChoices.Clear();
		foreach (AppItem item in _mainViewModel.AllApps.Where((AppItem x) => !x.IsHidden && !x.IsFolder && !x.IsWidget).OrderBy((AppItem x) => x.DisplayName, StringComparer.CurrentCultureIgnoreCase))
		{
			AppRemovalChoice appRemovalChoice = new AppRemovalChoice(item);
			appRemovalChoice.PropertyChanged += (object? _, PropertyChangedEventArgs _) =>
			{
				OnPropertyChanged("RemovalSelectionText");
			};
			RemovalChoices.Add(appRemovalChoice);
		}
		RemovalSearch = string.Empty;
		FilterRemovalChoices();
		OnPropertyChanged("RemovalSelectionText");
		IsManagingApplications = true;
	}

	[RelayCommand]
	private void CancelRemoveApplications()
	{
		IsManagingApplications = false;
	}

	[RelayCommand]
	private void SelectVisibleApplications()
	{
		bool isSelected = VisibleRemovalChoices.Any((AppRemovalChoice x) => !x.IsSelected);
		foreach (AppRemovalChoice visibleRemovalChoice in VisibleRemovalChoices)
		{
			visibleRemovalChoice.IsSelected = isSelected;
		}
	}

	[RelayCommand]
	private async Task RemoveSelectedApplicationsAsync()
	{
		AppItem[] selected = (from x in RemovalChoices
			where x.IsSelected
			select x.App).ToArray();
		if (selected.Length == 0)
		{
			StatusFeedback = "请先选择要移出的应用";
			return;
		}
		await _mainViewModel.RemoveApplicationsAsync(selected);
		StatusFeedback = $"已移出 {selected.Length} 个应用，软件仍保留在电脑中";
		IsManagingApplications = false;
		RemovalChoices.Clear();
		VisibleRemovalChoices.Clear();
	}

	[RelayCommand]
	private void ApplySolidColorPreset(string? color)
	{
		if (color != null && SolidColorPresets.Any((Option<string> p) => p.Value == color))
		{
			Settings.SolidBackgroundColor = color;
		}
	}

	[RelayCommand]
	private void ChooseSolidBackgroundColor()
	{
		SetDeactivationBlock?.Invoke(obj: true);
		try
		{
			ColorPickerDialog colorPickerDialog = new ColorPickerDialog(Application.Current?.MainWindow, Settings.SolidBackgroundColor);
			colorPickerDialog.ShowDialog();
			string selectedColor = colorPickerDialog.SelectedColor;
			if (selectedColor != null)
			{
				Settings.SolidBackgroundColor = selectedColor;
			}
		}
		finally
		{
			SetDeactivationBlock?.Invoke(obj: false);
			Application.Current?.MainWindow?.Activate();
		}
	}

	public void NotifyCustomBackdropChanged()
	{
		OnPropertyChanged("HasCustomBackdrop");
		OnPropertyChanged("CustomBackdropName");
		OnPropertyChanged("BackdropSourceOptions");
		OnPropertyChanged("IsPresetWallpaper");
		OnPropertyChanged("SelectedBackgroundSource");
	}

	[RelayCommand]
	public void ChooseBackdropImage()
	{
		SetDeactivationBlock?.Invoke(obj: true);
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "选择背景图片",
				Filter = "图片 (*.jpg, *.png, *.bmp, *.webp, *.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif|所有文件|*.*",
				CheckFileExists = true
			};
			Window window = Application.Current?.MainWindow;
			if (((window != null) ? openFileDialog.ShowDialog(window) : openFileDialog.ShowDialog()) == true)
			{
				string text = Path.GetExtension(openFileDialog.FileName);
				if (string.IsNullOrEmpty(text))
				{
					text = ".png";
				}
				string text2 = Path.Combine(_storageService.StorageDirectory, "background" + text);
				string text3 = text2 + ".tmp";
				File.Copy(openFileDialog.FileName, text3, overwrite: true);
				if (File.Exists(text2))
				{
					File.Delete(text2);
				}
				File.Move(text3, text2);
				BackdropHelper.InvalidateCustomCache();
				if (Settings.CustomBackdropPath == text2)
				{
					Settings.CustomBackdropPath = string.Empty;
				}
				Settings.CustomBackdropPath = text2;
				Settings.BackdropSource = BackdropSource.CustomImage;
				NotifyCustomBackdropChanged();
				StatusFeedback = "背景图片已更新";
			}
		}
		catch (Exception ex)
		{
			App.Log("Copying the backdrop image failed: " + ex.Message);
			StatusFeedback = "无法使用该图片，详见日志";
		}
		finally
		{
			SetDeactivationBlock?.Invoke(obj: false);
			Application.Current?.MainWindow?.Activate();
		}
	}

	[RelayCommand]
	public void ClearBackdropImage()
	{
		SetDeactivationBlock?.Invoke(obj: true);
		try
		{
			if (!string.IsNullOrEmpty(Settings.CustomBackdropPath) && File.Exists(Settings.CustomBackdropPath))
			{
				File.Delete(Settings.CustomBackdropPath);
			}
		}
		catch (Exception ex)
		{
			App.Log("Removing the backdrop image failed: " + ex.Message);
		}
		finally
		{
			SetDeactivationBlock?.Invoke(obj: false);
			Application.Current?.MainWindow?.Activate();
		}
		BackdropHelper.InvalidateCustomCache();
		Settings.CustomBackdropPath = string.Empty;
		if (Settings.BackdropSource == BackdropSource.CustomImage)
		{
			Settings.BackdropSource = BackdropSource.Wallpaper;
		}
		NotifyCustomBackdropChanged();
		StatusFeedback = "已恢复桌面壁纸";
	}

	private void SyncIconPreset()
	{
		IconPreset selectedIconPreset = IconPresetOptions.FirstOrDefault((IconPreset p) => p.Shape == Settings.IconShape && p.ShowNames == Settings.ShowAppNames && Math.Abs(p.FontSize - Settings.AppNameFontSize) < 0.5);
		SelectedIconPreset = selectedIconPreset;
	}

	private static ImageSource? LoadPreviewBitmap(string filePath, int decodeWidth = 72)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
			bitmapImage.DecodePixelWidth = decodeWidth;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static IReadOnlyList<ImageSource> GetSystemPreviewIcons()
	{
		if (_systemPreviewsCache != null && _systemPreviewsCache.Count > 0)
		{
			return _systemPreviewsCache;
		}
		List<ImageSource> list = new List<ImageSource>();
		string[] array = new string[7]
		{
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Taskmgr.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe")
		};
		foreach (string path in array)
		{
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				BitmapSource bitmapSource = ShellIcons.ExtractHighResolutionIcon(path, 64);
				if (bitmapSource != null)
				{
					list.Add(bitmapSource);
					if (list.Count >= 4)
					{
						break;
					}
				}
			}
			catch
			{
			}
		}
		_systemPreviewsCache = list;
		return list;
	}

	private static IReadOnlyList<ImageSource> GetPackPreviewIcons(IconPack pack)
	{
		if (_packPreviewsCache.TryGetValue(pack.Id, out IReadOnlyList<ImageSource> value))
		{
			return value;
		}
		List<ImageSource> list = new List<ImageSource>();
		string[] array = new string[7] { "chrome", "code", "terminal", "steam", "weixin", "qq", "notion" };
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array2 = array;
		int num = 0;
		while (true)
		{
			if (num < array2.Length)
			{
				string value2 = array2[num];
				foreach (KeyValuePair<string, string> icon in pack.Icons)
				{
					if (!icon.Key.Contains(value2, StringComparison.OrdinalIgnoreCase) || !File.Exists(icon.Value) || !hashSet.Add(icon.Value))
					{
						continue;
					}
					ImageSource imageSource = LoadPreviewBitmap(icon.Value);
					if (imageSource != null)
					{
						list.Add(imageSource);
						if (list.Count >= 4)
						{
							goto end_IL_0103;
						}
					}
					break;
				}
				num++;
				continue;
			}
			if (list.Count >= 4)
			{
				break;
			}
			foreach (string item in pack.Icons.Values.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!hashSet.Add(item) || !File.Exists(item))
				{
					continue;
				}
				ImageSource imageSource2 = LoadPreviewBitmap(item);
				if (imageSource2 != null)
				{
					list.Add(imageSource2);
					if (list.Count >= 4)
					{
						break;
					}
				}
			}
			break;
			continue;
			end_IL_0103:
			break;
		}
		_packPreviewsCache[pack.Id] = list;
		return list;
	}

	private void RefreshIconPacks()
	{
		IReadOnlyList<IconPack> readOnlyList = IconPackService.ListPacks();
		if (IconPackOptions != null && IconPackOptions.Length == readOnlyList.Count + 1 && (from p in IconPackOptions.Skip(1)
			select p.Id).SequenceEqual(readOnlyList.Select((IconPack p) => p.Id)))
		{
			return;
		}
		IReadOnlyList<ImageSource> systemPreviewIcons = GetSystemPreviewIcons();
		List<IconPackChoice> list = new List<IconPackChoice>
		{
			new IconPackChoice("", "系统图标", "Windows 原生", "保留应用与系统的原始设计风格", systemPreviewIcons)
		};
		foreach (IconPack item in readOnlyList)
		{
			string tag;
			if (item.Icons.Count > 0)
			{
				tag = $"{item.Icons.Count}+ 图标";
			}
			else
			{
				tag = (item.IsOfficial ? "官方预设" : "图标包");
			}
			string description = (item.IsOfficial ? "替换为整套图标；部分应用与自动磁贴相似" : "第三方自定义图标包");
			IReadOnlyList<ImageSource> packPreviewIcons = GetPackPreviewIcons(item);
			list.Add(new IconPackChoice(item.Id, item.Name + " · PRO", tag, description, packPreviewIcons));
		}
		IconPackOptions = list.ToArray();
		OnPropertyChanged("IconPackOptions");
		OnPropertyChanged("SelectedIconPackId");
	}

	private void SyncIconPack()
	{
		if (!string.IsNullOrEmpty(Settings.IconPack) && IconPackService.Find(Settings.IconPack) == null)
		{
			Settings.IconPack = string.Empty;
		}
		RefreshIconPacks();
		OnPropertyChanged("SelectedIconPackId");
	}

	private void RefreshAccount()
	{
		OnPropertyChanged("HasRedeemedInvite");
		IsPro = true;
		LicenseStatusText = "开源版";
		LicenseDetailText = "全部功能开放 · Apache-2.0";
		DeviceText = "本机：" + Environment.MachineName;
		InviteCodeText = string.Empty;
		VersionText = _updates.CurrentVersion;
		DateTime lastCheckedAt = _updates.LastCheckedAt;
		SettingsViewModel settingsViewModel = this;
		string updateSummaryText;
		if (_updates.Pending != null)
		{
			updateSummaryText = "有新版本 " + _updates.Pending.Version;
		}
		else
		{
			updateSummaryText = ((lastCheckedAt == default(DateTime)) ? "尚未检查过更新" : $"上次检查 {lastCheckedAt:MM-dd HH:mm}");
		}
		settingsViewModel.UpdateSummaryText = updateSummaryText;
		if (DistributionChannel.IsStore)
		{
			UpdateSummaryText = "此版本由 Microsoft Store 更新，请在商店的下载页面检查更新。";
		}
		HasReverted = false;
		RevertedText = string.Empty;
	}

	[RelayCommand]
	private void CopyFeedbackEmail()
	{
		StatusFeedback = "正在复制…";
		CopyOnOwnStaThread("xujtao@foxmail.com", "反馈邮箱已复制");
	}

	private void CopyOnOwnStaThread(string code, string successMessage = "邀请码已复制")
	{
		if (Interlocked.Exchange(ref _copyInProgress, 1) != 0)
		{
			return;
		}
		Window window = Application.Current?.MainWindow;
		nint owner = ((window != null) ? new WindowInteropHelper(window).Handle : IntPtr.Zero);
		Thread thread = new Thread(() =>
		{
			bool copied = false;
			try
			{
				for (int i = 0; i < 12; i++)
				{
					if (ClipboardText.TrySet(code, owner))
					{
						copied = true;
						break;
					}
					if (i < 11)
					{
						Thread.Sleep(50);
					}
				}
			}
			finally
			{
				Interlocked.Exchange(ref _copyInProgress, 0);
				Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
				{
					StatusFeedback = (copied ? successMessage : "剪贴板被其他程序占用，请稍后重试");
				}));
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}

	[RelayCommand]
	public async Task CheckUpdatesAsync()
	{
		StatusFeedback = "正在检查更新…";
		StatusFeedback = await App.CheckForUpdatesNowAsync();
		RefreshAccount();
	}

	[RelayCommand]
	public async Task RestoreHiddenApp(AppItem? app)
	{
		if (app != null && app.IsHidden)
		{
			await _mainViewModel.UnhideApp(app);
			StatusFeedback = "已恢复 " + app.DisplayName;
		}
	}

	[RelayCommand]
	public async Task RestoreAllHidden()
	{
		if (HiddenApps.Count != 0)
		{
			int count = HiddenApps.Count;
			await _mainViewModel.UnhideAll();
			StatusFeedback = $"已恢复 {count} 个应用";
		}
	}

	private static int SnapToOption(int value, int[] options)
	{
		int num = options[0];
		int num2 = Math.Abs(value - num);
		for (int i = 1; i < options.Length; i++)
		{
			int num3 = Math.Abs(value - options[i]);
			if (num3 < num2)
			{
				num = options[i];
				num2 = num3;
			}
		}
		return num;
	}

	public SettingsViewModel(IStorageService storageService, IStartupService startupService, IAppScannerService scannerService, MainViewModel mainViewModel, UpdateService updates)
	{
		_storageService = storageService;
		_startupService = startupService;
		_scannerService = scannerService;
		_mainViewModel = mainViewModel;
		_updates = updates;
		_settings = new AppSettings();
		_persistTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(450.0)
		};
		_persistTimer.Tick += OnPersistTick;
		RefreshIconPacks();
		RefreshAccount();
		_updates.Changed += RefreshAccount;
		RefreshAccount();
		InitializeWorkspaces();
		PrepareAppearancePreviewsAsync();
		RefreshWorkspaceLibraryAsync();
	}

	private void InitializeWorkspaces()
	{
		BackupHistory.CollectionChanged += (object? _, NotifyCollectionChangedEventArgs _) =>
		{
			UpdateVisibleBackupHistory();
		};
		UpdateVisibleBackupHistory();
	}

	private void OnSettingEdited(object? sender, PropertyChangedEventArgs e)
	{
		if (_suppressLive)
		{
			return;
		}
		string propertyName = e.PropertyName;
		if ((propertyName == "ClockLargeMode" || propertyName == "ClockLargeStyle") ? true : false)
		{
			OnPropertyChanged("IsClockColorEditable");
		}
		propertyName = e.PropertyName;
		if ((propertyName == "ShowTopClock" || propertyName == "ClockLargeMode") ? true : false)
		{
			OnPropertyChanged("SimpleClockMode");
		}
		propertyName = e.PropertyName;
		if ((propertyName == "Columns" || propertyName == "Rows") ? true : false)
		{
			OnPropertyChanged("SimpleGridLayout");
			OnPropertyChanged("SimpleGridDescription");
		}
		if (e.PropertyName == "ClockLargeFontSize")
		{
			OnPropertyChanged("SimpleClockSize");
		}
		switch (e.PropertyName)
		{
		case "IconPack":
			OnPropertyChanged("SelectedIconPackId");
			break;
		case "Backdrop":
		case "BackdropSource":
		case "CustomBackdropPath":
			NotifyCustomBackdropChanged();
			break;
		case "Columns":
		case "Rows":
			_mainViewModel.ApplyLayoutSettings(Settings);
			break;
		case "LaunchAtStartup":
			_startupDirty = true;
			break;
		case "HotkeyDisplay":
			OnPropertyChanged("HotkeyCaptureText");
			break;
		case "HotCorner":
		case "DesktopDockHotCorner":
		case "ResidentPinHotCorner":
		case "EnableResidentPinHotCorner":
		case "DesktopResidentLaunchpad":
			OnPropertyChanged("LaunchpadHotCornerOptions");
			OnPropertyChanged("DesktopDockHotCornerOptions");
			OnPropertyChanged("ResidentPinHotCornerOptions");
			break;
		case "AppNameFontSize":
		case "IconShape":
		case "ShowAppNames":
			if (!_applyingPreset)
			{
				SyncIconPreset();
			}
			break;
		case "SearchPosition":
			ResolvePositionConflict(isSearchChanged: true);
			break;
		case "TopWidgetsPosition":
			ResolvePositionConflict(isSearchChanged: false);
			break;
		}
		_persistTimer.Stop();
		_persistTimer.Start();
	}

	private async void OnPersistTick(object? sender, EventArgs e)
	{
		_persistTimer.Stop();
		await PersistAsync();
	}

	private async Task PersistAsync()
	{
		try
		{
			if (_startupDirty)
			{
				_startupService.SetStartup(Settings.LaunchAtStartup);
				_startupDirty = false;
			}
			StatusFeedback = ((await _storageService.SaveSettingsAsync(Settings)) ? "已自动保存" : "保存失败，请检查磁盘空间和目录权限后重试");
		}
		catch (Exception ex)
		{
			App.Log("Autosave failed: " + ex.Message);
			StatusFeedback = "保存失败，详见日志";
		}
	}

	public async Task FlushPendingSaveAsync()
	{
		_persistTimer.Stop();
		await PersistAsync();
		if (!WorkspaceBusy)
		{
			try
			{
				await SaveActiveSceneAsync();
			}
			catch (Exception ex)
			{
				App.Log("Scene save: " + ex);
				StatusFeedback = "场景保存失败，请重试";
			}
		}
	}

	public async Task LoadSettingsAsync()
	{
		_suppressLive = true;
		try
		{
			Settings = await _storageService.LoadSettingsAsync();
			Settings.LaunchAtStartup = _startupService.IsStartupEnabled();
			Settings.Columns = SnapToOption(Settings.Columns, ColumnOptions);
			Settings.Rows = SnapToOption(Settings.Rows, RowOptions);
			SyncIconPreset();
			SyncIconPack();
			NotifyCustomBackdropChanged();
		}
		finally
		{
			_suppressLive = false;
		}
		_mainViewModel.ApplyLayoutSettings(Settings, resetPage: false);
	}

	[RelayCommand]
	public async Task RescanApplicationsAsync()
	{
		StatusFeedback = "正在扫描应用…";
		await _mainViewModel.RescanApplicationsImmediatelyAsync();
		StatusFeedback = $"扫描完成，共 {_mainViewModel.AllApps.Count} 个应用";
	}

	[RelayCommand]
	public async Task AddApplicationAsync()
	{
		StatusFeedback = "选择程序中…";
		await _mainViewModel.AddApplicationByPickingAsync();
		OnPropertyChanged("ManualAppCount");
		StatusFeedback = _mainViewModel.StatusMessage;
	}

	[RelayCommand]
	public async Task ResetDatabaseAsync()
	{
		SetDeactivationBlock?.Invoke(obj: true);
		bool flag;
		try
		{
			flag = ConfirmDialog.Ask("重置应用数据库", "确定要清空应用数据库和图标缓存，并重新扫描吗？", "应用列表、Dock、分组和图标缓存会被清空后重新扫描；手动添加的应用保留。", "重置", "取消", destructive: true);
		}
		finally
		{
			SetDeactivationBlock?.Invoke(obj: false);
			Application.Current?.MainWindow?.Activate();
		}
		if (flag)
		{
			StatusFeedback = "正在重置…";
			await _storageService.ResetApplicationDatabaseAsync();
			await _mainViewModel.RescanApplicationsAsync();
			StatusFeedback = "数据库已重建";
		}
	}

	[RelayCommand]
	public async Task AddClockWidgetAsync()
	{
		await _mainViewModel.ToggleWidgetOnGridAsync("Clock");
		StatusFeedback = (_mainViewModel.HasClockWidgetOnGrid ? "已添加大字时钟到桌面" : "已从桌面移除时钟");
	}

	[RelayCommand]
	public async Task AddSystemMonitorWidgetAsync()
	{
		await _mainViewModel.ToggleWidgetOnGridAsync("SystemMonitor");
		StatusFeedback = (_mainViewModel.HasMonitorWidgetOnGrid ? "已添加性能监控到桌面" : "已从桌面移除监控");
	}

	[RelayCommand]
	public async Task MoveWidgetToFrontAsync(string? widgetType)
	{
		await _mainViewModel.MoveWidgetToFrontAsync(widgetType);
		StatusFeedback = _mainViewModel.StatusMessage;
	}

	private void ResolvePositionConflict(bool isSearchChanged)
	{
		if (!string.Equals(Settings.SearchPosition, Settings.TopWidgetsPosition, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string[] source = new string[3] { "Center", "Right", "Left" };
		if (isSearchChanged)
		{
			string text = source.FirstOrDefault((string s) => !string.Equals(s, Settings.SearchPosition, StringComparison.OrdinalIgnoreCase)) ?? "Right";
			Settings.TopWidgetsPosition = text;
			StatusFeedback = "为避免重叠，组件栏已自动调整至：" + GetPositionDisplayName(text);
		}
		else
		{
			string text2 = source.FirstOrDefault((string s) => !string.Equals(s, Settings.TopWidgetsPosition, StringComparison.OrdinalIgnoreCase)) ?? "Center";
			Settings.SearchPosition = text2;
			StatusFeedback = "为避免重叠，搜索栏已自动调整至：" + GetPositionDisplayName(text2);
		}
	}

	private static string GetPositionDisplayName(string pos)
	{
		return pos switch
		{
			"Left" => "左上角", 
			"Center" => "居中", 
			"Right" => "右上角", 
			_ => pos, 
		};
	}

	[RelayCommand]
	public void OpenDateAndTimeSettings()
	{
		_mainViewModel.OpenDateAndTimeSettings();
	}

	[RelayCommand]
	public void OpenTaskManager()
	{
		_mainViewModel.OpenTaskManager();
	}

	[RelayCommand]
	public void OpenWidgetBoard()
	{
		_mainViewModel.OpenWidgetBoard();
	}

	[RelayCommand]
	public async Task ResetSettingsAsync()
	{
		SetDeactivationBlock?.Invoke(obj: true);
		bool flag;
		try
		{
			flag = ConfirmDialog.Ask("恢复默认设置", "确定要把所有设置恢复为默认值吗？", "包括：布局、外观、图标（含图标包和大小）、动效、手势、快捷键、开机自启。\n不影响：应用列表、Dock、分组、手动添加的应用、单个应用的自选图标。", "恢复默认", "取消", destructive: true);
		}
		finally
		{
			SetDeactivationBlock?.Invoke(obj: false);
			Application.Current?.MainWindow?.Activate();
		}
		if (!flag)
		{
			return;
		}
		Settings.ResetToDefault();
		_applyingPreset = true;
		try
		{
			SyncIconPreset();
		}
		finally
		{
			_applyingPreset = false;
		}
		SyncIconPack();
		NotifyCustomBackdropChanged();
		AppSettings appSettings = new AppSettings();
		if (!App.TryAdoptHotkey(appSettings.HotkeyModifiers, appSettings.HotkeyKey, out string _))
		{
			App.Log("[RESET] default hotkey could not be registered, kept previous");
		}
		_persistTimer.Stop();
		await PersistAsync();
		StatusFeedback = "已恢复默认设置";
		App.Log("[RESET] settings restored to defaults");
	}

	public async Task PrepareAppearancePreviewsAsync()
	{
		ThemeSuite[] themeSuites = ThemeSuites;
		for (int i = 0; i < themeSuites.Length; i++)
		{
			await themeSuites[i].PreparePreviewAsync();
		}
	}

	[RelayCommand]
	private async Task SelectPresetWallpaperAsync(ThemeSuite? suite)
	{
		if (!(suite == null) && suite.Pro && RequireWorkspacePro())
		{
			await WorkspaceOperationAsync(async () =>
			{
				Settings.CustomBackdropPath = CreateSuiteBackground(suite);
				Settings.BackdropSource = BackdropSource.CustomImage;
				Settings.EnableAuroraBackground = false;
				NotifyCustomBackdropChanged();
				await _storageService.SaveSettingsAsync(Settings);
				WorkspaceFeedback = "已应用壁纸「" + suite.Name + "」";
			});
		}
	}

	public async Task MoveSceneAsync(WorkspaceEntry source, WorkspaceEntry target)
	{
		if (WorkspaceBusy || source.Id == target.Id || !RequireWorkspacePro())
		{
			return;
		}
		await WorkspaceOperationAsync(async () =>
		{
			List<string> list = LayoutPresets.Select((WorkspaceEntry e) => e.Id).ToList();
			int num = list.IndexOf(source.Id);
			int num2 = list.IndexOf(target.Id);
			if (num >= 0 && num2 >= 0)
			{
				list.RemoveAt(num);
				list.Insert(num2, source.Id);
				await Library.SaveSceneOrderAsync(list);
				await RefreshWorkspaceLibraryAsync();
				WorkspaceFeedback = "场景顺序已保存，数字快捷键已同步更新";
			}
		});
	}

	public static int ResolveSceneShortcutIndex(int count, int current, int number, int direction)
	{
		if (count <= 0)
		{
			return -1;
		}
		if (number > 0)
		{
			if (number > count)
			{
				return -1;
			}
			return number - 1;
		}
		if (direction == 0)
		{
			return -1;
		}
		if (current < 0 || current >= count)
		{
			if (direction <= 0)
			{
				return count - 1;
			}
			return 0;
		}
		return (current + ((direction > 0) ? 1 : (-1)) + count) % count;
	}

	public bool TrySwitchSceneShortcut(int number, int direction)
	{
		int current = LayoutPresets.ToList().FindIndex((WorkspaceEntry e) => e.Id == Settings.ActiveLayoutId);
		int num = ResolveSceneShortcutIndex(LayoutPresets.Count, current, number, direction);
		if (num < 0)
		{
			return false;
		}
		if (!WorkspaceBusy)
		{
			UseLayoutEntryCommand.Execute(LayoutPresets[num]);
		}
		return true;
	}

	[RelayCommand]
	private void ToggleBackupHistoryExpanded()
	{
		IsBackupHistoryExpanded = !IsBackupHistoryExpanded;
		UpdateVisibleBackupHistory();
	}

	private void UpdateVisibleBackupHistory()
	{
		IEnumerable<WorkspaceEntry> source;
		if (!IsBackupHistoryExpanded)
		{
			source = BackupHistory.Take(3);
		}
		else
		{
			IEnumerable<WorkspaceEntry> backupHistory = BackupHistory;
			source = backupHistory;
		}
		List<WorkspaceEntry> list = source.ToList();
		if (!VisibleBackupHistory.SequenceEqual(list))
		{
			VisibleBackupHistory.Clear();
			foreach (WorkspaceEntry item in list)
			{
				VisibleBackupHistory.Add(item);
			}
		}
		OnPropertyChanged("HasMoreBackupHistory");
		OnPropertyChanged("RemainingBackupHistoryCount");
		OnPropertyChanged("BackupHistoryExpandText");
	}

	private bool HasSelectedLayout()
	{
		return SelectedLayout != null;
	}

	private bool HasSelectedBackup()
	{
		return SelectedBackup != null;
	}

	private async Task SaveActiveSceneAsync()
	{
		WorkspaceEntry active = Library.List("layouts").FirstOrDefault((WorkspaceEntry e) => e.Id == Settings.ActiveLayoutId);
		if (active != null)
		{
			WorkspaceLibrary library = Library;
			await library.SaveAsync("layouts", await CaptureWorkspaceAsync(active.Name), active.Id);
		}
	}

	private async Task PreserveCurrentSceneAsync()
	{
		if (string.IsNullOrEmpty(Settings.ActiveLayoutId))
		{
			HashSet<string> hashSet = (from e in Library.List("layouts")
				select e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
			string text = "默认桌面";
			int num = 2;
			while (hashSet.Contains(text))
			{
				text = "默认桌面 " + num;
				num++;
			}
			AppSettings settings = Settings;
			WorkspaceLibrary library = Library;
			settings.ActiveLayoutId = await library.SaveAsync("layouts", await CaptureWorkspaceAsync(text));
		}
		else
		{
			await SaveActiveSceneAsync();
		}
	}

	public async Task RefreshWorkspaceLibraryAsync()
	{
		List<WorkspaceEntry> layouts = await Task.Run(() => Library.List("layouts"));
		List<WorkspaceEntry> list = await Task.Run(() => Library.List("history"));
		string layoutId = SelectedLayout?.Id;
		string historyId = SelectedBackup?.Id;
		LayoutPresets.Clear();
		foreach (WorkspaceEntry item in layouts)
		{
			LayoutPresets.Add(item);
		}
		BackupHistory.Clear();
		foreach (WorkspaceEntry item2 in list)
		{
			BackupHistory.Add(item2);
		}
		UpdateVisibleBackupHistory();
		SelectedLayout = layouts.FirstOrDefault((WorkspaceEntry e) => e.Id == layoutId) ?? layouts.FirstOrDefault();
		SelectedBackup = list.FirstOrDefault((WorkspaceEntry e) => e.Id == historyId) ?? list.FirstOrDefault();
		OnPropertyChanged("CurrentSceneText");
	}

	private bool RequireWorkspacePro()
	{
		if (true)
		{
			return true;
		}
		WorkspaceFeedback = "此功能属于 Pro；基础功能仍可继续使用，可在账户页激活专业版";
		return false;
	}

	private async Task WorkspaceOperationAsync(Func<Task> operation)
	{
		if (WorkspaceBusy)
		{
			return;
		}
		WorkspaceBusy = true;
		_persistTimer.Stop();
		try
		{
			await operation();
		}
		catch (Exception ex)
		{
			WorkspaceFeedback = "操作未完成：" + ex.Message;
			App.Log("Workspace: " + ex);
			if (!string.IsNullOrEmpty(_mainViewModel.SceneSwitchFeedback))
			{
				_mainViewModel.ShowSceneSwitchFeedback("场景切换失败，请重试");
			}
		}
		finally
		{
			WorkspaceBusy = false;
		}
	}

	private Task<WorkspaceBundle> CaptureWorkspaceAsync(string name, bool complete = true)
	{
		return Library.CaptureAsync(name, Settings, _mainViewModel.AllApps, _storageService, complete);
	}

	private async Task SnapshotAsync(string reason)
	{
		WorkspaceLibrary library = Library;
		await library.SaveAsync("history", await CaptureWorkspaceAsync(reason, true));
	}

	private static void CopyWorkspaceProperties(AppSettings source, AppSettings target)
	{
		foreach (PropertyInfo item in from p in typeof(AppSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public)
			where p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
			select p)
		{
			bool flag;
			switch (item.Name)
			{
			case "HotkeyModifiers":
			case "LaunchAtStartup":
			case "ActiveLayoutId":
			case "ScanSteamGames":
			case "HotkeyKey":
			case "HotkeyDisplay":
			case "OpenOnCurrentMonitor":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				item.SetValue(target, item.GetValue(source));
			}
		}
		if (target.BackdropSource == BackdropSource.Screen)
		{
			target.BackdropSource = BackdropSource.Wallpaper;
		}
	}

	private void CopyWorkspaceSettings(AppSettings source)
	{
		bool startup = Settings.LaunchAtStartup;
		uint hotkeyModifiers = Settings.HotkeyModifiers;
		uint hotkeyKey = Settings.HotkeyKey;
		_suppressLive = true;
		try
		{
			_mainViewModel.ApplySettingsBatch(() =>
			{
				Copy();
			});
		}
		finally
		{
			_suppressLive = false;
		}
		_mainViewModel.ApplyLayoutSettings(Settings);
		if (Application.Current is App && !App.TryAdoptHotkey(Settings.HotkeyModifiers, Settings.HotkeyKey, out string _))
		{
			Settings.HotkeyModifiers = hotkeyModifiers;
			Settings.HotkeyKey = hotkeyKey;
		}
		SyncIconPreset();
		SyncIconPack();
		NotifyCustomBackdropChanged();
		OnPropertyChanged("SimpleClockMode");
		OnPropertyChanged("SimpleClockSize");
		OnPropertyChanged("SimpleGridLayout");
		OnPropertyChanged("SimpleGridDescription");
		OnPropertyChanged("HotkeyCaptureText");
		void Copy()
		{
			CopyWorkspaceProperties(source, Settings);
			Settings.LaunchAtStartup = startup;
		}
	}

	private async Task RestoreWorkspaceAsync(WorkspaceBundle bundle, string? activeLayoutId = null)
	{
		_persistTimer.Stop();
		await Library.MaterializeAsync(bundle);
		AppSettings candidate = PersistenceSnapshot.Settings(Settings);
		CopyWorkspaceProperties(bundle.Settings, candidate);
		if (activeLayoutId != null)
		{
			candidate.ActiveLayoutId = activeLayoutId;
		}
		List<string> imported = new List<string>();
		bool committed = false;
		try
		{
			HashSet<string> names = (from e in Library.List("layouts")
				select e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (WorkspaceBundle saved in bundle.SavedLayouts)
			{
				string name = saved.Name;
				int num = 1;
				while (names.Contains(saved.Name))
				{
					saved.Name = name + "（导入 " + num++ + "）";
				}
				List<string> list = imported;
				list.Add(await Library.SaveAsync("layouts", saved));
				names.Add(saved.Name);
			}
			if (bundle.Complete)
			{
				await _mainViewModel.RestoreWorkspaceAsync(bundle, candidate);
			}
			else if (!(await _storageService.SaveSettingsAsync(candidate)))
			{
				throw new IOException("设置保存失败，原设置已保留");
			}
			committed = true;
			CopyWorkspaceSettings(candidate);
			Settings.ActiveLayoutId = candidate.ActiveLayoutId;
		}
		finally
		{
			if (!committed)
			{
				foreach (string item in imported)
				{
					Library.Delete("layouts", item);
				}
			}
		}
		await RefreshWorkspaceLibraryAsync();
	}

	[RelayCommand]
	private void BeginAddLayout()
	{
		LayoutName = string.Empty;
		_renamingLayoutId = null;
		OnPropertyChanged("SceneEditorTitle");
		IsAddingLayout = true;
	}

	[RelayCommand]
	private void CancelAddLayout()
	{
		IsAddingLayout = false;
	}

	[RelayCommand]
	private void RenameLayoutEntry(WorkspaceEntry? entry)
	{
		if (!(entry == null) && !WorkspaceBusy)
		{
			_renamingLayoutId = entry.Id;
			LayoutName = entry.Name;
			OnPropertyChanged("SceneEditorTitle");
			IsAddingLayout = true;
		}
	}

	[RelayCommand]
	private async Task SaveLayoutAsync()
	{
		if (!RequireWorkspacePro())
		{
			return;
		}
		await WorkspaceOperationAsync(async () =>
		{
			string name = LayoutName.Trim();
			int length = name.Length;
			if ((length < 1 || length > 32) ? true : false)
			{
				throw new InvalidDataException("布局名称请输入 1–32 个字符");
			}
			if (LayoutPresets.Any((WorkspaceEntry e) => e.Id != _renamingLayoutId && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidDataException("名称已存在，请换一个场景名称");
			}
			string renameId = _renamingLayoutId;
			string id;
			if (renameId != null)
			{
				await SaveActiveSceneAsync();
				WorkspaceBundle workspaceBundle = await Library.LoadAsync("layouts", renameId);
				workspaceBundle.Name = name;
				id = await Library.SaveAsync("layouts", workspaceBundle, renameId);
			}
			else
			{
				await PreserveCurrentSceneAsync();
				WorkspaceBundle fresh = WorkspaceLibrary.CreateFreshScene(name, _mainViewModel.AllApps);
				id = await Library.SaveAsync("layouts", fresh);
				await RestoreWorkspaceAsync(fresh, id);
			}
			await RefreshWorkspaceLibraryAsync();
			SelectedLayout = LayoutPresets.First((WorkspaceEntry e) => e.Id == id);
			IsAddingLayout = false;
			WorkspaceFeedback = ((_renamingLayoutId == null) ? ("已创建并切换到「" + name + "」，从默认配置开始") : "已修改场景名称");
			_renamingLayoutId = null;
		});
	}

	[RelayCommand]
	private async Task UseLayoutEntryAsync(WorkspaceEntry? entry)
	{
		if (!(entry == null) && !WorkspaceBusy)
		{
			SelectedLayout = entry;
			await SwitchLayoutAsync();
		}
	}

	[RelayCommand]
	private async Task UpdateLayoutEntryAsync(WorkspaceEntry? entry)
	{
		if (!(entry == null) && !WorkspaceBusy)
		{
			SelectedLayout = entry;
			await UpdateLayoutAsync();
		}
	}

	[RelayCommand]
	private async Task DeleteLayoutEntryAsync(WorkspaceEntry? entry)
	{
		if (!(entry == null) && !WorkspaceBusy)
		{
			SelectedLayout = entry;
			await DeleteLayoutAsync();
		}
	}

	[RelayCommand(CanExecute = "HasSelectedLayout")]
	private async Task SwitchLayoutAsync()
	{
		if (!RequireWorkspacePro() || SelectedLayout == null)
		{
			return;
		}
		WorkspaceEntry selected = SelectedLayout;
		if (selected.Id == Settings.ActiveLayoutId)
		{
			_mainViewModel.ShowSceneSwitchFeedback("当前场景 · " + selected.Name);
			return;
		}
		await WorkspaceOperationAsync(async () =>
		{
			WorkspaceBundle bundle = await Library.LoadAsync("layouts", selected.Id);
			await PreserveCurrentSceneAsync();
			await SnapshotAsync("切换布局前");
			await RestoreWorkspaceAsync(bundle, selected.Id);
			await RefreshWorkspaceLibraryAsync();
			WorkspaceFeedback = "已切换到「" + bundle.Name + "」；原布局已保存在历史快照";
			_mainViewModel.ShowSceneSwitchFeedback("已切换到 · " + bundle.Name);
		});
	}

	[RelayCommand(CanExecute = "HasSelectedLayout")]
	private async Task UpdateLayoutAsync()
	{
		if (RequireWorkspacePro() && !(SelectedLayout == null))
		{
			WorkspaceEntry selected = SelectedLayout;
			await WorkspaceOperationAsync(async () =>
			{
				WorkspaceFeedback = "正在更新「" + selected.Name + "」…";
				WorkspaceBundle workspaceBundle = await Library.LoadAsync("layouts", selected.Id);
				workspaceBundle.Name = "更新前 · " + workspaceBundle.Name;
				workspaceBundle.CreatedUtc = DateTime.UtcNow;
				await Library.SaveAsync("history", workspaceBundle);
				WorkspaceLibrary library = Library;
				await library.SaveAsync("layouts", await CaptureWorkspaceAsync(selected.Name), selected.Id);
				await RefreshWorkspaceLibraryAsync();
				WorkspaceFeedback = "已更新布局；旧版本保存在历史快照";
			});
		}
	}

	[RelayCommand(CanExecute = "HasSelectedLayout")]
	private async Task DeleteLayoutAsync()
	{
		if (!RequireWorkspacePro() || SelectedLayout == null)
		{
			return;
		}
		WorkspaceEntry selected = SelectedLayout;
		await WorkspaceOperationAsync(async () =>
		{
			WorkspaceFeedback = "正在删除「" + selected.Name + "」…";
			if (selected.Id == Settings.ActiveLayoutId)
			{
				await SaveActiveSceneAsync();
			}
			WorkspaceBundle workspaceBundle = await Library.LoadAsync("layouts", selected.Id);
			workspaceBundle.Name = "删除前 · " + workspaceBundle.Name;
			workspaceBundle.CreatedUtc = DateTime.UtcNow;
			await Library.SaveAsync("history", workspaceBundle);
			Library.Delete("layouts", selected.Id);
			if (Settings.ActiveLayoutId == selected.Id)
			{
				Settings.ActiveLayoutId = string.Empty;
				if (!(await _storageService.SaveSettingsAsync(Settings)))
				{
					throw new IOException("设置保存失败");
				}
			}
			await RefreshWorkspaceLibraryAsync();
			WorkspaceFeedback = "已删除保存的布局，当前桌面不受影响";
		});
	}

	[RelayCommand]
	private async Task SaveSnapshotAsync()
	{
		if (RequireWorkspacePro())
		{
			await WorkspaceOperationAsync(async () =>
			{
				WorkspaceFeedback = "正在保存当前桌面…";
				await SnapshotAsync(string.IsNullOrWhiteSpace(SnapshotName) ? "手动快照" : SnapshotName.Trim());
				await RefreshWorkspaceLibraryAsync();
				WorkspaceFeedback = "已保存完整快照；最多保留最近 20 个";
			});
		}
	}

	[RelayCommand(CanExecute = "HasSelectedBackup")]
	private async Task RestoreSnapshotAsync()
	{
		if (SelectedBackup == null || !RequireWorkspacePro())
		{
			return;
		}
		WorkspaceEntry entry = SelectedBackup;
		await WorkspaceOperationAsync(async () =>
		{
			WorkspaceFeedback = "正在恢复「" + entry.Name + "」…";
			WorkspaceBundle bundle = await Library.LoadAsync("history", entry.Id);
			if (!bundle.Complete || RequireWorkspacePro())
			{
				await SnapshotAsync("恢复快照前");
				await RestoreWorkspaceAsync(bundle);
				WorkspaceFeedback = "已恢复快照「" + entry.Name + "」；如需撤回，可恢复刚保存的「恢复快照前」";
			}
		});
	}

	[RelayCommand]
	private async Task RestoreHistoryEntryAsync(WorkspaceEntry? entry)
	{
		if (!(entry == null) && !WorkspaceBusy)
		{
			SelectedBackup = entry;
			await RestoreSnapshotAsync();
		}
	}

	private string? PickWorkspaceFile(bool save, bool complete)
	{
		SetDeactivationBlock?.Invoke(obj: true);
		try
		{
			Window window = Application.Current?.MainWindow;
			if (save)
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Title = (complete ? "导出完整备份" : "导出基础设置"),
					Filter = "Fluorite 配置|*.fluorite",
					FileName = (complete ? "Fluorite-完整备份.fluorite" : "Fluorite-基础设置.fluorite")
				};
				return (((window != null) ? saveFileDialog.ShowDialog(window) : saveFileDialog.ShowDialog()) == true) ? saveFileDialog.FileName : null;
			}
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "导入 Fluorite 配置",
				Filter = "Fluorite 配置|*.fluorite"
			};
			return (((window != null) ? openFileDialog.ShowDialog(window) : openFileDialog.ShowDialog()) == true) ? openFileDialog.FileName : null;
		}
		finally
		{
			Application.Current?.MainWindow?.Activate();
			SetDeactivationBlock?.Invoke(obj: false);
		}
	}

	[RelayCommand]
	private Task ExportBasicSettingsAsync()
	{
		return ExportWorkspaceAsync(complete: false);
	}

	[RelayCommand]
	private Task ExportCompleteBackupAsync()
	{
		return ExportWorkspaceAsync(complete: true);
	}

	private async Task ExportWorkspaceAsync(bool complete)
	{
		if (complete && !RequireWorkspacePro())
		{
			return;
		}
		await WorkspaceOperationAsync(async () =>
		{
			string file = PickWorkspaceFile(save: true, complete);
			if (file != null)
			{
				WorkspaceBundle bundle = await CaptureWorkspaceAsync("导出备份", complete);
				if (complete)
				{
					foreach (WorkspaceEntry item in Library.List("layouts"))
					{
						List<WorkspaceBundle> savedLayouts = bundle.SavedLayouts;
						savedLayouts.Add(await Library.LoadAsync("layouts", item.Id));
					}
				}
				await Library.WriteAsync(file, bundle);
				WorkspaceFeedback = (complete ? "完整备份已导出，含背景与自定义图标；不含许可证" : "基础设置已导出，不包含排列与图片资源");
			}
		});
	}

	[RelayCommand]
	private async Task ImportWorkspaceAsync()
	{
		await WorkspaceOperationAsync(async () =>
		{
			string text = PickWorkspaceFile(save: false, complete: false);
			if (text != null)
			{
				WorkspaceBundle bundle = await Library.ReadAsync(text);
				if (!bundle.Complete || RequireWorkspacePro())
				{
					await SnapshotAsync("导入配置前");
					await RestoreWorkspaceAsync(bundle);
					WorkspaceFeedback = "配置已导入；新电脑需保留原应用和文件路径，许可证不受影响";
				}
			}
		});
	}

	[RelayCommand]
	private async Task ApplyThemeSuiteAsync(ThemeSuite? suite)
	{
		if (suite == null || (suite.Pro && !RequireWorkspacePro()))
		{
			return;
		}
		await WorkspaceOperationAsync(async () =>
		{
			if (true)
			{
				await SnapshotAsync("应用主题前");
			}
			AppSettings appSettings = WorkspaceLibrary.Clone(Settings);
			appSettings.Theme = suite.Theme;
			appSettings.EnableAuroraBackground = false;
			appSettings.ThemePresetId = (suite.Pro ? suite.Id : string.Empty);
			bool flag = suite.Pro && suite.Id != "default";
			appSettings.IconShape = (flag ? IconShape.Rounded : IconShape.Original);
			appSettings.IconAutoTile = true;
			appSettings.IconPack = ((flag && IconPackService.Find("mac-tiles") != null) ? "mac-tiles" : "");
			appSettings.ClockLargeStyle = suite.Clock;
			appSettings.ClockStyle = ClockStyle.Classic;
			appSettings.ShowTopClock = true;
			appSettings.ClockLargeMode = suite.Pro;
			appSettings.EnableDock = true;
			appSettings.DockIconSize = ((suite.Id == "ink") ? 48 : 56);
			appSettings.EnableDockMagnification = true;
			appSettings.BackdropSource = (suite.Pro ? BackdropSource.CustomImage : BackdropSource.Wallpaper);
			appSettings.Backdrop = BackdropKind.Frosted;
			appSettings.CustomBackdropPath = (suite.Pro ? CreateSuiteBackground(suite) : string.Empty);
			CopyWorkspaceSettings(appSettings);
			await _storageService.SaveSettingsAsync(Settings);
			await RefreshWorkspaceLibraryAsync();
			WorkspaceFeedback = "已应用「" + suite.Name + "」，仍可自由调整每一项设置";
		});
	}

	private string CreateSuiteBackground(ThemeSuite suite)
	{
		string text = Path.Combine(_storageService.StorageDirectory, "theme-backgrounds");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, suite.WallpaperFile);
		using Stream stream = (Application.GetResourceStream(suite.WallpaperUri) ?? throw new FileNotFoundException("主题壁纸缺失", suite.WallpaperFile)).Stream;
		using FileStream destination = File.Create(text2);
		stream.CopyTo(destination);
		return text2;
	}

	private void OnRemovalSearchChanged(string value)
	{
		FilterRemovalChoices();
	}

	private void OnSettingsChanged(AppSettings value)
	{
		OnPropertyChanged("LaunchpadHotCornerOptions");
		OnPropertyChanged("DesktopDockHotCornerOptions");
		OnPropertyChanged("ResidentPinHotCornerOptions");
		OnPropertyChanged("SimpleClockMode");
		OnPropertyChanged("IsClockColorEditable");
		OnPropertyChanged("SimpleClockSize");
		OnPropertyChanged("SimpleGridLayout");
		OnPropertyChanged("SimpleGridDescription");
		value.PropertyChanged += OnSettingEdited;
		NotifyCustomBackdropChanged();
		OnPropertyChanged("SelectedIconPackId");
	}

	private void OnActiveSectionChanged(string value)
	{
		string text = value switch
		{
			"外观" => "外观与图标", 
			"时钟" => "小组件", 
			"应用" => "应用管理", 
			"专业版" => "账户", 
			_ => value, 
		};
		if (text != value)
		{
			ActiveSection = text;
			return;
		}
		if (ShowAdvancedSettings && value == "性能监控")
		{
			ActiveSection = "小组件";
			return;
		}
		if (!ShowAdvancedSettings)
		{
			string text2;
			switch (value)
			{
			case "程序坞":
			case "动效与手势":
				text2 = "通用";
				break;
			case "外观与图标":
			case "布局与网格":
				text2 = "外观";
				break;
			case "小组件":
			case "性能监控":
				text2 = "时钟";
				break;
			case "应用管理":
				text2 = "应用";
				break;
			case "专业版":
				text2 = "账户";
				break;
			default:
				text2 = value;
				break;
			}
			string text3 = text2;
			if (text3 != value)
			{
				ActiveSection = text3;
				return;
			}
		}
		OnPropertyChanged("ActiveSectionHint");
		bool flag;
		switch (value)
		{
		case "通用":
		case "专业版":
		case "账户":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			RefreshAccount();
			}
	}

	private void OnInviteInputChanged(string value)
	{
	}
}
