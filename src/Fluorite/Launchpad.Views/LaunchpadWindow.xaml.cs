using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Launchpad.Infrastructure;
using Launchpad.Interop;
using Launchpad.Models;
using Launchpad.Services;
using Launchpad.ViewModels;
using Microsoft.Win32;

namespace Launchpad.Views;

public partial class LaunchpadWindow : Window, IComponentConnector, IStyleConnector
{
	private struct DesktopWindowPosition
	{
		public nint Hwnd;

		public nint InsertAfter;

		public int X;

		public int Y;

		public int Width;

		public int Height;

		public uint Flags;
	}

	private sealed class DockFlight
	{
		public readonly TaskCompletionSource<Point> Done = new TaskCompletionSource<Point>();

		public required Point Start { get; init; }

		public required double StartScale { get; init; }

		public required int Index { get; init; }

		public required int Count { get; init; }

		public required int Token { get; init; }

		public required long StartTicks { get; init; }

		public int DurationMs { get; init; } = 280;
	}

	private sealed class DockSlot
	{
		public double Slide;

		public double SlideVel;

		public double Pop = 1.0;

		public double PopVel;

		public double PopS = 0.32;

		public double Gap;

		public double GapVel;

		public double GapTo;

		public FrameworkElement Element { get; }

		public int Item { get; }

		public ScaleTransform Scale { get; }

		public TranslateTransform Shift { get; }

		public DockSlot(FrameworkElement element, int item, ScaleTransform scale, TranslateTransform shift)
		{
			Element = element;
			Item = item;
			Scale = scale;
			Shift = shift;
		}
	}

	private bool _residentLaunchpadPinned;

	private bool _residentPinRequested;

	private readonly HotkeyService _residentLayerHotkey = new HotkeyService(9002);

	private HotCornerService? _residentPinCornerService;

	private DispatcherTimer? _desktopResidentTimer;

	private DispatcherTimer? _residentDockTaskbarTimer;

	private double _residentDockTaskbarOffset;

	private bool _wasDesktopResident;

	private AppSettings? _residentSettings;

	private nint _residentDesktop;

	private const int FolderColumns = 3;

	private const double FolderPaddingX = 24.0;

	private const double CardGutterX = 22.0;

	private bool _musicPointerGesture;

	private const int GridHoleMs = 180;

	private const int DockBandReleaseMs = 300;

	private const double DockBandReleaseBounce = 0.1;

	private const int DockBandRowStepMs = 20;

	private const int DockPlateSinkMs = 320;

	private const int DockPlateFadeMs = 230;

	private const int DockPlateFadeDelayMs = 170;

	private DispatcherTimer? _edgePagingTimer;

	private long _edgePageCooldownUntil;

	private static readonly Geometry CreateGlyph = Freeze(Geometry.Parse("M10.6,4.6 H13.4 V10.6 H19.4 V13.4 H13.4 V19.4 H10.6 V13.4 H4.6 V10.6 H10.6 Z"));

	private static readonly Geometry JoinGlyph = Freeze(Geometry.Parse("M8.6,4.8 L16,12 L8.6,19.2 L6.4,17 L11.3,12 L6.4,7 Z"));

	private static readonly Geometry ArrowDown = Freeze(Geometry.Parse("M0,0 H14 L7,7 Z"));

	private static readonly Geometry ArrowUp = Freeze(Geometry.Parse("M7,0 L14,7 H0 Z"));

	private const double DockMagnifyRangeSlots = 2.4;

	private const double DockHitSlack = 12.0;

	private const double DockFollowStrengthS = 0.16;

	private const double DockFollowCentreS = 0.045;

	private const double DockFollowRearrangeS = 0.32;

	private const double DockFollowLandS = 0.32;

	private const double DockFollowGapS = 0.28;

	private int _dockGapSlot = -1;

	private int _dockGapRoom;

	private double _dockLiveGapRoom;

	private double _dockLiveGapRoomVel;

	private int _dockGapLawRoom;

	private int _dockGapLawCount;

	private int _landProbeIndex = -1;

	private int _landProbeFrames;

	private int _flyProbeFrames;

	private int _dockGhostSlot = -1;

	private bool _dockShelfArmed;

	private bool _dockShelfLit;

	private bool _dockPlateRetiring;

	private bool _dockBarWasUp;

	private AppItem? _dockLastSingle;

	private double _dockBandPending;

	private bool _dockBandOwnedByGesture;

	private DispatcherTimer? _dockSizeTimer;

	private readonly Stopwatch _dockSizeClock = new Stopwatch();

	private double _dockSizeFrom;

	private double _dockSizeTo;

	private bool _dockSizeLayoutQueued;

	private bool _dockProbeOn;

	private bool _dockProbeHooked;

	private int _dockPendingFrames;

	private int _dockPendingMaxUnplaced;

	private TimeSpan _dockProbeLastTime;

	private bool _autoSizedOpenApps;

	private const double DockShelfBottomInset = 10.0;

	private const double DockPlateOuterRadius = 20.0;

	private const double DockPlateInnerRadius = 19.0;

	private const double DockPlateEdgeRadius = 19.5;

	private double _dockPlateScaleWritten;

	private const double DockTileFloor = 14.0;

	private const double DockTipGap = 8.0;

	private const int DockFlyMs = 280;

	private const double SettleFollowRatio = 1.3;

	private const double SettleFloorSpeed = 900.0;

	private const double SettleMinMs = 230.0;

	private const double SettleMaxMs = 650.0;

	private const double SettleMaxSlope = 3.0;

	private const double PagingBounce = 0.08;

	private const double PagingDecay = 4.5;

	private const double DemoReleaseSpeed = 2900.0;

	private long _stallTicks;

	private BitmapCache? _slideCache;

	private double _slideCacheScale;

	private string? _probePhase;

	private long _probeLastTicks;

	private int _probeFrames;

	private int _probeDrops;

	private double _probeWorstGap;

	private volatile bool _probeRunning;

	private readonly FrameMetrics _phaseMetrics = new FrameMetrics();

	private readonly FrameMetrics _motionMetrics = new FrameMetrics();

	private long _probeStartTicks;

	private double _probeWorstAt;

	private readonly List<string> _probeGaps = new List<string>();

	private Stopwatch? _openWatch;

	private double _openFirstTickMs;

	private readonly double[] _openStepTimings = new double[3];

	private readonly ConcurrentQueue<string> _uiBlocks = new ConcurrentQueue<string>();

	private int _watchdogRunning;

	private int _watchdogPending;

	private volatile bool _windowClosed;

	private volatile string _watchdogPhase = "idle";

	private DispatcherTimer? _idleSettleTimer;

	private DispatcherTimer? _warmTimer;

	private int _warmSlot = -1;

	private int _warmPage = -1;

	private int _warmItemIndex;

	private const int WM_MOUSEHWHEEL = 526;

	private readonly MainViewModel _viewModel;

	private DockWindowPreview? _dockWindowPreview;

	private readonly SettingsViewModel _settingsVm;

	private bool _isWindowAnimating;

	private bool _isPageAnimating;

	private bool _isAppLaunching;

	private DispatcherTimer? _searchIdleTimer;

	private bool _isItemDragging;

	private bool _isMouseDownOnApp;

	private AppItem? _pressedApp;

	private FrameworkElement? _pressedElement;

	private Point _appMouseDownPoint;

	private AppItem? _draggedApp;

	private int _draggedSlotIndex = -1;

	private FrameworkElement? _draggedSlotContainer;

	private Border? _dragDimmedLayer;

	private long _lastDragEndTimeTicks;

	private long _lastShiftTimeTicks;

	private int _lastShiftFromIndex = -1;

	private long _edgeHoverStartTicks;

	private int _pendingEdgeDirection;

	private int _dragCrossTargetPage = -1;

	private int _dragCrossDirection;

	private int _dragCrossOutgoingSlot = -1;

	private Point _dragStartPoint;

	private Point _lastMovePoint;

	private long _lastMoveTimeTicks;

	private double _velocityTrackerX;

	private double _peakSpeed;

	private long _peakSpeedTicks;

	private const double VelocityTauMs = 45.0;

	private const long FlingPeakWindowMs = 110L;

	private const double SwipeStartThreshold = 3.0;

	private const double FlingTransferSeconds = 0.13;

	private bool _isPointerDown;

	private bool _isSwiping;

	private int _swipeTargetPage = -1;

	private bool _chainRolledThisGesture;

	private bool _isReturning;

	private double _swipeDeviceScale = 1.0;

	private long _lastFrameTicks;

	private int _swipeFrameCount;

	private double _swipeFrameGapTotal;

	private double _swipeFrameGapWorst;

	private int _swipeDroppedFrames;

	private int _swipeMoveCount;

	private long _swipeStartTicks;

	private long _phaseStartRenderTicks;

	private double _swipeFrameGapWorstAt;

	private readonly List<string> _swipeGaps = new List<string>();

	private readonly WheelPageGesture _wheelGesture = new WheelPageGesture();

	private long _lastSwipeEndTimeTicks;

	private Border[] _layers = Array.Empty<Border>();

	private TranslateTransform[] _layerTranslates = Array.Empty<TranslateTransform>();

	private ItemsControl[] _layerItems = Array.Empty<ItemsControl>();

	private readonly int[] _layerPage = new int[4] { -1, -1, -1, -1 };

	private int _reserveIdx = 3;

	private int _primaryIdx = -1;

	private int _leftIdx = -1;

	private int _rightIdx = -1;

	private int _neighbourIdx = -1;

	private Border _primaryLayer;

	private TranslateTransform _primaryTranslate;

	private ItemsControl _primaryItems;

	private TranslateTransform _neighbourTranslate;

	private bool _layersReady;

	private int _lastTravelDirection = 1;

	private bool _warmScheduled;

	private int _warmRetries;

	private bool _renderHooked;

	private bool _dragCardDirty;

	private double? _pendingIndicatorProgress;

	private double _dragCardGrabX;

	private double _dragCardGrabY;

	private int _dragLandingToken;

	private double _pendingCardX;

	private double _pendingCardY;

	private int _currentTransitionTarget = -1;

	private int _currentTransitionDirection;

	private int _queuedTransitionTarget = -1;

	private double _slideFromPX;

	private double _slideToPX;

	private double _slideFromTX;

	private double _slideToTX;

	private double _slideDurationMs = 280.0;

	private IEasingFunction _slideEase = new SpringEase();

	private double _slideDistance;

	private long _slideStartTicks;

	private int _slideSeq;

	private bool _motionDriven;

	private bool _liftReleasePending;

	private int _motionSeq;

	private long _motionStartSw;

	private double _motionDurationMs = 1.0;

	private double _motionFromPX;

	private double _motionToPX;

	private double _motionFromTX;

	private double? _motionToTX;

	private IEasingFunction? _motionEase;

	private Action? _motionLanded;

	private TranslateTransform? _motionPrimary;

	private TranslateTransform? _motionNeighbour;

	private int _motionFrames;

	private int _motionDistinctOffsets;

	private int _motionSubPixelWrites;

	private long _motionLastDevOffset = long.MinValue;

	private double? _motionPingAmp;

	private double _motionSamplePx;

	private long _motionSampleSw;

	private double _motionVisualSpeed;

	private const double MergeDwellMs = 150.0;

	private const double MergeStillRadius = 5.0;

	private AppItem? _mergeTarget;

	private AppItem? _mergeCandidate;

	private long _mergeCandidateStartTicks;

	private Point _mergeAnchorPos;

	private FrameworkElement? _mergePulsedElement;

	private AppItem? _foldHintTarget;

	private Point _dragCursorPos;

	private bool _clickDismissedFolder;

	private bool _pressedFromDock;

	private bool _dragFromDock;

	private int _gridHoleIndex = -1;

	private AppItem? _gridHoleEjected;

	private bool? _probeOverDock;

	private bool _dockCarryHover;

	private DockFlight? _dockFly;

	private bool _dockFlyHooked;

	private readonly List<DockSlot> _dockSlots = new List<DockSlot>();

	private readonly Dictionary<FrameworkElement, DockSlot> _dockSlotsByElement = new Dictionary<FrameworkElement, DockSlot>();

	private bool _dockDriving;

	private string _dockHintLabel = "";

	private bool _dockLayoutQueued;

	private bool _suppressDockReflow;

	private double _dockTargetStrength;

	private double _dockTargetX;

	private double _dockLiveStrength;

	private double _dockLiveStrengthVel;

	private double _dockLiveX;

	private double _dockLiveXVel;

	private long _dockLiveTicks;

	private bool _dockRenderHooked;

	private Dictionary<AppItem, double> _dockPlacedLeft = new Dictionary<AppItem, double>();

	private AppItem? _dockTipApp;

	private double _dockTipLiveX;

	private double _dockTipXVel;

	private bool _frostTrackHooked;

	private long _frostTrackUntilTicks;

	private BitmapSource? _dockFrostSource;

	private BitmapSource? _dockFrostCapture;

	private const int DockFrostDownscale = 4;

	private const int DockFrostRadius = 5;

	private const double DockFrostTintOpacity = 0.3;

	private const int SheetFrostDownscale = 4;

	private const int SheetFrostRadius = 5;

	private const double FolderFrostCaptureScale = 0.25;

	private const int FolderFrostDownscale = 1;

	private const int FolderFrostRadius = 6;

	private Rect? _folderSourceRect;

	private bool _blocksDeactivation;

	private Window? _activeOwnedDialog;

	private int _openMenus;

	private long _menuClosedTicks;

	private ItemsControl? _dragHost;

	private static nint _selfHwnd;

	private bool _searchOnSecondRow;

	private bool _isPreWarming;

	private bool _isPreWarmed;

	private bool _firstFramePrepared;

	private bool _startupReady = true;

	private Task? _startupPreparation;

	private TaskCompletionSource? _startupSceneReady;

	private bool _pendingStartupSettings;

	private string? _pendingStartupSection;

	private ImageBrush? _windowBaseBrush;

	private ImageSource? _windowBaseSource;

	private string _backdropKey = string.Empty;

	private DispatcherTimer? _backdropTimer;

	private const int OpenDurationMs = 220;

	private const double OpenScale = 0.985;

	private const double OpenTravelDip = 10.0;

	private const double CloseLayerMs = 300.0;

	private const double CloseScaleX = 0.34;

	private const double CloseScaleY = 0.06;

	private const double CloseTravelFrac = 0.8;

	private const double CloseDockOverrunPx = 24.0;

	private const double DockEntranceDip = 44.0;

	private const double CloseCardFrac = 0.3;

	private const double CloseCardMs = 220.0;

	private const double CloseCardFadeMs = 200.0;

	private const double CloseCardStaggerMs = 6.0;

	private const double CloseWindowFadeAtMs = 200.0;

	private long _animatingStartTicks;

	private double _openMotionAtMs;

	private bool _capturingHotkey;

	private bool _settingsEscapeHeld;

	private bool _settingsLoaded;

	private AppSettings? _liveSettingsSource;

	private DispatcherTimer? _previewTimer;

	private DispatcherTimer? _previewReturnTimer;

	private int _previewReturnPage = -1;

	private bool _sheetSelectionAttached;

	private const double SheetStartScale = 0.06;

	private Transform? _sheetFlightTransform;

	private readonly BlurEffect _surfaceBlur = new BlurEffect
	{
		Radius = 0.0,
		KernelType = KernelType.Gaussian
	};

	private const double SurfaceBlurRadius = 22.0;

	private const int SurfaceBlurSamples = 10;

	private static IDropTargetHelper? _dropTargetHelper = TryCreateDropTargetHelper();

	private bool _isExternalFileDragging;

	private string[]? _cachedDropFiles;

	private FrameworkElement? _currentDropCardElement;

	private Rect _currentDropCardBounds;

	private AppItem? _currentDropApp;

	private ScaleTransform? _dropHighlightScale;

	private FrameworkElement? _highlightedElement;

	private DispatcherTimer? _folderHoverTimer;

	private AppItem? _hoveredFolder;

	private bool _folderTransferInProgress;

	private bool DesktopResidentSurface
	{
		get
		{
			if (_viewModel.Settings.DesktopResidentLaunchpad && !_viewModel.IsResidentLaunchpadDismissed && !_residentLaunchpadPinned && !IsSettingsOpen && !_blocksDeactivation)
			{
				return !(_activeOwnedDialog?.IsVisible ?? false);
			}
			return false;
		}
	}

	private double DockMagnifyMax
	{
		get
		{
			if (!(_viewModel.Settings.DockMagnifyScale > 1.0))
			{
				return 1.85;
			}
			return _viewModel.Settings.DockMagnifyScale;
		}
	}

	private bool DockGapArmed => _dockGapSlot >= 0;

	private double DockMagnifyReach => _viewModel.DockSlot * (DockMagnifyMax - 1.0);

	internal BitmapSource? ClockFrostSource
	{
		get
		{
			if (_viewModel.Settings.Backdrop != BackdropKind.Solid)
			{
				if (_dockFrostCapture != BackdropImage.Source)
				{
					return null;
				}
				return _dockFrostSource;
			}
			return null;
		}
	}

	private double TempoScale => _viewModel.Settings.MotionTempo switch
	{
		MotionTempo.Instant => 0.0, 
		MotionTempo.Fast => 0.62, 
		MotionTempo.Relaxed => 1.45, 
		_ => 1.0, 
	};

	private EasingFunctionBase? CurveOverride => _viewModel.Settings.MotionCurve switch
	{
		MotionCurve.EaseOut => (EasingFunctionBase?)new QuinticEase
		{
			EasingMode = EasingMode.EaseOut
		}, 
		MotionCurve.Linear => LinearEase.Instance, 
		_ => null, 
	};

	private double CurvePeak
	{
		get
		{
			if (_viewModel.Settings.MotionCurve == MotionCurve.Linear)
			{
				return 1.15;
			}
			return 5.0;
		}
	}

	private bool PageMotionActive
	{
		get
		{
			if (!_isSwiping && !_isPageAnimating)
			{
				return _isReturning;
			}
			return true;
		}
	}

	internal bool NavigationPending
	{
		get
		{
			if (!PageMotionActive)
			{
				return _queuedTransitionTarget >= 1;
			}
			return true;
		}
	}

	internal ItemsControl NextPageItems => _layerItems[_rightIdx];

	private double PageCommitRatio => Math.Clamp(_viewModel.Settings.PageCommitPercent, 2.0, 60.0) / 100.0;

	internal bool CanPresentModal
	{
		get
		{
			if (_startupReady && !_isPreWarming && !_isWindowAnimating && !_isSwiping)
			{
				return _activeOwnedDialog == null;
			}
			return false;
		}
	}

	public bool PinOpen { get; set; }

	public bool HasStartupOpenRequest { get; private set; }

	internal bool StartupEnvironmentReady => _startupReady;

	internal bool FirstFramePrepared => _firstFramePrepared;

	private bool IsSettingsOpen => SettingsOverlayModal.Visibility == Visibility.Visible;

	internal event EventHandler? ClockBackdropChanged;

	private void OnMenuToggleLaunchpadLayer(object sender, RoutedEventArgs e)
	{
		SetLaunchpadDesktopLayer(_residentLaunchpadPinned);
	}

	private void OnBackgroundMenuOpened(object sender, RoutedEventArgs e)
	{
		if (!(sender is ContextMenu contextMenu) || !(contextMenu.Items[1] is MenuItem menuItem))
		{
			return;
		}
		menuItem.Header = (_residentLaunchpadPinned ? "贴在桌面" : "置顶启动台");
		menuItem.ToolTip = (_residentLaunchpadPinned ? "将启动台放回其他窗口后面，保留在桌面上；再次切换可置顶操作。" : "将启动台显示在其他窗口前面，方便操作；再次切换可贴回桌面。");
		foreach (object item in (IEnumerable)contextMenu.Items)
		{
			if (item is MenuItem { ToolTip: not null } menuItem2)
			{
				if (!(menuItem2.ToolTip is ToolTip))
				{
					menuItem2.ToolTip = new ToolTip
					{
						Content = menuItem2.ToolTip,
						IsHitTestVisible = false,
						Placement = PlacementMode.Right,
						PlacementTarget = menuItem2
					};
				}
				menuItem2.MouseLeave -= OnBackgroundMenuItemLeave;
				menuItem2.MouseLeave += OnBackgroundMenuItemLeave;
				menuItem2.ToolTipOpening -= OnBackgroundMenuItemToolTipOpening;
				menuItem2.ToolTipOpening += OnBackgroundMenuItemToolTipOpening;
			}
		}
	}

	private void OnBackgroundMenuItemLeave(object sender, MouseEventArgs e)
	{
		if (sender is MenuItem { ToolTip: ToolTip toolTip })
		{
			toolTip.IsOpen = false;
		}
	}

	private void OnBackgroundMenuItemToolTipOpening(object sender, ToolTipEventArgs e)
	{
		if (sender is MenuItem { IsMouseOver: false })
		{
			e.Handled = true;
		}
	}

	private void OnBackgroundMenuClosed(object sender, RoutedEventArgs e)
	{
		if (!(sender is ContextMenu contextMenu))
		{
			return;
		}
		foreach (object item in (IEnumerable)contextMenu.Items)
		{
			if (item is MenuItem { ToolTip: ToolTip toolTip })
			{
				toolTip.IsOpen = false;
			}
		}
	}

	private bool TryAutoUnpinResidentLaunchpad()
	{
		if (_viewModel.Settings.DesktopResidentLaunchpad && _viewModel.Settings.AutoUnpinResidentLaunchpad && _residentLaunchpadPinned && !IsSettingsOpen)
		{
			Window activeOwnedDialog = _activeOwnedDialog;
			if (activeOwnedDialog == null || !activeOwnedDialog.IsVisible)
			{
				if (_viewModel.IsFolderOpen)
				{
					_viewModel.CloseFolder();
				}
				SetLaunchpadDesktopLayer(desktop: true);
				return true;
			}
		}
		return false;
	}

	private void OnResidentAppLaunchSucceeded()
	{
		TryAutoUnpinResidentLaunchpad();
	}

	private void PinResidentLaunchpadFromCorner()
	{
		if (!_viewModel.Settings.DesktopResidentLaunchpad || !_viewModel.Settings.EnableResidentPinHotCorner || _residentLaunchpadPinned || IsSettingsOpen || _capturingHotkey || _settingsVm.IsCapturingHotkey || _isItemDragging)
		{
			return;
		}
		Window activeOwnedDialog = _activeOwnedDialog;
		if (activeOwnedDialog == null || !activeOwnedDialog.IsVisible)
		{
			if (!_viewModel.IsOpen)
			{
				_residentPinRequested = true;
				_viewModel.OpenLaunchpad();
			}
			SetLaunchpadDesktopLayer(desktop: false);
			Focus();
		}
	}

	private void ToggleResidentLayerFromHotkey()
	{
		if (!_viewModel.Settings.DesktopResidentLaunchpad || _capturingHotkey || _settingsVm.IsCapturingHotkey || _isItemDragging)
		{
			return;
		}
		Window activeOwnedDialog = _activeOwnedDialog;
		if (activeOwnedDialog == null || !activeOwnedDialog.IsVisible)
		{
			if (!_viewModel.IsOpen)
			{
				_residentPinRequested = true;
				_viewModel.OpenLaunchpad();
				SetLaunchpadDesktopLayer(desktop: false);
			}
			else
			{
				SetLaunchpadDesktopLayer(_residentLaunchpadPinned);
			}
		}
	}

	private void UpdateResidentLayerHotkey()
	{
		if (!_viewModel.Settings.DesktopResidentLaunchpad)
		{
			_residentLayerHotkey.Unregister();
		}
		else if (!_residentLayerHotkey.IsRegistered)
		{
			nint handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero && !_residentLayerHotkey.Register(handle, 6u, (uint)KeyInterop.VirtualKeyFromKey(Key.T), out string errorMessage))
			{
				_viewModel.StatusMessage = "Ctrl+Shift+T 全局快捷键注册失败，可能已被其他软件占用";
				App.Log("[HOTKEY] Resident layer registration failed: " + errorMessage);
			}
		}
	}

	private void SetLaunchpadDesktopLayer(bool desktop)
	{
		if (_viewModel.Settings.DesktopResidentLaunchpad)
		{
			_residentLaunchpadPinned = !desktop;
			UpdateDesktopResidentMode();
			if (!desktop && !IsSettingsOpen)
			{
				EnsureForeground("pin-launchpad");
			}
		}
	}

	private void UpdateResidentDockTaskbar()
	{
		double pageNumber = 0.0;
		if (DesktopResidentSurface && IsVisible && _viewModel.IsOpen)
		{
			DpiScale dpi = VisualTreeHelper.GetDpi(this);
			double settingIndex = (Top + ActualHeight) * dpi.DpiScaleY;
			double num3 = Left * dpi.DpiScaleX;
			double num4 = (Left + ActualWidth) * dpi.DpiScaleX;
			string[] entries = new string[2] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
			foreach (string className in entries)
			{
				nint num5 = IntPtr.Zero;
				while ((num5 = FindWindowEx(IntPtr.Zero, num5, className, null)) != IntPtr.Zero)
				{
					if (NativeMethods.GetWindowRect(num5, out var lpRect) && !((double)lpRect.Right <= num3) && !((double)lpRect.Left >= num4) && !((double)lpRect.Top < settingIndex - 200.0 * dpi.DpiScaleY) && !((double)lpRect.Bottom < settingIndex - 4.0 * dpi.DpiScaleY))
					{
						double num6 = Math.Max(0.0, settingIndex - (double)lpRect.Top) / dpi.DpiScaleY;
						if (num6 > 4.0)
						{
							pageNumber = Math.Max(pageNumber, num6);
						}
					}
				}
			}
		}
		double num7 = 0.0 - pageNumber;
		if (!(Math.Abs(num7 - _residentDockTaskbarOffset) < 0.5))
		{
			_residentDockTaskbarOffset = num7;
			DoubleAnimation animation = new DoubleAnimation(ResidentDockTaskbarTranslate.Y, num7, TimeSpan.FromMilliseconds(90.0))
			{
				FillBehavior = FillBehavior.Stop
			};
			ResidentDockTaskbarTranslate.Y = num7;
			ResidentDockTaskbarTranslate.BeginAnimation(TranslateTransform.YProperty, animation);
		}
	}

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hwnd, uint command);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

	private nint ResidentDesktopWindow()
	{
		if (_residentDesktop == IntPtr.Zero || FindWindowEx(_residentDesktop, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
		{
			_residentDesktop = DesktopDockWindow.FindDesktopWindow();
		}
		return _residentDesktop;
	}

	private void ObserveResidentSettings()
	{
		_residentLaunchpadPinned = false;
		if (_residentSettings != null)
		{
			_residentSettings.PropertyChanged -= OnResidentSettingChanged;
		}
		_residentSettings = _viewModel.Settings;
		_residentSettings.PropertyChanged += OnResidentSettingChanged;
		UpdateResidentLayerHotkey();
		if (_residentSettings.DesktopResidentLaunchpad && !_viewModel.IsResidentLaunchpadDismissed)
		{
			_desktopResidentTimer.Start();
		}
		else
		{
			_desktopResidentTimer.Stop();
		}
		if (_residentSettings.DesktopResidentLaunchpad && !_viewModel.IsResidentLaunchpadDismissed)
		{
			_residentDockTaskbarTimer.Start();
		}
		else
		{
			_residentDockTaskbarTimer.Stop();
		}
		UpdateResidentDockTaskbar();
		UpdateDesktopResidentMode();
	}

	private void OnResidentSettingChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "DesktopResidentLaunchpad")
		{
			if (_viewModel.Settings.DesktopResidentLaunchpad)
			{
				_viewModel.OpenLaunchpad();
			}
			ObserveResidentSettings();
		}
	}

	private void OnResidentModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Settings")
		{
			ObserveResidentSettings();
		}
		else if (e.PropertyName == "IsOpen")
		{
			if (_viewModel.IsOpen && _viewModel.Settings.DesktopResidentLaunchpad)
			{
				_desktopResidentTimer.Start();
				_residentDockTaskbarTimer.Start();
				UpdateDesktopResidentMode();
			}
			else
			{
				_desktopResidentTimer.Stop();
				_residentDockTaskbarTimer.Stop();
			}
		}
	}

	private void InitializeDesktopResidentMode()
	{
		_residentPinCornerService = new HotCornerService(() => _viewModel.Settings.DesktopResidentLaunchpad && _viewModel.Settings.EnableResidentPinHotCorner, () => _viewModel.Settings.ResidentPinHotCorner, () => _viewModel.Settings.PauseHotCornerInFullscreen, () => _viewModel.AllApps);
		_residentPinCornerService.CornerTriggered += PinResidentLaunchpadFromCorner;
		_residentLayerHotkey.HotkeyPressed += ToggleResidentLayerFromHotkey;
		_viewModel.AppLaunchSucceeded += OnResidentAppLaunchSucceeded;
		SourceInitialized += (object? _, EventArgs _) =>
		{
			UpdateResidentLayerHotkey();
		};
		_residentDockTaskbarTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(50.0)
		};
		_residentDockTaskbarTimer.Tick += (object? _, EventArgs _) =>
		{
			UpdateResidentDockTaskbar();
		};
		_desktopResidentTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_desktopResidentTimer.Tick += (object? _, EventArgs _) =>
		{
			UpdateDesktopResidentMode();
		};
		_viewModel.PropertyChanged += OnResidentModelChanged;
		ObserveResidentSettings();
		Closed += (object? _, EventArgs _) =>
		{
			_residentLayerHotkey.Dispose();
			_residentPinCornerService.Dispose();
			_viewModel.AppLaunchSucceeded -= OnResidentAppLaunchSucceeded;
			_desktopResidentTimer.Stop();
			_residentDockTaskbarTimer.Stop();
			_viewModel.PropertyChanged -= OnResidentModelChanged;
			if (_residentSettings != null)
			{
				_residentSettings.PropertyChanged -= OnResidentSettingChanged;
			}
		};
	}

	private void UpdateDesktopResidentMode()
	{
		UpdateResidentDockTaskbar();
		if (!_startupReady || _isPreWarming)
		{
			return;
		}
		bool desktopResidentLaunchpad = _viewModel.Settings.DesktopResidentLaunchpad;
		if (_wasDesktopResident && !desktopResidentLaunchpad)
		{
			ShowActivated = true;
			Topmost = true;
		}
		_wasDesktopResident = desktopResidentLaunchpad;
		if (desktopResidentLaunchpad && _viewModel.IsResidentLaunchpadDismissed)
		{
			return;
		}
		if (IsSettingsOpen)
		{
			ShowActivated = true;
			Topmost = true;
		}
		else
		{
			if (!desktopResidentLaunchpad)
			{
				return;
			}
			if (_residentLaunchpadPinned)
			{
				ShowActivated = true;
				Topmost = true;
				return;
			}
			ShowActivated = false;
			if (!_viewModel.IsOpen)
			{
				_viewModel.OpenLaunchpad();
			}
			if (!DesktopResidentSurface)
			{
				return;
			}
			Topmost = false;
			nint pageNumber = ResidentDesktopWindow();
			if (pageNumber != IntPtr.Zero)
			{
				nint window = GetWindow(pageNumber, 3u);
				if (window != _selfHwnd)
				{
					SetWindowPos(_selfHwnd, window, 0, 0, 0, 0, 19u);
				}
			}
		}
	}

	private void KeepDesktopResidentZOrder(nint position)
	{
		if (!DesktopResidentSurface || !_startupReady || _isPreWarming || position == IntPtr.Zero)
		{
			return;
		}
		nint pageNumber = ResidentDesktopWindow();
		if (pageNumber == IntPtr.Zero)
		{
			return;
		}
		DesktopWindowPosition structure = Marshal.PtrToStructure<DesktopWindowPosition>(position);
		if ((structure.Flags & 4) == 0)
		{
			nint window = GetWindow(pageNumber, 3u);
			if (window == _selfHwnd)
			{
				structure.Flags |= 4u;
			}
			else
			{
				structure.InsertAfter = window;
			}
			Marshal.StructureToPtr(structure, position, fDeleteOld: false);
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "DockIconSize" || propertyName == "DockGap") ? true : false)
		{
			QueueDockSizeLayout();
		}
		if (e.PropertyName == "IsPageSwiping" && _viewModel.IsPageSwiping)
		{
			DismissEmptySearchForPaging();
		}
		if (e.PropertyName == "IsFolderOpen")
		{
			if (_viewModel.IsFolderOpen)
			{
				PlayFolderZoomIn();
			}
			else
			{
				PlayFolderZoomOut();
			}
			return;
		}
		if (e.PropertyName == "IsDockVisible")
		{
			bool dockBarWasUp = _dockBarWasUp;
			bool isDockVisible = _viewModel.IsDockVisible;
			if (dockBarWasUp && !isDockVisible && _viewModel.DockApps.Count == 0)
			{
				_dockBandPending = _viewModel.DockBarHeight + 64.0 - _viewModel.Settings.ViewportMarginTop * 0.5;
				RetireDockPlate(_dockBandOwnedByGesture ? null : _dockLastSingle);
				App.Log($"[DBG][DOCK-EXIT] band={_dockBandPending:F0} rows={_viewModel.Rows} cols={_viewModel.Columns} owned={_dockBandOwnedByGesture}");
			}
			_dockBarWasUp = isDockVisible;
			_dockLastSingle = ((_viewModel.DockApps.Count == 1) ? _viewModel.DockApps[0] : null);
		}
		else if (e.PropertyName == "ViewportMargin" && _dockBandPending > 0.0 && !_dockBandOwnedByGesture)
		{
			double dockBandPending = _dockBandPending;
			_dockBandPending = 0.0;
			PageViewport.UpdateLayout();
			ReleaseDockBand(dockBandPending);
		}
		if (e.PropertyName == "Settings")
		{
			ThemeManager.Apply(_viewModel.Settings.Theme);
			RebuildBackdrop();
		}
	}

	private Rect? TryGetFolderCardRect(AppItem? folder)
	{
		if (folder == null)
		{
			return null;
		}
		int pageNumber = _viewModel.CurrentPageApps.IndexOf(folder);
		if (pageNumber < 0)
		{
			return null;
		}
		FrameworkElement frameworkElement = ((!(_primaryItems.ItemContainerGenerator.ContainerFromIndex(pageNumber) is FrameworkElement frameworkElement2)) ? null : (FindVisualChild<Border>(frameworkElement2) ?? frameworkElement2));
		if (frameworkElement == null || frameworkElement.ActualWidth <= 0.0 || frameworkElement.ActualHeight <= 0.0)
		{
			_primaryItems.UpdateLayout();
			frameworkElement = ((!(_primaryItems.ItemContainerGenerator.ContainerFromIndex(pageNumber) is FrameworkElement frameworkElement3)) ? null : (FindVisualChild<Border>(frameworkElement3) ?? frameworkElement3));
			if (frameworkElement == null || frameworkElement.ActualWidth <= 0.0 || frameworkElement.ActualHeight <= 0.0)
			{
				return null;
			}
		}
		return new Rect(frameworkElement.TranslatePoint(new Point(0.0, 0.0), this), new Size(frameworkElement.ActualWidth, frameworkElement.ActualHeight));
	}

	private void ResetFolderPopupTransform()
	{
		FolderPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		FolderPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		FolderPopupTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		FolderPopupTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		FolderOverlayScrim.BeginAnimation(UIElement.OpacityProperty, null);
		FolderPopupScale.ScaleX = 1.0;
		FolderPopupScale.ScaleY = 1.0;
		FolderPopupTranslate.X = 0.0;
		FolderPopupTranslate.Y = 0.0;
		FolderOverlayScrim.Opacity = 1.0;
	}

	private void LayoutFolderCard()
	{
		int valueOrDefault = (_viewModel.ActiveFolder?.Children?.Count).GetValueOrDefault();
		int val = Math.Clamp(valueOrDefault, 1, 3);
		int pageNumber = (int)Math.Ceiling((double)valueOrDefault / 3.0);
		double settingIndex = (double)Math.Max(val, 2) * (_viewModel.CardWidth + 22.0) + 16.0;
		double num3 = _viewModel.CardHeight + 12.0;
		if ((double)pageNumber * num3 + 60.0 > FolderPopup.MaxHeight)
		{
			settingIndex += SystemParameters.VerticalScrollBarWidth;
		}
		FolderItemsControl.Width = settingIndex;
		FolderPopup.Width = settingIndex + 48.0;
	}

	private void UpdateFolderFrost()
	{
		if (FolderFrostLayer == null || FolderTintLayer == null)
		{
			return;
		}
		if (_viewModel.Settings.Backdrop == BackdropKind.Solid || ThemeManager.Effective == AppTheme.HighContrast || FolderPopup.ActualWidth < 1.0 || FolderPopup.ActualHeight < 1.0)
		{
			FolderFrostLayer.Visibility = Visibility.Collapsed;
			FolderTintLayer.Visibility = Visibility.Collapsed;
			FolderPopup.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
			return;
		}
		FolderPopup.Background = Brushes.Transparent;
		FolderTintLayer.Visibility = Visibility.Visible;
		if (Resources["FolderFrost"] is ImageBrush imageBrush)
		{
			BitmapSource bitmapSource = _dockFrostSource ?? (BackdropImage.Source as BitmapSource);
			if (bitmapSource == null || ActualWidth < 1.0 || ActualHeight < 1.0)
			{
				FolderFrostLayer.Visibility = Visibility.Collapsed;
				return;
			}
			double pageNumber = ((FolderOverlayModal.ActualWidth > 0.0) ? FolderOverlayModal.ActualWidth : ActualWidth);
			double settingIndex = ((FolderOverlayModal.ActualHeight > 0.0) ? FolderOverlayModal.ActualHeight : ActualHeight);
			Rect rect = new Rect((pageNumber - FolderPopup.ActualWidth) / 2.0, (settingIndex - FolderPopup.ActualHeight) / 2.0, FolderPopup.ActualWidth, FolderPopup.ActualHeight);
			double num3 = bitmapSource.Width / ActualWidth;
			double num4 = bitmapSource.Height / ActualHeight;
			imageBrush.ImageSource = bitmapSource;
			imageBrush.Viewbox = new Rect(rect.X * num3, rect.Y * num4, FolderPopup.ActualWidth * num3, FolderPopup.ActualHeight * num4);
			imageBrush.Viewport = new Rect(0.0, 0.0, FolderPopup.ActualWidth, FolderPopup.ActualHeight);
			FolderFrostLayer.Visibility = Visibility.Visible;
			App.Log($"[FOLDER-FROST] mapped {rect.Width:F0}x{rect.Height:F0} from {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");
		}
	}

	private void ComputeFolderZoomStart(out double scale, out double tx, out double ty)
	{
		scale = 0.55;
		tx = 0.0;
		ty = 0.0;
		Rect? folderSourceRect = _folderSourceRect;
		if (folderSourceRect.HasValue)
		{
			Rect valueOrDefault = folderSourceRect.GetValueOrDefault();
			if (valueOrDefault.Width > 0.0 && FolderPopup.ActualWidth > 0.0)
			{
				scale = Math.Clamp(valueOrDefault.Width / FolderPopup.ActualWidth, 0.06, 0.9);
				Point point = FolderPopup.TranslatePoint(new Point(FolderPopup.ActualWidth / 2.0, FolderPopup.ActualHeight / 2.0), this);
				tx = valueOrDefault.X + valueOrDefault.Width / 2.0 - point.X;
				ty = valueOrDefault.Y + valueOrDefault.Height / 2.0 - point.Y;
			}
		}
	}

	private void PlayFolderZoomIn()
	{
		BeginFrameProbe("folder-in");
		_folderSourceRect = TryGetFolderCardRect(_viewModel.ActiveFolder);
		LayoutFolderCard();
		ResetFolderPopupTransform();
		FolderOverlayModal.Visibility = Visibility.Visible;
		FolderPopup.UpdateLayout();
		UpdateFolderFrost();
		ComputeFolderZoomStart(out var scale, out var tx, out var ty);
		FolderPopupScale.ScaleX = scale;
		FolderPopupScale.ScaleY = scale;
		FolderPopupTranslate.X = tx;
		FolderPopupTranslate.Y = ty;
		FolderOverlayScrim.Opacity = 0.0;
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(280.0);
		SpringEase easingFunction = new SpringEase
		{
			Bounce = 0.0,
			Decay = 5.0
		};
		DoubleAnimation animation = new DoubleAnimation(scale, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		FolderPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		FolderPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		DoubleAnimation animation2 = new DoubleAnimation(tx, 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(ty, 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			EndFrameProbe();
		};
		FolderPopupTranslate.BeginAnimation(TranslateTransform.XProperty, animation2);
		FolderPopupTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
		FolderOverlayScrim.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void PlayFolderZoomOut()
	{
		if (FolderOverlayModal.Visibility == Visibility.Visible)
		{
			BeginFrameProbe("folder-out");
			double scaleX = FolderPopupScale.ScaleX;
			double x = FolderPopupTranslate.X;
			double y = FolderPopupTranslate.Y;
			ResetFolderPopupTransform();
			FolderPopupScale.ScaleX = scaleX;
			FolderPopupScale.ScaleY = scaleX;
			FolderPopupTranslate.X = x;
			FolderPopupTranslate.Y = y;
			ComputeFolderZoomStart(out var scale, out var tx, out var ty);
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(200.0);
			SpringEase easingFunction = new SpringEase
			{
				Decay = 5.0
			};
			DoubleAnimation animation = new DoubleAnimation(FolderPopupScale.ScaleX, scale, timeSpan)
			{
				EasingFunction = easingFunction
			};
			FolderPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			FolderPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			DoubleAnimation animation2 = new DoubleAnimation(x, tx, timeSpan)
			{
				EasingFunction = easingFunction
			};
			DoubleAnimation animation3 = new DoubleAnimation(y, ty, timeSpan)
			{
				EasingFunction = easingFunction
			};
			FolderPopupTranslate.BeginAnimation(TranslateTransform.XProperty, animation2);
			FolderPopupTranslate.BeginAnimation(TranslateTransform.YProperty, animation3);
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, timeSpan)
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			doubleAnimation.Completed += (object? s, EventArgs e) =>
			{
				FolderOverlayModal.Visibility = Visibility.Collapsed;
				ResetFolderPopupTransform();
				EndFrameProbe();
			};
			FolderOverlayScrim.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	private async void OnMenuStoreFolder(object sender, RoutedEventArgs e)
	{
		bool previousBlock = _blocksDeactivation;
		_blocksDeactivation = true;
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "选择要移动到收纳目录的文件夹",
				Multiselect = true,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
			};
			if (openFolderDialog.ShowDialog(this) == true && ConfirmDialog.Ask("收纳文件夹", "文件夹及其内容将移入收纳目录，原位置不再保留。", $"将移动 {openFolderDialog.FolderNames.Length} 个文件夹到：\n{_viewModel.ImportedFoldersDirectory}", "移动并添加"))
			{
				await _viewModel.StoreFoldersAsync(openFolderDialog.FolderNames);
			}
		}
		catch (Exception ex)
		{
			_viewModel.ReportFileOperationFeedback("无法收纳：" + ex.Message);
		}
		finally
		{
			_blocksDeactivation = previousBlock;
			if (IsVisible && !DesktopResidentSurface)
			{
				Activate();
			}
		}
	}

	private async void OnMenuChooseFolderStorage(object sender, RoutedEventArgs e)
	{
		bool previousBlock = _blocksDeactivation;
		_blocksDeactivation = true;
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "选择文件夹收纳目录（用于之后收纳的文件夹）",
				InitialDirectory = (Directory.Exists(_viewModel.ImportedFoldersDirectory) ? _viewModel.ImportedFoldersDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Personal))
			};
			if (openFolderDialog.ShowDialog(this) == true)
			{
				_viewModel.Settings.ImportedFoldersDirectory = openFolderDialog.FolderName;
				await _settingsVm.FlushPendingSaveAsync();
				_viewModel.ReportFileOperationFeedback("收纳目录已设置，之后收纳的文件夹将放在这里；已有文件夹保持原位置。");
			}
		}
		catch (Exception ex)
		{
			_viewModel.ReportFileOperationFeedback("无法设置收纳目录：" + ex.Message);
		}
		finally
		{
			_blocksDeactivation = previousBlock;
			if (IsVisible && !DesktopResidentSurface)
			{
				Activate();
			}
		}
	}

	private void OnMenuOpenFolderStorage(object sender, RoutedEventArgs e)
	{
		try
		{
			string importedFoldersDirectory = _viewModel.ImportedFoldersDirectory;
			Directory.CreateDirectory(importedFoldersDirectory);
			Process.Start(new ProcessStartInfo(importedFoldersDirectory)
			{
				UseShellExecute = true
			});
			_viewModel.CloseLaunchpad();
		}
		catch (Exception ex)
		{
			_viewModel.ReportFileOperationFeedback("无法打开收纳目录：" + ex.Message);
		}
	}

	private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (!IsHitTestVisible)
		{
			return;
		}
		_musicPointerGesture = BottomMusicWidget != null && IsDescendantOf(e.OriginalSource as DependencyObject, BottomMusicWidget);
		if (_musicPointerGesture || e.ChangedButton != MouseButton.Left || _isWindowAnimating || IsDescendantOf(e.OriginalSource as DependencyObject, SettingsButton) || IsDescendantOf(e.OriginalSource as DependencyObject, EmptyDockHint) || IsDescendantOf(e.OriginalSource as DependencyObject, EmptySearchState) || IsDescendantOf(e.OriginalSource as DependencyObject, SearchPanel) || (TopWidgetsPanel != null && IsDescendantOf(e.OriginalSource as DependencyObject, TopWidgetsPanel)) || IsSettingsOpen)
		{
			return;
		}
		Point position = e.GetPosition(this);
		AppItem appItem = GetAppItemAtPoint(position, out FrameworkElement appElement);
		bool pageNumber = _isPageAnimating || _isReturning;
		bool validated = DateTime.UtcNow.Ticks - _lastSwipeEndTimeTicks < TimeSpan.FromMilliseconds(600.0).Ticks;
		if (pageNumber)
		{
			appItem = null;
			appElement = null;
		}
		_pressedFromDock = appItem != null && DockItems != null && IsDescendantOf(appElement, DockItems);
		double settingIndex = _primaryTranslate.X;
		double x = _neighbourTranslate.X;
		_primaryTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		_neighbourTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		_primaryTranslate.X = settingIndex;
		_neighbourTranslate.X = x;
		_isPointerDown = true;
		_clickDismissedFolder = false;
		_dragStartPoint = position;
		_lastMovePoint = position;
		_lastMoveTimeTicks = DateTime.UtcNow.Ticks;
		_velocityTrackerX = 0.0;
		_peakSpeed = 0.0;
		_isSwiping = false;
		if (!pageNumber && !validated)
		{
			_viewModel.IsPageSwiping = false;
		}
		_swipeTargetPage = -1;
		if (_isPageAnimating || _isReturning)
		{
			bool isReturning = _isReturning;
			_isPageAnimating = false;
			_isReturning = false;
			int num3 = (isReturning ? (-1) : _currentTransitionTarget);
			int currentTransitionDirection = _currentTransitionDirection;
			_currentTransitionTarget = -1;
			_queuedTransitionTarget = -1;
			_slideSeq++;
			CancelDrivenMotion();
			LogFramePhase(isReturning ? "return-kill" : "settle-kill", _slideStartTicks);
			double slideDistance = GetSlideDistance();
			double num4 = ((currentTransitionDirection > 0) ? (0.0 - slideDistance) : slideDistance);
			bool flag2 = !isReturning && num3 >= 1 && (Math.Abs(settingIndex - num4) <= 20.0 || ((Math.Abs(settingIndex) <= 20.0) & isReturning));
			if (flag2)
			{
				StallBegin();
				if (_neighbourIdx >= 0 && _layerPage[_neighbourIdx] == num3)
				{
					RollLayers(currentTransitionDirection, num3);
				}
				else if (num3 >= 1)
				{
					_viewModel.CommitPageChange(num3);
				}
				ResetPageTransforms();
				StallEnd($"regrab landed tgt={num3}");
				settingIndex = 0.0;
			}
			_isSwiping = true;
			_motionMetrics.Begin("page-drag");
			_chainRolledThisGesture = false;
			_viewModel.IsPageSwiping = true;
			_swipeDeviceScale = Math.Max(1.0, VisualTreeHelper.GetDpi(this).DpiScaleX);
			HookFrameRendering();
			Mouse.Capture(this, CaptureMode.SubTree);
			_swipeTargetPage = ((num3 >= 1) ? num3 : (-1));
			_dragStartPoint = new Point(position.X - settingIndex, position.Y);
			_lastMovePoint = position;
			_lastMoveTimeTicks = DateTime.UtcNow.Ticks;
			_velocityTrackerX = 0.0;
			_peakSpeed = 0.0;
			_swipeStartTicks = DateTime.UtcNow.Ticks;
			_pressedApp = null;
			_pressedElement = null;
			_isMouseDownOnApp = false;
			SetSlidingLayersCacheEnabled(enabled: true);
			App.Log($"[SWIPE] in-flight grab px={settingIndex:F0} tgt={num3} landed={flag2}");
		}
		else
		{
			_pressedApp = appItem;
			_pressedElement = appElement;
			_isMouseDownOnApp = appItem != null && appElement != null;
			_appMouseDownPoint = position;
			SetSlidingLayersCacheEnabled(enabled: true);
			HookFrameRendering();
			if (!_isMouseDownOnApp)
			{
				RealiseNeighboursAtPress();
			}
		}
	}

	private void RealiseNeighboursAtPress()
	{
		if (!_isWindowAnimating && !_viewModel.IsFolderOpen)
		{
			StallBegin();
			int currentPage = _viewModel.CurrentPage;
			if (currentPage - 1 >= 1)
			{
				FillLayer(_leftIdx, currentPage - 1);
			}
			if (currentPage + 1 <= _viewModel.TotalPages)
			{
				FillLayer(_rightIdx, currentPage + 1);
			}
			StallEnd("press realise");
		}
	}

	private void OnWindowPreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_musicPointerGesture || (BottomMusicWidget != null && IsDescendantOf(e.OriginalSource as DependencyObject, BottomMusicWidget)))
		{
			return;
		}
		if (_liftReleasePending && !_isSwiping && !PageMotionActive && !_isItemDragging)
		{
			_liftReleasePending = false;
			_viewModel.IsPageSwiping = false;
		}
		if (IsSettingsOpen || _isPageAnimating)
		{
			RestDock("sheet");
			return;
		}
		Point position = e.GetPosition(this);
		if (!_isPointerDown && !_isItemDragging && !PageMotionActive && !_viewModel.IsPageSwiping && DateTime.UtcNow.Ticks - _lastSwipeEndTimeTicks >= TimeSpan.FromMilliseconds(600.0).Ticks && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow && _viewModel.Settings.RevealSearchOnTopHover)
		{
			if (position.Y <= 60.0)
			{
				if (!_viewModel.IsSearchRevealedByHover)
				{
					_viewModel.IsSearchRevealedByHover = true;
					if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
					{
						AnimateSearchSlide(slideDown: true, focus: false);
					}
				}
			}
			else if (position.Y > 100.0 && string.IsNullOrEmpty(_viewModel.SearchText) && !SearchBox.IsKeyboardFocusWithin && _viewModel.IsSearchRevealedByHover)
			{
				_viewModel.IsSearchRevealedByHover = false;
				if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
				{
					AnimateSearchSlide(slideDown: false);
				}
			}
		}
		if (!_isItemDragging && !_isWindowAnimating && !_viewModel.IsFolderOpen && !_viewModel.IsPageSwiping)
		{
			UpdateDockHover(position);
		}
		if (_isItemDragging && _draggedApp != null)
		{
			_pendingCardX = position.X - _dragCardGrabX;
			_pendingCardY = position.Y - _dragCardGrabY;
			_dragCardDirty = true;
			bool validated = IsOverDock(position, _dragFromDock);
			if (validated != _probeOverDock)
			{
				_probeOverDock = validated;
				Rect rect = DockBarRectInWindow();
				Rect rect2 = ((PageDots != null && PageDots.Visibility == Visibility.Visible) ? new Rect(PageDots.TranslatePoint(new Point(0.0, 0.0), this), new Size(PageDots.ActualWidth, PageDots.ActualHeight)) : Rect.Empty);
				App.Log($"[DBG][DOCK-HIT] over={validated} fromDock={_dragFromDock} pos=({position.X:F0},{position.Y:F0}) bar=({rect.X:F0},{rect.Y:F0},{rect.Width:F0}x{rect.Height:F0}) dots=({rect2.X:F0},{rect2.Y:F0},{rect2.Width:F0}x{rect2.Height:F0}) shelf={_dockShelfArmed} reserved={_viewModel.DockShelfReserved} win={ActualHeight:F0}");
			}
			if (_dockShelfArmed && _viewModel.DockApps.Count == 0)
			{
				SetDockShelfLit(IsOverDock(position));
			}
			if (_dragFromDock || (_dragHost == _primaryItems && IsOverDock(position)))
			{
				if (_mergeCandidate != null || _mergeTarget != null)
				{
					ClearMergeHover();
				}
				UpdateDockDragFeedback(position);
				UpdateDockHover(position, withTip: false);
				_dockCarryHover = true;
				if (_dragFromDock)
				{
					if (IsOverDock(position, fromRow: true))
					{
						ClearGridHole();
					}
					else
					{
						SetGridHoleForDockExit(position);
					}
				}
				return;
			}
			if (_dockCarryHover)
			{
				_dockCarryHover = false;
				RestDock("carry-off");
			}
			if (!_dragFromDock && (_dockGapSlot >= 0 || _dockGapRoom != 0))
			{
				ApplyDockGap(-1, 0);
			}
			HideDockDragFeedback();
			AppItem appItemAtPoint = GetAppItemAtPoint(position, out FrameworkElement _);
			UpdateMergeHover(appItemAtPoint, position);
			CheckAndShiftSlot(position);
			CheckEdgePaging(position);
		}
		else
		{
			if (!_isPointerDown)
			{
				return;
			}
			long ticks = DateTime.UtcNow.Ticks;
			double pageNumber = (double)(ticks - _lastMoveTimeTicks) / 10000000.0;
			if (pageNumber > 0.004)
			{
				double settingIndex = (position.X - _lastMovePoint.X) / pageNumber;
				double num3 = 1.0 - Math.Exp((0.0 - pageNumber) * 1000.0 / 45.0);
				_velocityTrackerX += (settingIndex - _velocityTrackerX) * num3;
				double num4 = Math.Abs(settingIndex);
				if ((ticks - _peakSpeedTicks) / 10000 > 110)
				{
					_peakSpeed = 0.0;
				}
				if (num4 > _peakSpeed)
				{
					_peakSpeed = num4;
					_peakSpeedTicks = ticks;
				}
				_lastMovePoint = position;
				_lastMoveTimeTicks = ticks;
			}
			double num5 = position.X - _dragStartPoint.X;
			double value = position.Y - _dragStartPoint.Y;
			if (!_isSwiping && !_isItemDragging)
			{
				if (!_viewModel.Settings.IconsLocked && _isMouseDownOnApp && _pressedApp != null && _pressedElement != null && (Math.Abs(num5) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(value) >= SystemParameters.MinimumVerticalDragDistance))
				{
					FrameworkElement pressedElement = _pressedElement;
					AppItem pressedApp = _pressedApp;
					_pressedElement = null;
					_pressedApp = null;
					_isMouseDownOnApp = false;
					StartInteractiveAppDrag(pressedElement, pressedApp, position);
					return;
				}
				if ((!_isMouseDownOnApp || (_viewModel.Settings.IconsLocked && !_pressedFromDock)) && !_viewModel.IsFolderOpen && Math.Abs(num5) >= 3.0 && Math.Abs(num5) >= Math.Abs(value) * 0.9 && !IsDescendantOf(e.OriginalSource as DependencyObject, SearchPanel))
				{
					_isSwiping = true;
					_motionMetrics.Begin("page-drag");
					_chainRolledThisGesture = false;
					_viewModel.IsPageSwiping = true;
					_swipeDeviceScale = Math.Max(1.0, VisualTreeHelper.GetDpi(this).DpiScaleX);
					_lastFrameTicks = 0L;
					_swipeFrameCount = 0;
					_swipeFrameGapTotal = 0.0;
					_swipeFrameGapWorst = 0.0;
					_swipeFrameGapWorstAt = 0.0;
					_swipeDroppedFrames = 0;
					_swipeMoveCount = 0;
					_swipeStartTicks = DateTime.UtcNow.Ticks;
					HookFrameRendering();
					_primaryTranslate.BeginAnimation(TranslateTransform.XProperty, null);
					_neighbourTranslate.BeginAnimation(TranslateTransform.XProperty, null);
					Mouse.Capture(this, CaptureMode.SubTree);
					int num6 = ((num5 < 0.0) ? 1 : (-1));
					int num7 = _viewModel.CurrentPage + num6;
					if (num7 >= 1 && num7 <= _viewModel.TotalPages)
					{
						_swipeTargetPage = num7;
						EnsureNeighbour(num6);
					}
					_dragStartPoint.X += (double)Math.Sign(num5) * 3.0;
					num5 = position.X - _dragStartPoint.X;
					_lastTravelDirection = num6;
					SetSlidingLayersCacheEnabled(enabled: true);
					ApplySwipeOffsets(num5);
				}
			}
			if (!_isSwiping)
			{
				return;
			}
			_swipeMoveCount++;
			double slideDistance = GetSlideDistance();
			int num8 = ((num5 < 0.0) ? 1 : ((num5 > 0.0) ? (-1) : _lastTravelDirection));
			if (!_chainRolledThisGesture)
			{
				int num9 = ((num8 > 0) ? _rightIdx : _leftIdx);
				int num10 = _viewModel.CurrentPage + num8;
				double num11 = ((num8 > 0) ? (0.0 - num5) : num5);
				if (num11 >= slideDistance && num10 >= 1 && num10 <= _viewModel.TotalPages && _layerPage[num9] == num10)
				{
					EnsureNeighbour(num8);
					RollLayers(num8, num10);
					_dragStartPoint.X -= (double)num8 * slideDistance;
					num5 += (double)num8 * slideDistance;
					int num12 = num10 + num8;
					_swipeTargetPage = ((num12 >= 1 && num12 <= _viewModel.TotalPages) ? num12 : (-1));
					_chainRolledThisGesture = true;
					App.Log($"[SWIPE] chain roll -> page {num10}, carried {num11 - slideDistance:F0} at {num5:F0} vis px nextTgt={_swipeTargetPage}");
				}
			}
			int num13 = _viewModel.CurrentPage + num8;
			if (num13 >= 1 && num13 <= _viewModel.TotalPages)
			{
				if (_swipeTargetPage != num13)
				{
					_swipeTargetPage = num13;
					_lastTravelDirection = num8;
					EnsureNeighbour(num8);
				}
				ApplySwipeOffsets(num5, num5 + (double)num8 * slideDistance);
				return;
			}
			double primaryX = SpringEase.RubberBand(num5, slideDistance);
			ApplySwipeOffsets(primaryX);
			if (_neighbourIdx >= 0)
			{
				ParkSlot(_neighbourIdx);
				_neighbourIdx = -1;
			}
			_swipeTargetPage = -1;
		}
	}

	private void OnWindowPreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_musicPointerGesture || (BottomMusicWidget != null && IsDescendantOf(e.OriginalSource as DependencyObject, BottomMusicWidget)))
		{
			_musicPointerGesture = false;
			return;
		}
		ApplyPendingFrameUpdates();
		if (IsSettingsOpen)
		{
			return;
		}
		if (_isSwiping)
		{
			e.Handled = true;
		}
		if (_isItemDragging)
		{
			UnhookFrameRendering();
			EndInteractiveAppDrag(e.GetPosition(this));
			return;
		}
		_isPointerDown = false;
		_isMouseDownOnApp = false;
		_pressedApp = null;
		_pressedElement = null;
		_pressedFromDock = false;
		_edgeHoverStartTicks = 0L;
		_pendingEdgeDirection = 0;
		if (_isSwiping)
		{
			_isSwiping = false;
			Mouse.Capture(null);
			double pageNumber = e.GetPosition(this).X - _dragStartPoint.X;
			double settingIndex = ((PageViewport.ActualWidth > 0.0) ? PageViewport.ActualWidth : ActualWidth);
			if (settingIndex <= 0.0)
			{
				settingIndex = 1400.0;
			}
			bool validated = DateTime.UtcNow.Ticks - _lastMoveTimeTicks < TimeSpan.FromMilliseconds(160.0).Ticks;
			double num3 = (validated ? Math.Abs(_velocityTrackerX) : 0.0);
			bool flag2 = (DateTime.UtcNow.Ticks - _peakSpeedTicks) / 10000 < 140;
			double num4;
			if (validated)
			{
				num4 = num3;
			}
			else
			{
				num4 = (flag2 ? (_peakSpeed * 0.5) : 0.0);
			}
			double num5 = Math.Abs(pageNumber) + num4 * 0.13;
			double num6 = settingIndex * PageCommitRatio;
			bool flag3 = !_viewModel.Settings.SnapBackWhenShort || num5 >= num6;
			double num7 = ((Math.Abs(_primaryTranslate.X) > Math.Abs(pageNumber)) ? _primaryTranslate.X : pageNumber);
			if (_swipeTargetPage <= 0)
			{
				if (num7 < -3.0 && _viewModel.CurrentPage < _viewModel.TotalPages)
				{
					_swipeTargetPage = _viewModel.CurrentPage + 1;
					EnsureNeighbour(1);
				}
				else if (num7 > 3.0 && _viewModel.CurrentPage > 1)
				{
					_swipeTargetPage = _viewModel.CurrentPage - 1;
					EnsureNeighbour(-1);
				}
			}
			bool flag4 = (_swipeTargetPage > _viewModel.CurrentPage) & flag3;
			bool flag5 = (_swipeTargetPage < _viewModel.CurrentPage && _swipeTargetPage >= 1) & flag3;
			if (flag4 | flag5)
			{
				int swipeTargetPage = _swipeTargetPage;
				int num8 = (flag4 ? 1 : (-1));
				EnsureNeighbour(num8);
				_isPageAnimating = true;
				_currentTransitionTarget = swipeTargetPage;
				_currentTransitionDirection = num8;
				double num9 = (flag4 ? Math.Max(100.0, settingIndex + pageNumber) : Math.Max(100.0, settingIndex - pageNumber));
				double endPrimaryX = (flag4 ? (0.0 - settingIndex) : settingIndex);
				var (easingFunctionBase, duration, num10) = ResolveSettle(num9, num4);
				App.Log($"[SWIPE] commit dx={pageNumber:F0} proj={num5:F0}/{num6:F0} rem={num9:F0} v={num3:F0} tr={num4:F0} peak={_peakSpeed:F0} -> {duration.TotalMilliseconds:F0}ms {easingFunctionBase.GetType().Name} hand={num4 / 1000.0:F1} needed={num9 / duration.TotalMilliseconds:F1} top={num10 * num9 / duration.TotalMilliseconds:F1} DIP/ms");
				StartSlideAnimation(swipeTargetPage, num8, _primaryTranslate.X, _neighbourTranslate.X, endPrimaryX, duration, easingFunctionBase);
			}
			else
			{
				App.Log($"[SWIPE] snap dx={pageNumber:F0} vis={_primaryTranslate.X:F0} proj={num5:F0}/{num6:F0} v={num3:F0} tr={num4:F0} peak={_peakSpeed:F0} tgt={_swipeTargetPage}");
				(EasingFunctionBase Ease, TimeSpan Duration) tuple2 = ResolveReturn(Math.Abs(_primaryTranslate.X), num4);
				EasingFunctionBase item = tuple2.Ease;
				TimeSpan item2 = tuple2.Duration;
				_isReturning = true;
				_slideFromPX = _primaryTranslate.X;
				_slideToPX = 0.0;
				_slideFromTX = ((_neighbourIdx >= 0) ? _neighbourTranslate.X : 0.0);
				_slideToTX = ((pageNumber < 0.0) ? settingIndex : (0.0 - settingIndex));
				_slideEase = item;
				_slideDurationMs = item2.TotalMilliseconds;
				_slideStartTicks = DateTime.UtcNow.Ticks;
				BeginDrivenMotion(_slideFromPX, 0.0, _slideFromTX, (_neighbourIdx >= 0) ? new double?(_slideToTX) : ((double?)null), item2, item, () =>
				{
					_isReturning = false;
					_swipeTargetPage = -1;
					LogFramePhase("return", _slideStartTicks);
					ResetPageTransforms();
				});
			}
			LogFramePhase("drag", _swipeStartTicks);
			_lastSwipeEndTimeTicks = DateTime.UtcNow.Ticks;
			return;
		}
		UnhookFrameRendering();
		Point position = e.GetPosition(this);
		double length = (position - _dragStartPoint).Length;
		if (e.ChangedButton != MouseButton.Left || !(length < 6.0) || _viewModel.IsFolderOpen || _clickDismissedFolder || (e.OriginalSource != RootGrid && (!(e.OriginalSource is FrameworkElement frameworkElement) || frameworkElement.DataContext is AppItem)))
		{
			return;
		}
		Rect rect = ((PageDots != null && PageDots.Visibility == Visibility.Visible) ? new Rect(PageDots.TranslatePoint(new Point(0.0, 0.0), this), new Size(PageDots.ActualWidth, PageDots.ActualHeight)) : Rect.Empty);
		Rect rect2 = DockHitRect();
		App.Log($"[DBG][CLICK-DBG] pos=({position.X:F0},{position.Y:F0}) src={(e.OriginalSource as DependencyObject)?.GetType().Name ?? "null"} dots=({rect.X:F0},{rect.Y:F0},{rect.Width:F0}x{rect.Height:F0}) inDots={rect.Contains(position)} band=({rect2.X:F0},{rect2.Y:F0},{rect2.Width:F0}x{rect2.Height:F0}) inBand={rect2.Contains(position)} shelf={_dockShelfArmed} reserved={_viewModel.DockShelfReserved} menus={_openMenus}");
		if (TookContextMenu())
		{
			App.Log("Click only took the context menu away, grid stays open");
		}
		else if (!IsDescendantOf(e.OriginalSource as DependencyObject, SearchPanel) && !IsDescendantOf(e.OriginalSource as DependencyObject, SettingsButton) && !IsDescendantOf(e.OriginalSource as DependencyObject, EmptySearchState) && (TopWidgetsPanel == null || !IsDescendantOf(e.OriginalSource as DependencyObject, TopWidgetsPanel)) && !IsDescendantOf(e.OriginalSource as DependencyObject, DockLayer) && !IsDescendantOf(e.OriginalSource as DependencyObject, EmptyDockHint) && (PageDots == null || !IsDescendantOf(e.OriginalSource as DependencyObject, PageDots)))
		{
			App.Log("Background clicked, closing Launchpad");
			if (!TryAutoUnpinResidentLaunchpad())
			{
				_viewModel.CloseLaunchpad();
			}
		}
	}

	private bool TookContextMenu()
	{
		double pageNumber = ((_menuClosedTicks == 0L) ? double.PositiveInfinity : ((double)(DateTime.UtcNow.Ticks - _menuClosedTicks) / 10000.0));
		if (_openMenus <= 0 && pageNumber >= 250.0)
		{
			return false;
		}
		_openMenus = 0;
		_menuClosedTicks = 0L;
		return true;
	}

	private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T result)
			{
				return result;
			}
			T val = FindVisualChild<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	private AppItem? GetAppItemAtPoint(Point windowPoint, out FrameworkElement? appElement)
	{
		appElement = null;
		if (_viewModel.IsFolderOpen)
		{
			ItemsControl folderItemsControl = FolderItemsControl;
			ObservableCollection<AppItem> observableCollection = _viewModel.ActiveFolder?.Children;
			if (folderItemsControl != null && observableCollection != null)
			{
				for (int i = 0; i < observableCollection.Count; i++)
				{
					if (folderItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement && new Rect(frameworkElement.TranslatePoint(new Point(0.0, 0.0), this), new Size(frameworkElement.ActualWidth, frameworkElement.ActualHeight)).Contains(windowPoint))
					{
						appElement = FindVisualChild<Button>(frameworkElement) ?? frameworkElement;
						return observableCollection[i];
					}
				}
			}
			return null;
		}
		if (TryGetDockItemAt(windowPoint, out FrameworkElement element, out AppItem app))
		{
			appElement = FindVisualChild<Button>(element) ?? element;
			return app;
		}
		bool validated = false;
		ObservableCollection<AppItem> currentPageApps = _viewModel.CurrentPageApps;
		Rect cardRect;
		for (int j = 0; j < currentPageApps.Count && TryGetCardRectAt(j, out cardRect); j++)
		{
			validated = true;
			if (cardRect.Contains(windowPoint))
			{
				appElement = ItemButtonOf(_primaryItems, j);
				if (appElement != null)
				{
					return currentPageApps[j];
				}
				appElement = null;
				return null;
			}
		}
		if (validated)
		{
			return null;
		}
		HitTestResult hitTestResult = VisualTreeHelper.HitTest(this, windowPoint);
		if (hitTestResult?.VisualHit != null)
		{
			DependencyObject dependencyObject = hitTestResult.VisualHit;
			while (dependencyObject != null && dependencyObject != this)
			{
				if (dependencyObject is Button { DataContext: AppItem dataContext } button)
				{
					appElement = button;
					return dataContext;
				}
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
		}
		return null;
	}

	private static T? FindVisualChild<T>(DependencyObject? parent, string name) where T : FrameworkElement
	{
		if (parent == null)
		{
			return null;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T val && val.Name == name)
			{
				return val;
			}
			T val2 = FindVisualChild<T>(child, name);
			if (val2 != null)
			{
				return val2;
			}
		}
		return null;
	}

	private void ResetAllGridItemsVisualState(AppItem? keepHidden = null)
	{
		for (int i = 0; i < _viewModel.CurrentPageApps.Count; i++)
		{
			if ((keepHidden == null || _viewModel.CurrentPageApps[i] != keepHidden) && _primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
			{
				frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
				frameworkElement.Opacity = 1.0;
				frameworkElement.BeginAnimation(TranslateTransform.XProperty, null);
				frameworkElement.BeginAnimation(TranslateTransform.YProperty, null);
				frameworkElement.RenderTransform = null;
				Button button = FindVisualChild<Button>(frameworkElement);
				if (button != null)
				{
					button.Opacity = 1.0;
				}
			}
		}
	}

	private void SetGridTileOpacity(AppItem app, double alpha)
	{
		int pageNumber = _viewModel.CurrentPageApps.IndexOf(app);
		if (pageNumber >= 0 && _primaryItems.ItemContainerGenerator.ContainerFromIndex(pageNumber) is FrameworkElement frameworkElement)
		{
			frameworkElement.Opacity = alpha;
		}
	}

	internal void OnAppButtonClick(object sender, RoutedEventArgs e)
	{
		if (_isItemDragging || DateTime.UtcNow.Ticks - _lastDragEndTimeTicks < TimeSpan.FromMilliseconds(450.0).Ticks)
		{
			e.Handled = true;
		}
		else
		{
			_isAppLaunching = true;
		}
	}

	private void SetDragPageDim(Border? layer)
	{
		if (layer != _dragDimmedLayer)
		{
			if (_dragDimmedLayer != null)
			{
				_dragDimmedLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180.0)));
			}
			_dragDimmedLayer = layer;
			layer?.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.55, TimeSpan.FromMilliseconds(160.0)));
		}
	}

	private void StartInteractiveAppDrag(FrameworkElement element, AppItem app, Point mousePos)
	{
		if (!_viewModel.Settings.IconsLocked && !app.IsOpenWindowEntry)
		{
			_isItemDragging = true;
			_dockBandPending = 0.0;
			_dockBandOwnedByGesture = false;
			_draggedApp = app;
			_viewModel.IsItemDragActive = true;
			_dragFromDock = _pressedFromDock;
			if (_dragFromDock)
			{
				_dragHost = DockItems;
				_draggedSlotIndex = _viewModel.DockApps.IndexOf(app);
				CloseDockGap();
			}
			else
			{
				_dragHost = (_viewModel.IsFolderOpen ? FolderItemsControl : _primaryItems);
				_draggedSlotIndex = ((!_viewModel.IsFolderOpen) ? _viewModel.CurrentPageApps : _viewModel.ActiveFolder?.Children)?.IndexOf(app) ?? (-1);
			}
			_draggedSlotContainer = element;
			_dragCursorPos = mousePos;
			_lastShiftTimeTicks = DateTime.UtcNow.Ticks;
			_lastShiftFromIndex = -1;
			_edgeHoverStartTicks = 0L;
			_pendingEdgeDirection = 0;
			_edgePageCooldownUntil = 0L;
			_edgePagingTimer?.Stop();
			_mergeTarget = null;
			_mergeCandidate = null;
			_mergeCandidateStartTicks = 0L;
			_mergePulsedElement = null;
			_gridHoleIndex = -1;
			_gridHoleEjected = null;
			_dockCarryHover = false;
			if (!_dragFromDock && !_viewModel.IsFolderOpen && _viewModel.DockApps.Count == 0 && DockCanAccept(app))
			{
				ArmDockShelf(app);
			}
			_isSwiping = false;
			_swipeTargetPage = -1;
			ResetPageTransforms();
			_dragLandingToken++;
			FloatingDragCard.BeginAnimation(Canvas.LeftProperty, null);
			FloatingDragCard.BeginAnimation(Canvas.TopProperty, null);
			double cardWidth = _viewModel.CardWidth;
			double cardHeight = _viewModel.CardHeight;
			FrameworkElement frameworkElement = FindVisualChild<Border>(element, "CardBorder") ?? element;
			FrameworkElement frameworkElement2 = FindVisualChild<Border>(element, "IconBorder") ?? frameworkElement;
			double pageNumber = (_dragFromDock ? _viewModel.DockIconSize : ((double)_viewModel.IconSize));
			FloatingDragImage.Width = pageNumber;
			FloatingDragImage.Height = pageNumber;
			FloatingDragImage.Source = app.Icon;
			FloatingDragLabel.Text = app.DisplayName;
			FloatingDragLabel.MaxWidth = _viewModel.CardTextWidth;
			FloatingDragLabel.Visibility = Visibility.Visible;
			Point point = frameworkElement2.TranslatePoint(new Point(0.0, 0.0), frameworkElement);
			_dragCardGrabX = point.X + frameworkElement2.ActualWidth / 2.0;
			_dragCardGrabY = point.Y + frameworkElement2.ActualHeight / 2.0;
			FloatingDragCard.Width = cardWidth;
			FloatingDragCard.Height = cardHeight;
			Canvas.SetLeft(FloatingDragCard, mousePos.X - _dragCardGrabX);
			Canvas.SetTop(FloatingDragCard, mousePos.Y - _dragCardGrabY);
			FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, null);
			FloatingDragCard.Opacity = 1.0;
			FloatingDragCard.Visibility = Visibility.Visible;
			FloatingDragScale.ScaleX = 1.0;
			FloatingDragScale.ScaleY = 1.0;
			DoubleAnimation animation = SpringTo(1.12, 220, 0.3);
			FloatingDragScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			FloatingDragScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			if (_draggedSlotIndex >= 0 && _dragHost?.ItemContainerGenerator.ContainerFromIndex(_draggedSlotIndex) is FrameworkElement frameworkElement3)
			{
				frameworkElement3.Opacity = (_dragFromDock ? 0.0 : 0.22);
			}
			else
			{
				element.Opacity = (_dragFromDock ? 0.0 : 0.22);
			}
			Mouse.Capture(this, CaptureMode.SubTree);
			if (_dragHost == _primaryItems)
			{
				SetDragPageDim(_primaryLayer);
			}
			RestDock("drag-start");
			HookFrameRendering();
		}
	}

	private bool TryGetGridGeometry(out Point gridOrigin, out double cellWidth, out double cellHeight)
	{
		gridOrigin = default;
		cellWidth = (cellHeight = 0.0);
		int pageNumber = Math.Max(1, _viewModel.Columns);
		int settingIndex = Math.Max(1, _viewModel.Rows);
		UniformGrid uniformGrid = FindVisualChild<UniformGrid>(_primaryItems);
		if (uniformGrid == null || uniformGrid.ActualWidth <= 0.0 || uniformGrid.ActualHeight <= 0.0)
		{
			return false;
		}
		gridOrigin = uniformGrid.TranslatePoint(new Point(0.0, 0.0), this);
		cellWidth = uniformGrid.ActualWidth / (double)pageNumber;
		cellHeight = uniformGrid.ActualHeight / (double)settingIndex;
		return true;
	}

	private int GridSlotAt(Point pos)
	{
		if (!TryGetGridGeometry(out var gridOrigin, out var cellWidth, out var cellHeight))
		{
			return -1;
		}
		int pageNumber = Math.Max(1, _viewModel.Columns);
		int settingIndex = Math.Max(1, _viewModel.Rows);
		double num3 = pos.X - gridOrigin.X;
		double num4 = pos.Y - gridOrigin.Y;
		if (num3 < -30.0 || num3 > cellWidth * (double)pageNumber + 30.0 || num4 < -30.0 || num4 > cellHeight * (double)settingIndex + 30.0)
		{
			return -1;
		}
		int num5 = Math.Clamp((int)(num4 / cellHeight), 0, settingIndex - 1) * pageNumber + Math.Clamp((int)(num3 / cellWidth), 0, pageNumber - 1);
		int count = _viewModel.CurrentPageApps.Count;
		if (count <= 0 || num5 < count)
		{
			return num5;
		}
		return count - 1;
	}

	private void CheckAndShiftSlot(Point currentPos)
	{
		if (_viewModel.CurrentPageApps.Count <= 1 || _draggedApp == null || _draggedSlotIndex < 0 || _mergeTarget != null || _dragHost != _primaryItems || !TryGetGridGeometry(out var gridOrigin, out var cellWidth, out var cellHeight))
		{
			return;
		}
		int pageNumber = Math.Max(1, _viewModel.Columns);
		int settingIndex = GridSlotAt(currentPos);
		if (settingIndex < 0)
		{
			return;
		}
		_ = settingIndex % pageNumber;
		_ = settingIndex / pageNumber;
		if (settingIndex == _draggedSlotIndex || settingIndex < 0)
		{
			return;
		}
		int num3 = _draggedSlotIndex % pageNumber;
		int num4 = _draggedSlotIndex / pageNumber;
		int num5 = settingIndex % pageNumber;
		int num6 = settingIndex / pageNumber;
		AppItem appItem = ((settingIndex < _viewModel.CurrentPageApps.Count) ? _viewModel.CurrentPageApps[settingIndex] : null);
		if (appItem == null || appItem == _draggedApp || !TryGetCardRect(appItem, out var cardRect) || HasReachedCardCentre(cardRect, currentPos, num5 - num3, num6 - num4))
		{
			double num7 = gridOrigin.X + ((double)num3 + 0.5) * cellWidth;
			double num8 = gridOrigin.Y + ((double)num4 + 0.5) * cellHeight;
			double num9 = gridOrigin.X + ((double)num5 + 0.5) * cellWidth;
			double num10 = gridOrigin.Y + ((double)num6 + 0.5) * cellHeight;
			double num11 = Math.Sqrt(Math.Pow(currentPos.X - num7, 2.0) + Math.Pow(currentPos.Y - num8, 2.0));
			double num12 = Math.Sqrt(Math.Pow(currentPos.X - num9, 2.0) + Math.Pow(currentPos.Y - num10, 2.0));
			long ticks = DateTime.UtcNow.Ticks;
			if (num12 + 8.0 < num11 && ticks - _lastShiftTimeTicks > TimeSpan.FromMilliseconds(110.0).Ticks && (settingIndex != _lastShiftFromIndex || ticks - _lastShiftTimeTicks >= TimeSpan.FromMilliseconds(260.0).Ticks))
			{
				_lastShiftFromIndex = _draggedSlotIndex;
				_lastShiftTimeTicks = ticks;
				ShiftGridItems(_draggedSlotIndex, settingIndex);
			}
		}
	}

	private static bool HasReachedCardCentre(Rect cardRect, Point cursorPos, int colDelta, int rowDelta)
	{
		if (Math.Abs(colDelta) >= Math.Abs(rowDelta))
		{
			double pageNumber = cardRect.X + cardRect.Width / 2.0;
			if (colDelta <= 0)
			{
				return cursorPos.X <= pageNumber;
			}
			return cursorPos.X >= pageNumber;
		}
		double settingIndex = cardRect.Y + cardRect.Height / 2.0;
		if (rowDelta <= 0)
		{
			return cursorPos.Y <= settingIndex;
		}
		return cursorPos.Y >= settingIndex;
	}

	private void ShiftGridItems(int fromIndex, int toIndex)
	{
		if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= _viewModel.CurrentPageApps.Count || toIndex >= _viewModel.CurrentPageApps.Count)
		{
			return;
		}
		Dictionary<AppItem, Point> dictionary = new Dictionary<AppItem, Point>();
		for (int i = 0; i < _viewModel.CurrentPageApps.Count; i++)
		{
			AppItem key = _viewModel.CurrentPageApps[i];
			if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
			{
				dictionary[key] = frameworkElement.TranslatePoint(new Point(0.0, 0.0), PageViewport);
				frameworkElement.BeginAnimation(TranslateTransform.XProperty, null);
				frameworkElement.BeginAnimation(TranslateTransform.YProperty, null);
				frameworkElement.RenderTransform = null;
			}
		}
		_viewModel.CurrentPageApps.Move(fromIndex, toIndex);
		_draggedSlotIndex = toIndex;
		PageViewport.UpdateLayout();
		for (int j = 0; j < _viewModel.CurrentPageApps.Count; j++)
		{
			AppItem appItem = _viewModel.CurrentPageApps[j];
			if (!(_primaryItems.ItemContainerGenerator.ContainerFromIndex(j) is FrameworkElement frameworkElement2))
			{
				continue;
			}
			if (appItem == _draggedApp)
			{
				frameworkElement2.Opacity = 0.22;
				frameworkElement2.RenderTransform = null;
				_draggedSlotContainer = frameworkElement2;
				continue;
			}
			frameworkElement2.Opacity = 1.0;
			if (dictionary.TryGetValue(appItem, out var value))
			{
				Point point = frameworkElement2.TranslatePoint(new Point(0.0, 0.0), PageViewport);
				double pageNumber = value.X - point.X;
				double settingIndex = value.Y - point.Y;
				if (Math.Abs(pageNumber) > 0.5 || Math.Abs(settingIndex) > 0.5)
				{
					TranslateTransform translateTransform = (TranslateTransform)(frameworkElement2.RenderTransform = new TranslateTransform(pageNumber, settingIndex));
					DoubleAnimation animation = SpringTo(pageNumber, 0.0, 210, 0.22);
					DoubleAnimation animation2 = SpringTo(settingIndex, 0.0, 210, 0.22);
					translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
					translateTransform.BeginAnimation(TranslateTransform.YProperty, animation2);
				}
				else
				{
					frameworkElement2.RenderTransform = null;
				}
			}
		}
	}

	private void SetGridHoleForDockExit(Point pos)
	{
		if (_viewModel.IsSearchActive)
		{
			ClearGridHole();
		}
		else if (_draggedApp != null)
		{
			int pageNumber = GridSlotAt(pos);
			if (pageNumber != _gridHoleIndex)
			{
				ApplyGridHole(pageNumber);
			}
		}
	}

	private void ClearGridHole()
	{
		if (_gridHoleIndex >= 0)
		{
			ApplyGridHole(-1);
		}
	}

	private void ApplyGridHole(int slot)
	{
		ObservableCollection<AppItem> currentPageApps = _viewModel.CurrentPageApps;
		int gridHoleIndex = _gridHoleIndex;
		_gridHoleIndex = slot;
		int pageNumber = 0;
		Dictionary<AppItem, Point> dictionary = new Dictionary<AppItem, Point>(currentPageApps.Count);
		for (int i = 0; i < currentPageApps.Count; i++)
		{
			if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
			{
				dictionary[currentPageApps[i]] = frameworkElement.TranslatePoint(new Point(0.0, 0.0), PageViewport);
				frameworkElement.BeginAnimation(TranslateTransform.XProperty, null);
				frameworkElement.BeginAnimation(TranslateTransform.YProperty, null);
				frameworkElement.RenderTransform = null;
			}
		}
		if (gridHoleIndex >= 0 && gridHoleIndex < currentPageApps.Count && currentPageApps[gridHoleIndex] == _draggedApp)
		{
			currentPageApps.RemoveAt(gridHoleIndex);
		}
		if (slot >= 0 && _draggedApp != null)
		{
			currentPageApps.Insert(Math.Min(slot, currentPageApps.Count), _draggedApp);
		}
		if (slot >= 0)
		{
			if (_gridHoleEjected == null && currentPageApps.Count > _viewModel.PageSize)
			{
				_gridHoleEjected = currentPageApps[currentPageApps.Count - 1];
				currentPageApps.RemoveAt(currentPageApps.Count - 1);
			}
		}
		else if (_gridHoleEjected != null)
		{
			currentPageApps.Add(_gridHoleEjected);
			_gridHoleEjected = null;
		}
		PageViewport.UpdateLayout();
		for (int j = 0; j < currentPageApps.Count; j++)
		{
			AppItem appItem = currentPageApps[j];
			if (!(_primaryItems.ItemContainerGenerator.ContainerFromIndex(j) is FrameworkElement frameworkElement2))
			{
				continue;
			}
			if (appItem == _draggedApp)
			{
				frameworkElement2.Opacity = 0.0;
				frameworkElement2.RenderTransform = null;
				continue;
			}
			frameworkElement2.Opacity = 1.0;
			if (dictionary.TryGetValue(appItem, out var value))
			{
				Point point = frameworkElement2.TranslatePoint(new Point(0.0, 0.0), PageViewport);
				double settingIndex = value.X - point.X;
				double num3 = value.Y - point.Y;
				if (!(Math.Abs(settingIndex) < 0.5) || !(Math.Abs(num3) < 0.5))
				{
					TranslateTransform translateTransform = (TranslateTransform)(frameworkElement2.RenderTransform = new TranslateTransform());
					DoubleAnimation doubleAnimation = SpringTo(settingIndex, 0.0, 180, 0.2);
					DoubleAnimation doubleAnimation2 = SpringTo(num3, 0.0, 180, 0.2);
					doubleAnimation.FillBehavior = FillBehavior.Stop;
					doubleAnimation2.FillBehavior = FillBehavior.Stop;
					translateTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
					translateTransform.BeginAnimation(TranslateTransform.YProperty, doubleAnimation2);
					pageNumber++;
				}
			}
		}
		App.Log($"[DBG][GRID-HOLE] {gridHoleIndex} -> {slot} n={currentPageApps.Count} moved={pageNumber}");
	}

	private void ReleaseDockBand(double delta)
	{
		if (delta < 1.0)
		{
			return;
		}
		int pageNumber = Math.Max(1, _viewModel.Rows);
		int settingIndex = Math.Max(1, _viewModel.Columns);
		ObservableCollection<AppItem> currentPageApps = _viewModel.CurrentPageApps;
		int num3 = 0;
		for (int i = 0; i < currentPageApps.Count; i++)
		{
			if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement element)
			{
				int num4 = i / settingIndex;
				if (num4 < pageNumber)
				{
					SetGridBandOffset(element, (0.0 - ((double)num4 + 0.5)) * delta / (double)pageNumber, (pageNumber - 1 - num4) * 20);
					num3++;
				}
			}
		}
		if (PageDots != null && PageDots.Visibility == Visibility.Visible)
		{
			SetGridBandOffset(PageDots, 0.0 - delta, 0);
		}
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(36, 3);
		defaultInterpolatedStringHandler.AppendLiteral("[DBG][DOCK-BAND] delta=");
		defaultInterpolatedStringHandler.AppendFormatted(delta, "F0");
		defaultInterpolatedStringHandler.AppendLiteral(" moved=");
		defaultInterpolatedStringHandler.AppendFormatted(num3);
		defaultInterpolatedStringHandler.AppendLiteral(" dots=");
		ItemsControl pageDots = PageDots;
		defaultInterpolatedStringHandler.AppendFormatted((pageDots != null && pageDots.Visibility == Visibility.Visible) ? 1 : 0);
		App.Log(defaultInterpolatedStringHandler.ToStringAndClear());
	}

	private static void SetGridBandOffset(FrameworkElement element, double deltaY, int delayMs)
	{
		TranslateTransform own = new TranslateTransform();
		Transform existing = element.RenderTransform;
		TransformGroup group = ((existing == null) ? null : new TransformGroup());
		if (group != null)
		{
			group.Children.Add(existing);
			group.Children.Add(own);
			element.RenderTransform = group;
		}
		else
		{
			element.RenderTransform = own;
		}
		own.Y = deltaY;
		DoubleAnimation doubleAnimation = SpringTo(deltaY, 0.0, 300, 0.1);
		doubleAnimation.BeginTime = TimeSpan.FromMilliseconds(delayMs);
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			own.Y = 0.0;
			own.BeginAnimation(TranslateTransform.YProperty, null);
			if (group != null && element.RenderTransform == group)
			{
				element.RenderTransform = existing;
			}
			else if (group == null && element.RenderTransform == own)
			{
				element.RenderTransform = null;
			}
		};
		own.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
	}

	private async void EndInteractiveAppDrag(Point dropPos)
	{
		if (!_isItemDragging)
		{
			return;
		}
		ApplyPendingFrameUpdates();
		SettleDragCrossPage();
		AppItem draggedApp = _draggedApp;
		bool dragFromDock = _dragFromDock;
		AppItem mergeTarget = _mergeTarget ?? _mergeCandidate;
		bool validated = mergeTarget != null && draggedApp != null && draggedApp != mergeTarget;
		bool flag2 = _dragHost == FolderItemsControl;
		ItemsControl dragHost = _dragHost;
		int draggedSlotIndex = _draggedSlotIndex;
		Point location = FolderPopup.TranslatePoint(new Point(0.0, 0.0), this);
		Rect rect = new Rect(location, new Size(FolderPopup.ActualWidth, FolderPopup.ActualHeight));
		bool flag3 = flag2 && draggedApp != null && !rect.Contains(dropPos);
		Point? point = null;
		if (validated && mergeTarget != null && TryGetCardRect(mergeTarget, out var cardRect))
		{
			point = new Point(cardRect.X + cardRect.Width / 2.0, cardRect.Y + cardRect.Height / 2.0);
		}
		_isItemDragging = false;
		_viewModel.IsItemDragActive = false;
		_isPointerDown = false;
		_isMouseDownOnApp = false;
		_pressedApp = null;
		_pressedElement = null;
		_pressedFromDock = false;
		_dragFromDock = false;
		_dockCarryHover = false;
		_isSwiping = false;
		_swipeTargetPage = -1;
		_edgeHoverStartTicks = 0L;
		_pendingEdgeDirection = 0;
		_edgePageCooldownUntil = 0L;
		_edgePagingTimer?.Stop();
		_mergeTarget = null;
		_mergeCandidate = null;
		_mergeCandidateStartTicks = 0L;
		_lastDragEndTimeTicks = DateTime.UtcNow.Ticks;
		_viewModel.LastDragEndTimeTicks = _lastDragEndTimeTicks;
		Mouse.Capture(null);
		if (!(point.HasValue | flag3) && (draggedApp == null || (!dragFromDock && !IsOverDock(dropPos))))
		{
			FloatingDragCard.Visibility = Visibility.Collapsed;
		}
		ResetPageTransforms();
		ClearMergeHoverVisual();
		HideFoldHint();
		SetDragPageDim(null);
		_draggedApp = null;
		_draggedSlotIndex = -1;
		_draggedSlotContainer = null;
		_dragHost = null;
		if (dragFromDock && draggedApp != null && _viewModel.IsSearchActive && !IsOverDock(dropPos, fromRow: true))
		{
			ClearGridHole();
			ResetAllGridItemsVisualState();
			ApplyDockGap(draggedSlotIndex, 0);
			await FlyCardIntoDockAsync(draggedSlotIndex, intoHole: false, 180);
			CloseDockGap();
			LayoutDock(reflow: true);
			RetireDragCard(25);
			EnsureDockFrames();
			return;
		}
		if (flag2)
		{
			if (draggedSlotIndex >= 0 && dragHost?.ItemContainerGenerator.ContainerFromIndex(draggedSlotIndex) is FrameworkElement frameworkElement)
			{
				frameworkElement.Opacity = 1.0;
			}
			if (flag3 && draggedApp != null)
			{
				await _viewModel.RemoveFromFolder(draggedApp);
				_viewModel.CloseFolder();
				await FlyCardIntoGridAsync(draggedApp);
			}
			return;
		}
		if (dragFromDock && draggedApp != null && !IsOverDock(dropPos, fromRow: true))
		{
			_gridHoleIndex = -1;
			_gridHoleEjected = null;
		}
		else
		{
			ClearGridHole();
		}
		bool flag4 = draggedApp != null && !dragFromDock && IsOverDock(dropPos) && DockCanAccept(draggedApp);
		bool flag5 = dragFromDock && draggedApp != null && !IsOverDock(dropPos, fromRow: true);
		if (_dockShelfArmed && !flag4)
		{
			DisarmDockShelf();
		}
		if (!dragFromDock && !flag4 && (_dockGapSlot >= 0 || _dockGapRoom != 0))
		{
			ApplyDockGap(-1, 0);
			CloseDockGap();
		}
		ResetAllGridItemsVisualState((flag4 | flag5) ? draggedApp : null);
		if (flag4 | flag5)
		{
			SetGridTileOpacity(draggedApp, 0.0);
		}
		if (draggedApp != null && (dragFromDock || IsOverDock(dropPos)))
		{
			int insertAt = DockInsertIndexAt(dropPos);
			UpdateDockHover(dropPos, withTip: false);
			if (dragFromDock && IsOverDock(dropPos, fromRow: true))
			{
				Stopwatch swMove = Stopwatch.StartNew();
				BeginFrameProbe("dockmove");
				if (insertAt > draggedSlotIndex)
				{
					insertAt--;
				}
				if (FloatingDragLabel.Visibility == Visibility.Visible)
				{
					double pageNumber = FloatingDragCard.Height / 2.0 - _dragCardGrabY;
					FloatingDragLabel.Visibility = Visibility.Collapsed;
					if (pageNumber != 0.0)
					{
						Canvas.SetTop(FloatingDragCard, Canvas.GetTop(FloatingDragCard) - pageNumber);
					}
				}
				await FlyCardIntoDockAsync(insertAt, intoHole: false, 180);
				await _viewModel.AddToDockAsync(draggedApp, insertAt, deferSave: true);
				CloseDockGap();
				DockItems.UpdateLayout();
				_dockLayoutQueued = false;
				LayoutDock(reflow: true);
				if (_dockLiveStrength > 0.005 && _dockSlots.Count > 0)
				{
					ApplyDockMagnification(_dockLiveStrength, _dockLiveX);
				}
				EnsureDockFrames();
				RetireDragCard(25);
				EndFrameProbe();
				App.Log($"[DBG][DOCK-MOVE] total={swMove.Elapsed.TotalMilliseconds:F0} ms live={_dockLiveStrength:F2} crown={_dockLiveX:F0}");
				_viewModel.SaveDockStateAsync();
				return;
			}
			if (dragFromDock)
			{
				Stopwatch swMove = Stopwatch.StartNew();
				BeginFrameProbe("dockout");
				int landCell = GridSlotAt(dropPos);
				if (landCell < 0)
				{
					landCell = _viewModel.CurrentPageApps.Count;
				}
				if (draggedSlotIndex >= 0 && draggedSlotIndex < _dockSlots.Count)
				{
					_dockSlots[draggedSlotIndex].Element.Opacity = 0.0;
				}
				await FlyCardIntoGridAsync(draggedApp, _viewModel.IconSize, landCell, triggerPop: false);
				_suppressDockReflow = true;
				_dockBandOwnedByGesture = true;
				await _viewModel.RemoveFromDockAsync(draggedApp, landCell, deferSave: true);
				_dockBandOwnedByGesture = false;
				ResetAllGridItemsVisualState();
				_dockGapRoom = 0;
				DockItems.UpdateLayout();
				_dockLayoutQueued = false;
				LayoutDock(reflow: true);
				CloseDockGap();
				_suppressDockReflow = false;
				if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(landCell) is FrameworkElement element)
				{
					PopIntoView(element);
				}
				if (_dockBandPending > 0.0)
				{
					double dockBandPending = _dockBandPending;
					_dockBandPending = 0.0;
					ReleaseDockBand(dockBandPending);
				}
				EndFrameProbe();
				App.Log($"[DBG][DOCK-OUT] total={swMove.Elapsed.TotalMilliseconds:F0} ms cell={landCell} live={_dockLiveStrength:F2}");
				_viewModel.SaveDockStateAsync();
				return;
			}
			Stopwatch swIn = Stopwatch.StartNew();
			BeginFrameProbe("dockin");
			if (FloatingDragLabel.Visibility == Visibility.Visible)
			{
				double settingIndex = FloatingDragCard.Height / 2.0 - _dragCardGrabY;
				FloatingDragLabel.Visibility = Visibility.Collapsed;
				if (settingIndex != 0.0)
				{
					Canvas.SetTop(FloatingDragCard, Canvas.GetTop(FloatingDragCard) - settingIndex);
				}
			}
			await FlyCardIntoDockAsync(insertAt, DockCanAccept(draggedApp), 180);
			_ = swIn.Elapsed.TotalMilliseconds;
			await _viewModel.AddToDockAsync(draggedApp, insertAt, deferSave: true);
			_ = swIn.Elapsed.TotalMilliseconds;
			ResetAllGridItemsVisualState();
			_dockGapRoom = 0;
			CloseDockGap();
			DockItems.UpdateLayout();
			_dockLayoutQueued = false;
			LayoutDock(reflow: true);
			DisarmDockShelf(instant: true);
			if (_dockLiveStrength > 0.005 && _dockSlots.Count > 0)
			{
				ApplyDockMagnification(_dockLiveStrength, _dockLiveX);
			}
			EnsureDockFrames();
			RetireDragCard(25);
			_viewModel.SaveDockStateAsync();
			EndFrameProbeAfter("dockin", 420);
			return;
		}
		if (validated && mergeTarget != null && draggedApp != null)
		{
			if (point.HasValue)
			{
				await FlyDragCardToAsync(point.Value, 0.62, 230);
			}
			else
			{
				FloatingDragCard.Visibility = Visibility.Collapsed;
			}
			await _viewModel.MergeIntoFolderAsync(mergeTarget, draggedApp);
			await PopFolderTileHoldingAsync(mergeTarget);
			return;
		}
		if (draggedSlotIndex >= 0 && draggedSlotIndex < _viewModel.CurrentPageApps.Count)
		{
			DependencyObject dependencyObject = _primaryItems.ItemContainerGenerator.ContainerFromIndex(draggedSlotIndex);
			FrameworkElement finalContainer = dependencyObject as FrameworkElement;
			if (finalContainer != null)
			{
				ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
				finalContainer.RenderTransform = scaleTransform;
				DoubleAnimation doubleAnimation = new DoubleAnimation(1.04, 1.0, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				doubleAnimation.Completed += (object? s, EventArgs e) =>
				{
					finalContainer.RenderTransform = null;
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
			}
		}
		await _viewModel.PersistCurrentOrderAsync();
	}

	private Task FlyDragCardToAsync(Point center, double endScale, int durationMs, double? endIconSize = null)
	{
		TaskCompletionSource landed = new TaskCompletionSource();
		int token = _dragLandingToken;
		double left = Canvas.GetLeft(FloatingDragCard);
		double top = Canvas.GetTop(FloatingDragCard);
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(durationMs);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(left, center.X - FloatingDragCard.Width / 2.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation = new DoubleAnimation(top, center.Y - FloatingDragCard.Height / 2.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation2 = new DoubleAnimation(FloatingDragScale.ScaleX, endScale, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation3 = new DoubleAnimation(FloatingDragScale.ScaleY, endScale, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation growIcon = ((!endIconSize.HasValue) ? null : new DoubleAnimation(endIconSize.Value, timeSpan)
		{
			EasingFunction = easingFunction
		});
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			if (growIcon != null)
			{
				FloatingDragImage.BeginAnimation(FrameworkElement.WidthProperty, null);
				FloatingDragImage.BeginAnimation(FrameworkElement.HeightProperty, null);
			}
			if (token == _dragLandingToken)
			{
				if (growIcon != null)
				{
					FloatingDragImage.Width = endIconSize.Value;
					FloatingDragImage.Height = endIconSize.Value;
				}
				FloatingDragCard.BeginAnimation(Canvas.LeftProperty, null);
				FloatingDragCard.BeginAnimation(Canvas.TopProperty, null);
				FloatingDragScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				FloatingDragScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				FloatingDragCard.Visibility = Visibility.Collapsed;
			}
			landed.TrySetResult();
		};
		FloatingDragCard.BeginAnimation(Canvas.LeftProperty, doubleAnimation);
		FloatingDragCard.BeginAnimation(Canvas.TopProperty, animation);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation2);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation3);
		if (growIcon != null)
		{
			FloatingDragImage.BeginAnimation(FrameworkElement.WidthProperty, growIcon);
			FloatingDragImage.BeginAnimation(FrameworkElement.HeightProperty, growIcon);
		}
		return landed.Task;
	}

	private async Task FlyCardIntoGridAsync(AppItem app, double? endIconSize = null, int landCell = -1, bool triggerPop = true)
	{
		int token = _dragLandingToken;
		FrameworkElement target = null;
		Rect landing = Rect.Empty;
		await Dispatcher.InvokeAsync(() =>
		{
			int pageNumber = _viewModel.CurrentPageApps.IndexOf(app);
			if (pageNumber >= 0)
			{
				target = _primaryItems.ItemContainerGenerator.ContainerFromIndex(pageNumber) as FrameworkElement;
				TryGetCardRect(app, out landing);
			}
			else if (landCell >= 0)
			{
				TryGetCardRectAt(landCell, out landing);
			}
		}, DispatcherPriority.Loaded);
		if (token != _dragLandingToken)
		{
			return;
		}
		if (landing.IsEmpty)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180.0));
			doubleAnimation.Completed += (object? _, EventArgs _) =>
			{
				if (token == _dragLandingToken)
				{
					FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, null);
					FloatingDragCard.Opacity = 1.0;
					FloatingDragCard.Visibility = Visibility.Collapsed;
				}
			};
			FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			return;
		}
		if (target != null)
		{
			target.Opacity = 0.0;
		}
		await FlyDragCardToAsync(new Point(landing.X + landing.Width / 2.0, landing.Y + landing.Height / 2.0), 1.0, 240, endIconSize);
		if (target != null)
		{
			target.Opacity = 1.0;
			if (triggerPop)
			{
				PopIntoView(target);
			}
		}
	}

	private async Task PopFolderTileHoldingAsync(AppItem child)
	{
		await Dispatcher.InvokeAsync(() =>
		{
			AppItem appItem = _viewModel.CurrentPageApps.FirstOrDefault((AppItem a) => a.IsFolder && a.Children.Contains(child));
			int pageNumber = ((appItem == null) ? (-1) : _viewModel.CurrentPageApps.IndexOf(appItem));
			if (pageNumber >= 0 && _primaryItems.ItemContainerGenerator.ContainerFromIndex(pageNumber) is FrameworkElement element)
			{
				PopIntoView(element);
			}
		}, DispatcherPriority.Loaded);
	}

	private static void PopIntoView(FrameworkElement element, double from = 1.04, int durationMs = 180)
	{
		ScaleTransform scale = new ScaleTransform(from, from);
		element.RenderTransformOrigin = new Point(0.5, 0.5);
		element.RenderTransform = scale;
		DoubleAnimation doubleAnimation = new DoubleAnimation(from, 1.0, TimeSpan.FromMilliseconds(durationMs))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			if (element.RenderTransform == scale)
			{
				element.RenderTransform = null;
			}
		};
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
	}

	private void CheckEdgePaging(Point currentPos)
	{
		if (!_isItemDragging || _viewModel.IsFolderOpen || _draggedApp == null)
		{
			_edgePagingTimer?.Stop();
			_edgeHoverStartTicks = 0L;
			_pendingEdgeDirection = 0;
		}
		else
		{
			if (_isPageAnimating)
			{
				return;
			}
			int pageNumber = 0;
			double settingIndex = Math.Clamp(ActualWidth * 0.035, 40.0, 64.0);
			if (currentPos.X < settingIndex && _viewModel.CurrentPage > 1)
			{
				pageNumber = -1;
			}
			else if (currentPos.X > ActualWidth - settingIndex && _viewModel.CurrentPage < _viewModel.TotalPages)
			{
				pageNumber = 1;
			}
			if (pageNumber == 0)
			{
				_edgePagingTimer?.Stop();
				_edgeHoverStartTicks = 0L;
				_pendingEdgeDirection = 0;
				return;
			}
			long ticks = DateTime.UtcNow.Ticks;
			if (_pendingEdgeDirection != pageNumber)
			{
				_pendingEdgeDirection = pageNumber;
				_edgeHoverStartTicks = ticks;
				if (_edgePagingTimer == null)
				{
					_edgePagingTimer = new DispatcherTimer
					{
						Interval = TimeSpan.FromMilliseconds(40.0)
					};
					_edgePagingTimer.Tick += (object? _, EventArgs _) =>
					{
						CheckEdgePaging(Mouse.GetPosition(this));
					};
				}
				_edgePagingTimer.Start();
			}
			else if (ticks >= _edgePageCooldownUntil && ticks - _edgeHoverStartTicks >= TimeSpan.FromMilliseconds(300.0).Ticks)
			{
				_edgeHoverStartTicks = ticks;
				_edgePageCooldownUntil = ticks + TimeSpan.FromMilliseconds(850.0).Ticks;
				int targetPage = _viewModel.CurrentPage + pageNumber;
				MoveDraggedItemToPage(targetPage);
			}
		}
	}

	private void MoveDraggedItemToPage(int targetPage)
	{
		if (_draggedApp != null)
		{
			int pageNumber = ((targetPage > _viewModel.CurrentPage) ? 1 : (-1));
			_viewModel.CurrentPageApps.Remove(_draggedApp);
			_viewModel.MoveAppToPage(_draggedApp, targetPage);
			int settingIndex = ((pageNumber > 0) ? _rightIdx : _leftIdx);
			_layerPage[settingIndex] = -1;
			if (!_viewModel.IsFolderOpen)
			{
				_dragHost = _layerItems[settingIndex];
			}
			SetDragPageDim(_layers[settingIndex]);
			_dragCrossTargetPage = targetPage;
			_dragCrossDirection = pageNumber;
			_dragCrossOutgoingSlot = _primaryIdx;
			AnimatePageTransition(targetPage, pageNumber);
		}
	}

	private void SettleDragCrossPage()
	{
		if (_dragCrossTargetPage < 0)
		{
			return;
		}
		int dragCrossTargetPage = _dragCrossTargetPage;
		int dragCrossDirection = _dragCrossDirection;
		int dragCrossOutgoingSlot = _dragCrossOutgoingSlot;
		_dragCrossTargetPage = -1;
		_dragCrossOutgoingSlot = -1;
		if (_viewModel.CurrentPage != dragCrossTargetPage)
		{
			_isPageAnimating = false;
			_swipeTargetPage = -1;
			_queuedTransitionTarget = -1;
			if (_neighbourIdx >= 0 && _layerPage[_neighbourIdx] == dragCrossTargetPage)
			{
				RollLayers(dragCrossDirection, dragCrossTargetPage);
			}
			else
			{
				_viewModel.CommitPageChange(dragCrossTargetPage);
			}
		}
		if (dragCrossOutgoingSlot >= 0 && dragCrossOutgoingSlot != _primaryIdx)
		{
			_layerPage[dragCrossOutgoingSlot] = -1;
		}
		if (_draggedApp != null)
		{
			if (!_viewModel.IsFolderOpen)
			{
				_dragHost = _primaryItems;
			}
			_draggedSlotIndex = 0;
			PageViewport.UpdateLayout();
			ResetAllGridItemsVisualState();
			if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement frameworkElement)
			{
				frameworkElement.Opacity = 0.22;
				_draggedSlotContainer = frameworkElement;
			}
		}
	}

	private void UpdateMergeHover(AppItem? app, Point cursorPos)
	{
		_dragCursorPos = cursorPos;
		Rect cardRect;
		if (_mergeCandidate != null && _mergeCandidate != app && TryGetCellRect(_mergeCandidate, out var cellRect) && cellRect.Contains(cursorPos))
		{
			TickMergeDwell();
			SyncFoldHint(track: true);
		}
		else if (app == null || _draggedApp == null || _draggedApp.IsFolder || _viewModel.IsFolderOpen || _isPageAnimating || app == _draggedApp || !TryGetCardRect(app, out cardRect) || !cardRect.Contains(cursorPos))
		{
			ClearMergeHover();
		}
		else if (_mergeCandidate != app)
		{
			ClearMergeHoverVisual();
			_mergeCandidate = app;
			_mergeCandidateStartTicks = DateTime.UtcNow.Ticks;
			_mergeAnchorPos = cursorPos;
			_mergeTarget = null;
			HideFoldHint();
		}
		else
		{
			TickMergeDwell();
			SyncFoldHint(track: true);
		}
	}

	private void SyncFoldHint(bool track = false)
	{
		if (_mergeTarget != null)
		{
			if (track || _foldHintTarget != _mergeTarget)
			{
				ShowFoldHint(_mergeTarget);
			}
		}
		else if (_mergeCandidate != null || _foldHintTarget != null)
		{
			HideFoldHint();
		}
	}

	private void ShowFoldHint(AppItem target)
	{
		if (!TryGetCardRect(target, out var cardRect))
		{
			HideFoldHint();
			return;
		}
		bool isFolder = target.IsFolder;
		if (_foldHintTarget != target)
		{
			FoldHintText.Text = (isFolder ? "松手并入此文件夹" : "松手新建文件夹");
			FoldHintGlyph.Data = (isFolder ? JoinGlyph : CreateGlyph);
			FoldHintBadge.Visibility = Visibility.Visible;
			FoldHintText.Margin = new Thickness(7.0, 0.0, 1.0, 0.0);
		}
		Canvas.SetLeft(FoldHighlight, cardRect.X - 9.0);
		Canvas.SetTop(FoldHighlight, cardRect.Y - 9.0);
		FoldHighlight.Width = cardRect.Width + 18.0;
		FoldHighlight.Height = cardRect.Height + 18.0;
		bool validated = _foldHintTarget != target;
		_foldHintTarget = target;
		FoldHighlight.Visibility = Visibility.Visible;
		FoldHint.Visibility = Visibility.Visible;
		DragCanvas.UpdateLayout();
		double pageNumber = cardRect.Y - FoldHint.ActualHeight - 6.0;
		bool flag2 = pageNumber >= 4.0;
		if (!flag2)
		{
			pageNumber = cardRect.Bottom + 6.0;
		}
		Canvas.SetLeft(length: Math.Clamp(cardRect.X + cardRect.Width / 2.0 - FoldHint.ActualWidth / 2.0, 4.0, Math.Max(4.0, ActualWidth - FoldHint.ActualWidth - 4.0)), element: FoldHint);
		Canvas.SetTop(FoldHint, pageNumber);
		FoldHintArrow.Data = (flag2 ? ArrowDown : ArrowUp);
		if (validated)
		{
			FoldHint.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(110.0)));
			FoldHintShift.BeginAnimation(TranslateTransform.YProperty, SpringTo(flag2 ? 7 : (-7), 0.0, 230, 0.2));
			DoubleAnimation animation = SpringTo(0.9, 1.0, 260, 0.35);
			FoldHighlightScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			FoldHighlightScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			FoldHighlight.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120.0)));
		}
	}

	private void HideFoldHint()
	{
		_foldHintTarget = null;
		_dockHintLabel = "";
		FoldHighlight.Visibility = Visibility.Collapsed;
		FoldHint.Visibility = Visibility.Collapsed;
	}

	private static Geometry Freeze(Geometry geometry)
	{
		geometry.Freeze();
		return geometry;
	}

	private void TickMergeDwell()
	{
		if (_mergeCandidate == null || _mergeTarget != null)
		{
			return;
		}
		if ((_dragCursorPos - _mergeAnchorPos).Length > 5.0)
		{
			_mergeAnchorPos = _dragCursorPos;
			_mergeCandidateStartTicks = DateTime.UtcNow.Ticks;
		}
		else if (!((double)(DateTime.UtcNow.Ticks - _mergeCandidateStartTicks) / 10000.0 < 150.0))
		{
			if (!TryGetCardRect(_mergeCandidate, out var cardRect) || !cardRect.Contains(_dragCursorPos))
			{
				ClearMergeHover();
				return;
			}
			_mergeTarget = _mergeCandidate;
			StartMergePulse(_mergeTarget);
		}
	}

	private void CloseDockGap()
	{
		_dockGapSlot = -1;
		_dockGapRoom = 0;
		_dockGapLawRoom = 0;
		_dockGapLawCount = 0;
		_dockGhostSlot = -1;
		_dockLiveGapRoom = 0.0;
		_dockLiveGapRoomVel = 0.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			dockSlot.Element.Opacity = 1.0;
		}
	}

	private bool DockCanAccept(AppItem? app)
	{
		if (app == null)
		{
			return false;
		}
		if (app.IsFolder || app.IsInFolder)
		{
			return false;
		}
		return _viewModel.DockApps.Count < _viewModel.MaxDockItems;
	}

	private void SetDockDragGap(Point pos)
	{
		int count = _viewModel.DockApps.Count;
		int draggedSlotIndex = _draggedSlotIndex;
		bool validated = IsOverDock(pos, _dragFromDock);
		int gap = -1;
		int room = 0;
		if (_dockShelfArmed && count == 0)
		{
			ApplyDockGap(-1, 0);
			return;
		}
		if (_dragFromDock && count > 1 && draggedSlotIndex >= 0)
		{
			if (validated)
			{
				int pageNumber = DockInsertIndexAt(pos);
				gap = Math.Clamp((pageNumber <= draggedSlotIndex) ? pageNumber : (pageNumber - 1), 0, count - 1);
			}
			else if (_viewModel.IsSearchActive)
			{
				gap = draggedSlotIndex;
			}
			else
			{
				gap = draggedSlotIndex;
				room = -1;
			}
		}
		else if ((!_dragFromDock & validated) && DockCanAccept(_draggedApp))
		{
			gap = Math.Clamp(DockInsertIndexAt(pos), 0, count);
			room = 1;
		}
		ApplyDockGap(gap, room);
	}

	private void ApplyDockGap(int gap, int room)
	{
		if (gap == _dockGapSlot && room == _dockGapRoom)
		{
			return;
		}
		_dockGapSlot = gap;
		_dockGapRoom = room;
		_dockGapLawRoom = room;
		_dockGapLawCount = Math.Max(1, _viewModel.DockApps.Count + room);
		App.Log($"[DBG][DOCK-GAP] gap={gap} room={room} n={_viewModel.DockApps.Count} slots={_dockSlots.Count} src={_draggedSlotIndex} fromDock={_dragFromDock}");
		double dockSlot = _viewModel.DockSlot;
		int draggedSlotIndex = _draggedSlotIndex;
		foreach (DockSlot dockSlot3 in _dockSlots)
		{
			if (gap < 0)
			{
				dockSlot3.GapTo = 0.0;
				continue;
			}
			DockSlot dockSlot2 = dockSlot3;
			dockSlot2.GapTo = room switch
			{
				1 => ((dockSlot3.Item >= gap) ? 0.5 : (-0.5)) * dockSlot, 
				-1 => ((dockSlot3.Item < draggedSlotIndex) ? 0.5 : ((dockSlot3.Item > draggedSlotIndex) ? (-0.5) : 0.0)) * dockSlot, 
				_ => DockPermutationOffset(dockSlot3, gap, draggedSlotIndex, dockSlot), 
			};
		}
		_dockGhostSlot = ((_dragFromDock && draggedSlotIndex >= 0) ? draggedSlotIndex : (-1));
		EnsureDockFrames();
	}

	private static double DockPermutationOffset(DockSlot s, int gap, int src, double slot)
	{
		int pageNumber;
		if (s.Item == src)
		{
			pageNumber = gap;
		}
		else
		{
			pageNumber = ((s.Item < src) ? s.Item : (s.Item - 1));
		}
		if (s.Item != src && pageNumber >= gap)
		{
			pageNumber++;
		}
		return (double)(pageNumber - s.Item) * slot;
	}

	private void AnimateDockSize(double target)
	{
		_dockSizeTimer?.Stop();
		_dockSizeFrom = _viewModel.DockIconSize;
		_dockSizeTo = target;
		if (!IsVisible || _viewModel.DockApps.Count == 0)
		{
			_viewModel.SetDockPresentationSize(target);
			return;
		}
		if (_dockSizeTimer == null)
		{
			_dockSizeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (object? _, EventArgs _) =>
			{
				double pageNumber = Math.Clamp(_dockSizeClock.Elapsed.TotalMilliseconds / 180.0, 0.0, 1.0);
				double settingIndex = 1.0 - Math.Pow(1.0 - pageNumber, 3.0);
				_viewModel.SetDockPresentationSize(_dockSizeFrom + (_dockSizeTo - _dockSizeFrom) * settingIndex);
				if (pageNumber >= 1.0)
				{
					_dockSizeTimer?.Stop();
				}
			}, Dispatcher);
		}
		_dockSizeClock.Restart();
		_dockSizeTimer.Start();
	}

	private void QueueDockSizeLayout()
	{
		if (_dockSizeLayoutQueued || _windowClosed)
		{
			return;
		}
		_dockSizeLayoutQueued = true;
		Dispatcher.BeginInvoke((Action)(() =>
		{
			_dockSizeLayoutQueued = false;
			if (!_windowClosed)
			{
				LayoutDock();
			}
		}), DispatcherPriority.Loaded);
	}

	private void OnDockContentsReplaced()
	{
		if (!_suppressDockReflow && !_dockLayoutQueued)
		{
			_dockLayoutQueued = true;
			App.Log($"[DBG][DOCK-QUEUED] n={_viewModel.DockApps.Count} vis={DockLayer.Visibility}");
			ArmDockPileProbe();
			Dispatcher.BeginInvoke((Action)(() =>
			{
				_dockLayoutQueued = false;
				LayoutDock(reflow: true);
			}), DispatcherPriority.Loaded);
		}
	}

	private void ArmDockPileProbe()
	{
		_dockProbeOn = true;
		_dockPendingFrames = 0;
		_dockPendingMaxUnplaced = 0;
		_dockProbeLastTime = default;
		if (!_dockProbeHooked)
		{
			_dockProbeHooked = true;
			CompositionTarget.Rendering += OnDockPileProbe;
		}
	}

	private void ReleaseDockPileProbe()
	{
		_dockProbeOn = false;
		if (_dockProbeHooked)
		{
			_dockProbeHooked = false;
			CompositionTarget.Rendering -= OnDockPileProbe;
			App.Log($"[DBG][DOCK-DRAINED] presentedFrames={_dockPendingFrames} maxMisplaced={_dockPendingMaxUnplaced}");
		}
	}

	private void OnDockPileProbe(object? sender, EventArgs e)
	{
		if (!(e is RenderingEventArgs e2) || e2.RenderingTime == _dockProbeLastTime)
		{
			return;
		}
		_dockProbeLastTime = e2.RenderingTime;
		if (!_dockProbeOn || DockItems == null)
		{
			return;
		}
		_dockPendingFrames++;
		int pageNumber = 0;
		int settingIndex = 0;
		int count = _viewModel.DockApps.Count;
		double num3 = double.NaN;
		double value = double.NaN;
		for (int i = 0; i < count; i++)
		{
			if (!(DockItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement { Visibility: Visibility.Visible, Opacity: not (<=0.0) } frameworkElement))
			{
				continue;
			}
			settingIndex++;
			if (Math.Abs(Canvas.GetLeft(frameworkElement) - _viewModel.DockApps[i].DockLeft) > 0.5)
			{
				pageNumber++;
			}
			try
			{
				double x = frameworkElement.TranslatePoint(new Point(0.0, 0.0), this).X;
				if (double.IsNaN(num3))
				{
					num3 = x;
				}
				value = x + frameworkElement.ActualWidth;
			}
			catch (InvalidOperationException)
			{
			}
		}
		if (pageNumber > _dockPendingMaxUnplaced)
		{
			_dockPendingMaxUnplaced = pageNumber;
		}
		double num4 = 1.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			if (dockSlot.Scale.ScaleX > num4)
			{
				num4 = dockSlot.Scale.ScaleX;
			}
		}
		App.Log($"[DBG][DOCK-PILE] frame={_dockPendingFrames} rendered={settingIndex}/{count} misplaced={pageNumber} slots={_dockSlots.Count} gap={_dockGapSlot} room={_dockGapRoom} law={_dockGapLawRoom} edges=[{num3:F1}..{value:F1}] plateW={DockBar.ActualWidth * DockPlateScale.ScaleX:F1} mag=[tgt={_dockTargetStrength:F2} live={_dockLiveStrength:F2} smax={num4:F2} driving={_dockDriving}]");
	}

	private void LayoutDock(bool reflow = false)
	{
		_dockSlots.Clear();
		if (DockItems == null || DockBar == null)
		{
			return;
		}
		bool validated = _viewModel.DockApps.Any((AppItem a) => a.IsOpenWindowEntry);
		if (validated || _autoSizedOpenApps)
		{
			double pageNumber = ((_viewModel.Settings.DockIconSize > 0.0) ? _viewModel.Settings.DockIconSize : 56.0);
			int count = _viewModel.DockApps.Count;
			double settingIndex = ((ActualWidth > 0.0) ? ActualWidth : SystemParameters.WorkArea.Width);
			double num3 = (validated ? Math.Min(pageNumber, Math.Max(12.0, (settingIndex - 96.0 - (double)count * _viewModel.DockGap) / Math.Max(1.0, (double)count + 3.3 * (DockMagnifyMax - 1.0)))) : pageNumber);
			if (Math.Abs(_viewModel.DockIconSize - num3) > 0.1)
			{
				_viewModel.SetDockPresentationSize(num3);
			}
			_autoSizedOpenApps = validated;
		}
		double dockSlot = _viewModel.DockSlot;
		int count2 = _viewModel.DockApps.Count;
		double num4 = (double)count2 * dockSlot / 2.0;
		List<string> list = new List<string>(count2);
		Dictionary<AppItem, double> dockPlacedLeft = _dockPlacedLeft;
		Dictionary<AppItem, double> dictionary = new Dictionary<AppItem, double>(count2);
		double num5 = 1.0;
		double num6 = 0.0;
		double num7 = 0.0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		DockItems.UpdateLayout();
		double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
		for (int num8 = 0; num8 < count2; num8++)
		{
			if (DockItems.ItemContainerGenerator.ContainerFromIndex(num8) is FrameworkElement frameworkElement)
			{
				AppItem appItem = _viewModel.DockApps[num8];
				double num9 = (appItem.DockLeft = (double)num8 * dockSlot - num4);
				frameworkElement.Opacity = 1.0;
				Canvas.SetLeft(frameworkElement, num9);
				Canvas.SetTop(frameworkElement, 0.0);
				TransformGroup transformGroup = frameworkElement.RenderTransform as TransformGroup;
				if (transformGroup == null || transformGroup.Children.Count != 2)
				{
					transformGroup = new TransformGroup();
					transformGroup.Children.Add(new ScaleTransform());
					transformGroup.Children.Add(new TranslateTransform());
					frameworkElement.RenderTransform = transformGroup;
				}
				ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
				TranslateTransform translateTransform = (TranslateTransform)transformGroup.Children[1];
				_dockSlotsByElement.TryGetValue(frameworkElement, out DockSlot value);
				double num11 = (double)scaleTransform.GetValue(ScaleTransform.ScaleXProperty);
				double num12 = (double)translateTransform.GetValue(TranslateTransform.XProperty);
				if (value == null && _dockLiveStrength > 0.005)
				{
					double rowCentre = DockRowCentreX();
					double num13 = dockSlot * (DockMagnifyMax - 1.0) * Math.Clamp(_dockLiveStrength, 0.0, 1.0);
					double num14 = DockBump(num8, count2, rowCentre, dockSlot, _dockLiveX) * num13;
					num11 = (scaleTransform.ScaleY = (scaleTransform.ScaleX = 1.0 + num14 / dockSlot));
				}
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
				double num17 = ((value != null) ? (value.Slide + value.Gap) : num12);
				double num18 = ((reflow && dockPlacedLeft.TryGetValue(appItem, out var failure)) ? (failure - num9) : 0.0);
				if (_suppressDockReflow)
				{
					num17 = 0.0;
					num18 = 0.0;
				}
				DockSlot dockSlot2 = new DockSlot(frameworkElement, num8, scaleTransform, translateTransform)
				{
					Slide = num17 + num18,
					SlideVel = (_suppressDockReflow ? 0.0 : ((value?.SlideVel ?? 0.0) + (value?.GapVel ?? 0.0))),
					Pop = (_suppressDockReflow ? 1.0 : (value?.Pop ?? 1.0)),
					PopVel = (_suppressDockReflow ? 0.0 : (value?.PopVel ?? 0.0)),
					PopS = (value?.PopS ?? 0.32)
				};
				_dockSlots.Add(dockSlot2);
				dictionary[appItem] = num9;
				if (num11 > num5)
				{
					num5 = num11;
				}
				double num19 = Math.Abs(dockSlot2.Slide);
				if (num19 > num6)
				{
					num6 = num19;
				}
				if (Math.Abs(dockSlot2.Pop - 1.0) > num7)
				{
					num7 = Math.Abs(dockSlot2.Pop - 1.0);
				}
				list.Add($"{num8}:s{num11:F2} d{dockSlot2.Slide:F0} p{dockSlot2.Pop:F2}");
			}
		}
		_dockPlacedLeft = dictionary;
		double num20 = stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds;
		if (count2 == 0)
		{
			SetDockPlateScale(1.0);
		}
		_dockSlotsByElement.Clear();
		foreach (DockSlot dockSlot3 in _dockSlots)
		{
			_dockSlotsByElement[dockSlot3.Element] = dockSlot3;
		}
		if (num6 > 0.25 || num5 > 1.002 || num7 > 0.01)
		{
			HandDockRowToSprings();
		}
		if (_dockLiveStrength > 0.005 && _dockSlots.Count > 0)
		{
			ApplyDockMagnification(_dockLiveStrength, _dockLiveX);
		}
		UpdateDockFrost();
		double value3 = stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds - num20;
		App.Log($"[DBG][DOCK-REFLOW] reflow={reflow} n={count2} barW={DockBar.ActualWidth:F0} vis={DockLayer.Visibility} widest={num6:F0} pop={num7:F2} tallest={num5:F2} driving={_dockDriving} tiles={_dockSlots.Count} cost=[gen={totalMilliseconds:F1} slots={num20:F1} frost={value3:F1}] [{string.Join(" | ", list)}]");
		if (count2 > 0 && _dockSlots.Count < count2 && DockBar.ActualWidth > 0.0)
		{
			ArmDockPileProbe();
			Dispatcher.BeginInvoke((Action)(() =>
			{
				LayoutDock(reflow);
			}), DispatcherPriority.Loaded);
		}
		else
		{
			ReleaseDockPileProbe();
		}
	}

	private void UpdateDockFrost()
	{
		if (DockFrostLayer == null || DockTintLayer == null)
		{
			return;
		}
		BitmapSource capture = ((_viewModel.Settings.Backdrop == BackdropKind.Solid) ? null : (BackdropImage.Source as BitmapSource));
		if (capture == null || ActualWidth < 1.0 || ActualHeight < 1.0)
		{
			DockFrostLayer.Visibility = Visibility.Collapsed;
			DockTintLayer.Opacity = 1.0;
			return;
		}
		if (capture != _dockFrostCapture)
		{
			_dockFrostCapture = capture;
			Dispatcher.BeginInvoke((Action)(() =>
			{
				BuildDockFrost(capture);
			}), DispatcherPriority.Background);
		}
		DockFrostLayer.Visibility = Visibility.Visible;
		DockTintLayer.Opacity = 0.3;
		if (_dockFrostSource != null && Resources["DockFrost"] is ImageBrush imageBrush && !(DockFrostLayer.ActualWidth < 1.0) && !(DockFrostLayer.ActualHeight < 1.0))
		{
			imageBrush.ImageSource = _dockFrostSource;
			double pageNumber = imageBrush.ImageSource.Width / ActualWidth;
			double settingIndex = imageBrush.ImageSource.Height / ActualHeight;
			Rect rect = DockRestRectInWindow();
			if (!rect.IsEmpty)
			{
				imageBrush.Viewbox = new Rect((rect.X + 1.0) * pageNumber, (rect.Y + 1.0) * settingIndex, DockFrostLayer.ActualWidth * pageNumber, DockFrostLayer.ActualHeight * settingIndex);
				imageBrush.Viewport = new Rect(0.0, 0.0, DockFrostLayer.ActualWidth, DockFrostLayer.ActualHeight);
			}
		}
	}

	private void BuildDockFrost(BitmapSource capture)
	{
		if (_dockFrostCapture == capture)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			_dockFrostSource = BackdropHelper.BuildFrost(capture, 4, 5);
			App.Log($"[DOCK-FROST] from {capture.PixelWidth}x{capture.PixelHeight} to {_dockFrostSource?.PixelWidth ?? 0}x{_dockFrostSource?.PixelHeight ?? 0} in {stopwatch.Elapsed.TotalMilliseconds:F1}ms");
			UpdateDockFrost();
			ClockBackdropChanged?.Invoke(this, EventArgs.Empty);
			if (IsSettingsOpen)
			{
				UpdateSheetFrost();
			}
		}
	}

	private void UpdateSheetFrost()
	{
		if (SheetFrostLayer == null || SheetTintLayer == null)
		{
			return;
		}
		if (_viewModel.Settings.Backdrop == BackdropKind.Solid || ThemeManager.Effective == AppTheme.HighContrast || SettingsPopup.ActualWidth < 1.0 || SettingsPopup.ActualHeight < 1.0)
		{
			SheetFrostLayer.Visibility = Visibility.Collapsed;
			SheetTintLayer.Visibility = Visibility.Collapsed;
			SettingsPopup.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
			return;
		}
		SettingsPopup.Background = Brushes.Transparent;
		SheetTintLayer.Visibility = Visibility.Visible;
		if (Resources["SheetFrost"] is ImageBrush imageBrush)
		{
			BitmapSource bitmapSource = _dockFrostSource ?? (BackdropImage.Source as BitmapSource);
			if (bitmapSource == null || ActualWidth < 1.0 || ActualHeight < 1.0)
			{
				SheetFrostLayer.Visibility = Visibility.Collapsed;
				SettingsPopup.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
				return;
			}
			double pageNumber = ((SettingsOverlayModal.ActualWidth > 0.0) ? SettingsOverlayModal.ActualWidth : ActualWidth);
			double settingIndex = ((SettingsOverlayModal.ActualHeight > 0.0) ? SettingsOverlayModal.ActualHeight : ActualHeight);
			Rect rect = new Rect((pageNumber - SettingsPopup.ActualWidth) / 2.0, (settingIndex - SettingsPopup.ActualHeight) / 2.0, SettingsPopup.ActualWidth, SettingsPopup.ActualHeight);
			double num3 = bitmapSource.Width / ActualWidth;
			double num4 = bitmapSource.Height / ActualHeight;
			imageBrush.ImageSource = bitmapSource;
			imageBrush.Viewbox = new Rect(rect.X * num3, rect.Y * num4, SettingsPopup.ActualWidth * num3, SettingsPopup.ActualHeight * num4);
			imageBrush.Viewport = new Rect(0.0, 0.0, SettingsPopup.ActualWidth, SettingsPopup.ActualHeight);
			SheetFrostLayer.Visibility = Visibility.Visible;
		}
	}

	private void ScheduleSheetFrost()
	{
		Grid settingsOverlayModal = SettingsOverlayModal;
		if (settingsOverlayModal != null && settingsOverlayModal.Visibility == Visibility.Visible)
		{
			Dispatcher.BeginInvoke(new Action(UpdateSheetFrost), DispatcherPriority.Loaded);
		}
	}

	private void TrackDockFrost(int forMs)
	{
		_frostTrackUntilTicks = DateTime.UtcNow.Ticks + (long)forMs * 10000L;
		if (!_frostTrackHooked)
		{
			_frostTrackHooked = true;
			CompositionTarget.Rendering += OnFrostTrackTick;
		}
	}

	private void OnFrostTrackTick(object? sender, EventArgs e)
	{
		UpdateDockFrost();
		if (DateTime.UtcNow.Ticks > _frostTrackUntilTicks)
		{
			_frostTrackHooked = false;
			CompositionTarget.Rendering -= OnFrostTrackTick;
		}
	}

	private Rect DockBarRectInWindow()
	{
		if (DockBar == null || DockLayer == null || DockBar.ActualWidth <= 0.0)
		{
			if (!_dockShelfArmed)
			{
				return Rect.Empty;
			}
			return DockShelfRectInWindow();
		}
		Point point = DockLayer.TranslatePoint(new Point(0.0, 0.0), this);
		double x = point.X + Math.Max(0.0, (DockLayer.ActualWidth - DockBar.ActualWidth) / 2.0);
		double y = point.Y + Math.Max(0.0, DockLayer.ActualHeight - DockBar.ActualHeight);
		return new Rect(new Point(x, y), new Size(DockBar.ActualWidth, DockBar.ActualHeight));
	}

	private Rect DockShelfRectInWindow()
	{
		double dockShelfWidth = _viewModel.DockShelfWidth;
		double dockBarHeight = _viewModel.DockBarHeight;
		return new Rect((ActualWidth - dockShelfWidth) / 2.0, ActualHeight - 10.0 - dockBarHeight, dockShelfWidth, dockBarHeight);
	}

	private double DockRowCentreX()
	{
		if (!_dockShelfArmed || _dockSlots.Count != 0)
		{
			return DockItems.TranslatePoint(new Point(0.0, 0.0), this).X;
		}
		return ActualWidth / 2.0;
	}

	private Rect DockHitRect(bool fromRow = false)
	{
		Rect rect = DockBarRectInWindow();
		if (rect.IsEmpty)
		{
			return Rect.Empty;
		}
		double pageNumber = 12.0;
		double settingIndex = (fromRow ? (rect.Y - _viewModel.DockIconSize * (DockMagnifyMax - 1.0)) : rect.Y);
		double num3 = ((_dockShelfArmed && _viewModel.DockApps.Count == 0) ? Math.Max(pageNumber, _viewModel.DockSlot * 2.0) : pageNumber);
		return new Rect(rect.X - num3, settingIndex, rect.Width + num3 * 2.0, Math.Max(rect.Height + pageNumber, ActualHeight - settingIndex));
	}

	private bool IsOverDock(Point pos, bool fromRow = false)
	{
		Rect rect = DockHitRect(fromRow);
		if (!rect.IsEmpty)
		{
			return rect.Contains(pos);
		}
		return false;
	}

	private int DockInsertIndexAt(Point pos)
	{
		int count = _viewModel.DockApps.Count;
		if (DockItems == null || count == 0)
		{
			return count;
		}
		double x = DockItems.TranslatePoint(new Point(0.0, 0.0), this).X;
		double pageNumber = (pos.X - x) / _viewModel.DockSlot + (double)count / 2.0;
		if (_dockGapSlot >= 0 && _dockGapRoom != 0)
		{
			int settingIndex = Math.Clamp(_dockGapSlot, 0, count);
			if (pageNumber < (double)settingIndex - 0.5 - 0.25)
			{
				return Math.Clamp((int)Math.Round(pageNumber, MidpointRounding.AwayFromZero), 0, count);
			}
			if (pageNumber > (double)settingIndex + 0.5 + 0.25)
			{
				return Math.Clamp((int)Math.Round(pageNumber, MidpointRounding.AwayFromZero), 0, count);
			}
			return settingIndex;
		}
		return Math.Clamp((int)Math.Round(pageNumber, MidpointRounding.AwayFromZero), 0, count);
	}

	private bool TryGetDockItemAt(Point pos, out FrameworkElement? element, out AppItem? app)
	{
		element = null;
		app = null;
		if (DockLayer == null || DockLayer.Visibility != Visibility.Visible)
		{
			return false;
		}
		for (int pageNumber = _dockSlots.Count - 1; pageNumber >= 0; pageNumber--)
		{
			DockSlot dockSlot = _dockSlots[pageNumber];
			FrameworkElement element2 = dockSlot.Element;
			int item = dockSlot.Item;
			Point point = element2.TranslatePoint(new Point(0.0, 0.0), this);
			Point point2 = element2.TranslatePoint(new Point(element2.ActualWidth, element2.ActualHeight), this);
			if (new Rect(point, point2).Contains(pos))
			{
				element = element2;
				app = ((item < _viewModel.DockApps.Count) ? _viewModel.DockApps[item] : null);
				return app != null;
			}
		}
		return false;
	}

	private static double DockBump(int index, int count, double rowCentre, double slot, double pointerX)
	{
		double pageNumber = rowCentre + ((double)index - (double)(count - 1) / 2.0) * slot;
		return DockMotion.Bump((pointerX - pageNumber) / slot);
	}

	private static double DockLawWidth(int count)
	{
		int pageNumber = Math.Min(Math.Max(0, count - 1), (int)Math.Ceiling(2.4));
		double settingIndex = 0.0;
		for (int i = -pageNumber; i <= pageNumber; i++)
		{
			if ((double)Math.Abs(i) < 2.4)
			{
				settingIndex += Math.Cos((double)Math.Abs(i) / 2.4 * (Math.PI / 2.0));
			}
		}
		return settingIndex;
	}

	private static double DockLawNorm(double rawSum, int count, double grow)
	{
		return DockMotion.Normalize(rawSum, count, grow);
	}

	private void SetDockPlateScale(double sx)
	{
		if (DockPlateEdge != null && !(sx <= 0.0) && (!(_dockPlateScaleWritten > 0.0) || !(Math.Abs(_dockPlateScaleWritten - sx) < 0.001)))
		{
			_dockPlateScaleWritten = sx;
			DockPlateScale.ScaleX = sx;
			DockFrostLayer.RadiusX = 19.0 / sx;
			DockTintLayer.RadiusX = 19.0 / sx;
			DockPlateEdge.RadiusX = 19.5 / sx;
		}
	}

	private void SetDockShelfScale(double sx)
	{
		if (DockShelfEdge != null && !(sx <= 0.0))
		{
			DockShelfScale.ScaleX = sx;
			DockShelfTint.RadiusX = 20.0 / sx;
			DockShelfEdge.RadiusX = 19.5 / sx;
		}
	}

	private void ApplyDockMagnification(double strength, double crownX)
	{
		int count = _dockSlots.Count;
		int count2 = _viewModel.DockApps.Count;
		if (count == 0 || DockItems == null || DockBar == null)
		{
			return;
		}
		double slot = _viewModel.DockSlot;
		double rowCentre = DockItems.TranslatePoint(new Point(0.0, 0.0), this).X;
		strength = Math.Clamp(strength, 0.0, 1.0);
		double grow = slot * (DockMagnifyMax - 1.0) * strength;
		int gap = _dockGapSlot;
		bool reorder = gap >= 0 && _dockGapLawRoom == 0 && _dockGhostSlot >= 0;
		bool remap = _dockGapLawRoom != 0 && _dockGapLawCount > 0 && _dockSlots.Count != _dockGapLawCount;
		int lawCount = (remap ? _dockGapLawCount : Math.Max(1, count2));
		double pageNumber = _dockGapLawRoom switch
		{
			1 => Math.Clamp(_dockLiveGapRoom, 0.0, 1.0), 
			-1 => Math.Clamp(0.0 - _dockLiveGapRoom, 0.0, 1.0), 
			_ => 1.0, 
		};
		double settingIndex = ((remap && _dockGapLawRoom == 1 && gap >= 0) ? (BumpAt(gap) * pageNumber) : 0.0);
		double num3 = settingIndex;
		for (int i = 0; i < count; i++)
		{
			double num4 = DockBump(_dockSlots[i].Item, Math.Max(1, count2), rowCentre, slot, crownX) * grow;
			double num5 = BumpOf(_dockSlots[i].Item);
			double num6 = (remap ? (num4 + (num5 - num4) * pageNumber) : num5);
			num3 += num6;
		}
		double num7 = DockLawNorm(num3, lawCount, grow);
		num3 *= num7;
		double num8 = settingIndex * num7;
		double num9 = 0.0;
		bool validated = false;
		IList<DockSlot> list;
		if (!reorder)
		{
			IList<DockSlot> dockSlots = _dockSlots;
			list = dockSlots;
		}
		else
		{
			IList<DockSlot> dockSlots = _dockSlots.OrderBy((DockSlot s) => MappedIndex(s.Item)).ToArray();
			list = dockSlots;
		}
		IList<DockSlot> list2 = list;
		for (int num10 = 0; num10 < count; num10++)
		{
			DockSlot dockSlot = list2[num10];
			if ((!validated & remap) && _dockGapLawRoom == 1 && gap >= 0 && dockSlot.Item >= gap)
			{
				num9 += num8;
				validated = true;
			}
			double num11 = DockBump(dockSlot.Item, Math.Max(1, count2), rowCentre, slot, crownX) * grow;
			double num12 = BumpOf(dockSlot.Item);
			double num13 = (remap ? (num11 + (num12 - num11) * pageNumber) : num12) * num7;
			dockSlot.Shift.X = num9 - num3 / 2.0 + num13 / 2.0 + dockSlot.Slide + dockSlot.Gap;
			num9 += num13;
			double num14 = (1.0 + num13 / slot) * dockSlot.Pop;
			dockSlot.Scale.ScaleX = num14;
			dockSlot.Scale.ScaleY = num14;
			double num15 = ((dockSlot.Item == _dockGhostSlot) ? 0.0 : 1.0);
			if (dockSlot.Element.Opacity != num15)
			{
				dockSlot.Element.Opacity = num15;
			}
		}
		double dockBaseWidth = _viewModel.DockBaseWidth;
		double num16 = _dockLiveGapRoom * slot;
		SetDockPlateScale((dockBaseWidth > 1.0) ? (1.0 + (num3 + num16) / dockBaseWidth) : 1.0);
		double BumpAt(int index)
		{
			return DockBump(index, lawCount, rowCentre, slot, crownX) * grow;
		}
		double BumpOf(int item)
		{
			if (reorder)
			{
				return BumpAt(MappedIndex(item));
			}
			double result;
			if (remap)
			{
				result = ((gap >= 0) ? (_dockGapLawRoom switch
				{
					1 => BumpAt((item >= gap) ? (item + 1) : item), 
					-1 => (item == gap) ? 0.0 : BumpAt((item > gap) ? (item - 1) : item), 
					_ => BumpAt(item), 
				}) : BumpAt(item));
			}
			else
			{
				result = BumpAt(item);
			}
			return result;
		}
		int MappedIndex(int item)
		{
			if (!reorder)
			{
				return item;
			}
			if (item == _dockGhostSlot)
			{
				return gap;
			}
			int num17 = ((item < _dockGhostSlot) ? item : (item - 1));
			if (num17 < gap)
			{
				return num17;
			}
			return num17 + 1;
		}
	}

	private Rect DockRestRectInWindow()
	{
		if (DockBar == null || DockItems == null || _viewModel.DockApps.Count == 0)
		{
			return Rect.Empty;
		}
		double x = DockItems.TranslatePoint(new Point(0.0, 0.0), this).X;
		double y = DockBar.TranslatePoint(new Point(0.0, DockBar.ActualHeight), this).Y;
		double dockBaseWidth = _viewModel.DockBaseWidth;
		double dockBarHeight = _viewModel.DockBarHeight;
		return new Rect(x - dockBaseWidth / 2.0, y - dockBarHeight, dockBaseWidth, dockBarHeight);
	}

	private bool IsOverPageDots(Point pos)
	{
		if (PageDots == null || PageDots.Visibility != Visibility.Visible || _viewModel.TotalPages <= 1)
		{
			return false;
		}
		try
		{
			Point point = PageDots.TranslatePoint(new Point(0.0, 0.0), this);
			return new Rect(point.X - 24.0, point.Y - 14.0, PageDots.ActualWidth + 48.0, PageDots.ActualHeight + 28.0).Contains(pos);
		}
		catch
		{
			return false;
		}
	}

	private void UpdateDockHover(Point pos, bool withTip = true)
	{
		if (DockBar == null || !_viewModel.IsDockVisible || _dockSlots.Count == 0)
		{
			return;
		}
		if (!_viewModel.Settings.EnableDockMagnification)
		{
			RestDock("magnify-disabled");
			if (withTip)
			{
				SetDockTipApp(DockAppAt(pos.X));
			}
			return;
		}
		if (IsOverPageDots(pos))
		{
			RestDock("over-dots");
			SetDockTipApp(null);
			return;
		}
		Rect rect = DockRestRectInWindow();
		if (rect.IsEmpty)
		{
			return;
		}
		double pageNumber = _viewModel.DockSlot * 1.2;
		if (pos.X < rect.X - pageNumber || pos.X > rect.Right + pageNumber)
		{
			RestDock("out-x");
			return;
		}
		double settingIndex = DockMagnifyReach * 1.25;
		double num3 = ((pos.Y >= rect.Y) ? 1.0 : ((pos.Y - (rect.Y - settingIndex)) / settingIndex));
		if (num3 <= 0.001)
		{
			RestDock("out-y");
			return;
		}
		_dockTargetStrength = num3;
		_dockTargetX = pos.X;
		if (withTip)
		{
			SetDockTipApp(DockAppAt(pos.X));
		}
		if (!_dockDriving)
		{
			StartDockHover();
		}
		else
		{
			EnsureDockFrames();
		}
	}

	private AppItem? DockAppAt(double x)
	{
		int count = _viewModel.DockApps.Count;
		if (count == 0 || DockItems == null)
		{
			return null;
		}
		double dockSlot = _viewModel.DockSlot;
		double x2 = DockItems.TranslatePoint(new Point(0.0, 0.0), this).X;
		int index = Math.Clamp((int)Math.Round((x - x2) / dockSlot + (double)(count - 1) / 2.0, MidpointRounding.AwayFromZero), 0, count - 1);
		if (Math.Abs(x - (x2 + DockTileOffset(index))) > dockSlot * 0.6)
		{
			return null;
		}
		return _viewModel.DockApps[index];
	}

	private double DockTileOffset(int index)
	{
		double pageNumber = 0.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			if (dockSlot.Item == index)
			{
				pageNumber = dockSlot.Shift.X;
				break;
			}
		}
		return ((double)index - (double)(_viewModel.DockApps.Count - 1) / 2.0) * _viewModel.DockSlot + pageNumber;
	}

	private bool TryGetDockSlotLive(int index, int count, out Rect live, out double scale)
	{
		live = DockBarRectInWindow();
		scale = 1.0;
		if (live.IsEmpty || DockItems == null || count <= 0 || index < 0 || index >= count)
		{
			return false;
		}
		double dockSlot = _viewModel.DockSlot;
		double pageNumber = DockRowCentreX();
		double settingIndex = dockSlot * (DockMagnifyMax - 1.0) * Math.Clamp(_dockLiveStrength, 0.0, 1.0);
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		for (int i = 0; i < count; i++)
		{
			double num6 = DockBump(i, count, pageNumber, dockSlot, _dockLiveX) * settingIndex;
			num3 += num6;
			if (i < index)
			{
				num4 += num6;
			}
			else if (i == index)
			{
				num5 = num6;
			}
		}
		double num7 = DockLawNorm(num3, count, settingIndex);
		num3 *= num7;
		num4 *= num7;
		num5 *= num7;
		scale = 1.0 + num5 / dockSlot;
		double num8 = pageNumber + ((double)index - (double)(count - 1) / 2.0) * dockSlot + num4 - num3 / 2.0 + num5 / 2.0;
		double num9 = _viewModel.DockIconSize * scale;
		live = new Rect(num8 - num9 / 2.0, live.Bottom - 14.0 - num9, num9, num9);
		return true;
	}

	private double DockTileRise(int index)
	{
		double pageNumber = 1.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			if (dockSlot.Item == index)
			{
				pageNumber = dockSlot.Scale.ScaleY;
				break;
			}
		}
		return Math.Max(0.0, _viewModel.DockIconSize * pageNumber - (_viewModel.DockBarHeight - 14.0));
	}

	private void PlaceDockTip(int index)
	{
		if (DockTip != null && DockTipTranslate != null)
		{
			DockTipTranslate.X = _dockTipLiveX;
			DockTipTranslate.Y = 0.0 - (DockTip.ActualHeight + 8.0 + DockTileRise(index));
		}
	}

	private void SetDockTipApp(AppItem? app)
	{
		if (app == _dockTipApp)
		{
			return;
		}
		_dockTipApp = app;
		if (DockTip == null || DockTipText == null)
		{
			return;
		}
		if (app == null)
		{
			FadeDockTip(0.0);
			return;
		}
		DockTipText.Text = app.DisplayName;
		int pageNumber = _viewModel.DockApps.IndexOf(app);
		if (pageNumber >= 0 && DockTip.Opacity <= 0.01)
		{
			_dockTipLiveX = DockTileOffset(pageNumber);
			_dockTipXVel = 0.0;
			PlaceDockTip(pageNumber);
		}
		FadeDockTip(1.0);
	}

	private void FadeDockTip(double to)
	{
		if (DockTip != null)
		{
			bool validated = to > DockTip.Opacity;
			DoubleAnimation animation = new DoubleAnimation(DockTip.Opacity, to, TimeSpan.FromMilliseconds(validated ? 130 : 110))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = (validated ? EasingMode.EaseOut : EasingMode.EaseIn)
				}
			};
			DockTip.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	private void StartDockHover()
	{
		AdoptDockRow();
		_dockDriving = true;
		_dockLiveX = _dockTargetX;
		_dockLiveXVel = 0.0;
		_dockLiveTicks = 0L;
		App.Log($"[DBG][DOCK-START] from={_dockLiveStrength:F2}");
		EnsureDockFrames();
	}

	private void AdoptDockRow()
	{
		double pageNumber = 1.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			if (dockSlot.Pop >= 0.999 && dockSlot.Scale.ScaleX > pageNumber)
			{
				pageNumber = dockSlot.Scale.ScaleX;
			}
		}
		_dockLiveStrength = Math.Clamp((pageNumber - 1.0) / (DockMagnifyMax - 1.0), 0.0, 1.0);
		_dockLiveStrengthVel = 0.0;
	}

	private void HandDockRowToSprings()
	{
		if (!_dockDriving)
		{
			_dockTargetStrength = ((_dockLiveStrength > 0.01) ? 1.0 : 0.0);
			_dockLiveX = _dockTargetX;
			_dockLiveXVel = 0.0;
			_dockLiveTicks = 0L;
			AdoptDockRow();
			_dockDriving = true;
		}
		EnsureDockFrames();
	}

	private void EnsureDockFrames()
	{
		if (!_dockRenderHooked)
		{
			_dockRenderHooked = true;
			CompositionTarget.Rendering += OnDockRenderTick;
		}
	}

	private void OnDockRenderTick(object? sender, EventArgs e)
	{
		long ticks = ((RenderingEventArgs)e).RenderingTime.Ticks;
		if (_dockLiveTicks == ticks)
		{
			return;
		}
		double pageNumber = ((_dockLiveTicks == 0L) ? (1.0 / 60.0) : ((double)(ticks - _dockLiveTicks) / 10000.0 / 1000.0));
		_dockLiveTicks = ticks;
		if (pageNumber <= 0.0)
		{
			pageNumber = 1.0 / 60.0;
		}
		if (pageNumber > 1.0 / 15.0)
		{
			pageNumber = 1.0 / 15.0;
		}
		bool validated = false;
		if (_dockDriving)
		{
			_dockLiveStrength = Follow(_dockLiveStrength, _dockTargetStrength, ref _dockLiveStrengthVel, pageNumber, 0.16);
			_dockLiveX = Follow(_dockLiveX, _dockTargetX, ref _dockLiveXVel, pageNumber, 0.045);
			validated = _dockLiveStrength != _dockTargetStrength || _dockLiveStrengthVel != 0.0 || _dockLiveX != _dockTargetX || _dockLiveXVel != 0.0;
		}
		foreach (DockSlot dockSlot2 in _dockSlots)
		{
			if (dockSlot2.Slide != 0.0 || dockSlot2.SlideVel != 0.0)
			{
				dockSlot2.Slide = Follow(dockSlot2.Slide, 0.0, ref dockSlot2.SlideVel, pageNumber, 0.32);
				validated = true;
			}
			if (dockSlot2.Pop != 1.0 || dockSlot2.PopVel != 0.0)
			{
				dockSlot2.Pop = Follow(dockSlot2.Pop, 1.0, ref dockSlot2.PopVel, pageNumber, dockSlot2.PopS);
				validated = true;
			}
			if (dockSlot2.Gap != dockSlot2.GapTo || dockSlot2.GapVel != 0.0)
			{
				dockSlot2.Gap = Follow(dockSlot2.Gap, dockSlot2.GapTo, ref dockSlot2.GapVel, pageNumber, 0.28);
				validated = true;
			}
		}
		if (_dockLiveGapRoom != (double)_dockGapRoom || _dockLiveGapRoomVel != 0.0)
		{
			_dockLiveGapRoom = Follow(_dockLiveGapRoom, _dockGapRoom, ref _dockLiveGapRoomVel, pageNumber, 0.28);
			validated = true;
		}
		ApplyDockMagnification(_dockLiveStrength, _dockLiveX);
		if (_landProbeFrames > 0)
		{
			_landProbeFrames--;
			DockSlot dockSlot = _dockSlots.FirstOrDefault((DockSlot s) => s.Item == _landProbeIndex);
			int value = -1;
			double settingIndex = 1.0;
			for (int num3 = 0; num3 < _dockSlots.Count; num3++)
			{
				if (_dockSlots[num3].Scale.ScaleX > settingIndex)
				{
					settingIndex = _dockSlots[num3].Scale.ScaleX;
					value = num3;
				}
			}
			App.Log($"[DBG][DOCK-LAND] f={_landProbeFrames} idx={_landProbeIndex} n={_dockSlots.Count} scale={dockSlot?.Scale.ScaleX.ToString("F3") ?? "-"} pop={dockSlot?.Pop.ToString("F3") ?? "-"} slide={dockSlot?.Slide.ToString("F1") ?? "-"} d{dockSlot?.Gap.ToString("F1") ?? "-"} crown={value}@{settingIndex:F3} law={_dockGapLawRoom}/{_dockGapLawCount} card={FloatingDragScale.ScaleX:F3} op={FloatingDragCard.Opacity:F2} vis={FloatingDragCard.Visibility} live={_dockLiveStrength:F2}");
		}
		if (_dockTipApp != null)
		{
			int num4 = _viewModel.DockApps.IndexOf(_dockTipApp);
			if (num4 < 0)
			{
				SetDockTipApp(null);
			}
			else
			{
				_dockTipLiveX = Follow(_dockTipLiveX, DockTileOffset(num4), ref _dockTipXVel, pageNumber, 0.045);
				PlaceDockTip(num4);
				validated |= _dockTipLiveX != DockTileOffset(num4) || _dockTipXVel != 0.0;
			}
		}
		if (!validated && !DockGapArmed)
		{
			if (_dockTargetStrength > 0.0)
			{
				_dockRenderHooked = false;
				_dockLiveTicks = 0L;
				CompositionTarget.Rendering -= OnDockRenderTick;
				return;
			}
			App.Log($"[DBG][DOCK-REST] x={_dockLiveX:F0} slots={_dockSlots.Count}");
			DockAtRest();
		}
	}

	private static double Follow(double current, double target, ref double velocity, double dt, double smoothTime)
	{
		return DockMotion.Follow(current, target, ref velocity, dt, smoothTime);
	}

	private void RestDock(string why = "")
	{
		SetDockTipApp(null);
		if (_dockDriving && _dockTargetStrength != 0.0)
		{
			_dockTargetStrength = 0.0;
			EnsureDockFrames();
			App.Log($"[DBG][DOCK-RELEASE] why={why} live={_dockLiveStrength:F2} vel={_dockLiveStrengthVel:F2} x={_dockLiveX - _dockTargetX:F0}");
		}
	}

	private void DockAtRest()
	{
		StopDockFollower();
		_dockLiveStrength = 0.0;
		_dockLiveStrengthVel = 0.0;
		_dockLiveXVel = 0.0;
		foreach (DockSlot dockSlot in _dockSlots)
		{
			dockSlot.Slide = 0.0;
			dockSlot.SlideVel = 0.0;
			dockSlot.Pop = 1.0;
			dockSlot.PopVel = 0.0;
			dockSlot.Gap = 0.0;
			dockSlot.GapVel = 0.0;
			dockSlot.GapTo = 0.0;
			dockSlot.Scale.ScaleX = 1.0;
			dockSlot.Scale.ScaleY = 1.0;
			dockSlot.Shift.X = 0.0;
		}
		CloseDockGap();
		SetDockPlateScale(1.0);
	}

	private void StopDockFollower()
	{
		_dockDriving = false;
		_dockLiveTicks = 0L;
		if (_dockRenderHooked)
		{
			_dockRenderHooked = false;
			CompositionTarget.Rendering -= OnDockRenderTick;
		}
	}

	private void UpdateDockDragFeedback(Point pos)
	{
		AppItem draggedApp = _draggedApp;
		if (draggedApp != null && DockBar != null)
		{
			SetDockDragGap(pos);
			bool validated = IsOverDock(pos, _dragFromDock);
			if (_dragFromDock && _viewModel.IsSearchActive && !validated)
			{
				double dockIconSize = _viewModel.DockIconSize;
				ShowDockHint("搜索结果中不能移出 Dock", new Rect(pos.X - dockIconSize / 2.0, pos.Y - dockIconSize / 2.0, dockIconSize, dockIconSize));
			}
			else if (_dragFromDock || !validated)
			{
				HideDockDragFeedback();
			}
			else if (draggedApp.IsFolder || draggedApp.IsInFolder)
			{
				ShowDockHint("文件夹不能加入 Dock", DockBarRectInWindow());
			}
			else if (_viewModel.DockApps.Count >= _viewModel.MaxDockItems)
			{
				ShowDockHint("Dock 空间已满，可缩小图标后继续添加", DockBarRectInWindow());
			}
			else
			{
				HideDockDragFeedback();
			}
		}
	}

	private void ShowDockHint(string text, Rect ring)
	{
		if (!ring.IsEmpty)
		{
			bool validated = _dockHintLabel != text;
			_dockHintLabel = text;
			_foldHintTarget = null;
			FoldHintText.Text = text;
			FoldHighlight.Visibility = Visibility.Collapsed;
			FoldHint.Visibility = Visibility.Visible;
			DragCanvas.UpdateLayout();
			double pageNumber = ring.Y - FoldHint.ActualHeight - 8.0;
			if (pageNumber < 4.0)
			{
				pageNumber = ring.Bottom + 8.0;
			}
			Canvas.SetLeft(length: Math.Clamp(ring.X + ring.Width / 2.0 - FoldHint.ActualWidth / 2.0, 4.0, Math.Max(4.0, ActualWidth - FoldHint.ActualWidth - 4.0)), element: FoldHint);
			Canvas.SetTop(FoldHint, pageNumber);
			FoldHintArrow.Data = ((pageNumber < ring.Y) ? ArrowDown : ArrowUp);
			if (validated)
			{
				FoldHintBadge.Visibility = Visibility.Collapsed;
				FoldHintText.Margin = new Thickness(0.0, 0.0, 1.0, 0.0);
				FoldHint.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(110.0)));
				FoldHintShift.BeginAnimation(TranslateTransform.YProperty, SpringTo(7.0, 0.0, 230, 0.2));
				FoldHighlightScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				FoldHighlightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				FoldHighlight.BeginAnimation(UIElement.OpacityProperty, null);
			}
		}
	}

	private void HideDockDragFeedback()
	{
		if (_dockHintLabel.Length != 0)
		{
			HideFoldHint();
		}
	}

	private void ArmDockShelf(AppItem app)
	{
		if (_dockShelfArmed || DockShelf == null)
		{
			return;
		}
		_dockShelfArmed = true;
		_dockPlateRetiring = false;
		_dockShelfLit = false;
		DockShelfIcon.Source = app.Icon;
		DockShelf.BeginAnimation(UIElement.OpacityProperty, null);
		DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		SetDockShelfScale(1.0);
		DockShelf.Visibility = Visibility.Visible;
		DockShelf.Opacity = 0.0;
		DockShelfTranslate.Y = _viewModel.DockBarHeight + 18.0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		_viewModel.DockShelfReserved = true;
		UpdateLayout();
		double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
		DockShelf.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140.0)));
		DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, SpringTo(_viewModel.DockBarHeight + 18.0, 0.0, 260, 0.1));
		double dockShelfWidth = _viewModel.DockShelfWidth;
		double dockBarHeight = _viewModel.DockBarHeight;
		Rect rect = new Rect((ActualWidth - dockShelfWidth) / 2.0, ActualHeight - 10.0 - dockBarHeight, dockShelfWidth, dockBarHeight);
		Point point = DockShelf.TranslatePoint(new Point(0.0, 0.0), this);
		point.Y -= DockShelfTranslate.Y;
		Rect rect2 = ((PageDots != null && PageDots.Visibility == Visibility.Visible) ? new Rect(PageDots.TranslatePoint(new Point(0.0, 0.0), this), new Size(PageDots.ActualWidth, PageDots.ActualHeight)) : Rect.Empty);
		Rect rect3 = DockHitRect();
		StringBuilder stringBuilder = new StringBuilder();
		if (PageDots != null)
		{
			PageDots.UpdateLayout();
			for (int i = 0; i < _viewModel.PageIndicators.Count && PageDots.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement; i++)
			{
				Point point2 = frameworkElement.TranslatePoint(new Point(0.0, 0.0), this);
				bool validated = i < _viewModel.PageIndicators.Count && _viewModel.PageIndicators[i].IsSelected;
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 6, stringBuilder2);
				handler.AppendLiteral("[");
				handler.AppendFormatted(validated ? "*" : " ");
				handler.AppendFormatted(i);
				handler.AppendLiteral(":(");
				handler.AppendFormatted(point2.X, "F0");
				handler.AppendLiteral(",");
				handler.AppendFormatted(point2.Y, "F0");
				handler.AppendLiteral(",");
				handler.AppendFormatted(frameworkElement.ActualWidth, "F0");
				handler.AppendLiteral("x");
				handler.AppendFormatted(frameworkElement.ActualHeight, "F0");
				handler.AppendLiteral(")]");
				stringBuilder2.Append(ref handler);
			}
		}
		App.Log($"[DBG][DOCK-DOTS] {stringBuilder}");
		App.Log($"[DBG][DOCK-SHELF] arm relayout={totalMilliseconds:F1}ms win={ActualWidth:F0}x{ActualHeight:F0} derived=({rect.X:F0},{rect.Y:F0},{rect.Width:F0}x{rect.Height:F0}) arranged=({point.X:F0},{point.Y:F0}) dots=({rect2.X:F0},{rect2.Y:F0},{rect2.Width:F0}x{rect2.Height:F0}) band=({rect3.X:F0},{rect3.Y:F0},{rect3.Width:F0}x{rect3.Height:F0})");
	}

	private void DisarmDockShelf(bool instant = false)
	{
		if (!_dockShelfArmed || DockShelf == null)
		{
			return;
		}
		_dockShelfArmed = false;
		_dockShelfLit = false;
		_viewModel.DockShelfReserved = false;
		App.Log($"[DBG][DOCK-SHELF] disarm instant={instant} n={_viewModel.DockApps.Count} vis={DockLayer.Visibility}");
		if (instant)
		{
			DockShelf.BeginAnimation(UIElement.OpacityProperty, null);
			DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			DockShelf.Opacity = 0.0;
			DockShelfTranslate.Y = 0.0;
			DockShelf.Visibility = Visibility.Collapsed;
			DockShelfIcon.Source = null;
			return;
		}
		DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, SpringTo(0.0, _viewModel.DockBarHeight + 18.0, 200, 0.0));
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120.0));
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			if (!_dockShelfArmed)
			{
				DockShelf.BeginAnimation(UIElement.OpacityProperty, null);
				DockShelf.Visibility = Visibility.Collapsed;
				DockShelfIcon.Source = null;
			}
		};
		DockShelf.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void RetireDockPlate(AppItem? leaving)
	{
		if (DockShelf == null || DockShelfSlot == null)
		{
			return;
		}
		_dockPlateRetiring = true;
		DockShelf.BeginAnimation(UIElement.OpacityProperty, null);
		DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		DockShelfSlot.BeginAnimation(UIElement.OpacityProperty, null);
		DockShelfSlot.Opacity = 1.0;
		DockShelfIcon.Source = leaving?.Icon;
		SetDockShelfScale(Math.Max(1.0, DockPlateScale.ScaleX));
		DockShelfScale.ScaleY = 1.0;
		DockShelf.Visibility = Visibility.Visible;
		DockShelf.Opacity = 1.0;
		DockShelfTranslate.Y = 0.0;
		DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, _viewModel.DockBarHeight + 18.0, TimeSpan.FromMilliseconds(320.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			}
		});
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(230.0))
		{
			BeginTime = TimeSpan.FromMilliseconds(170.0),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			}
		};
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			if (_dockPlateRetiring && !_dockShelfArmed)
			{
				_dockPlateRetiring = false;
				DockShelf.BeginAnimation(UIElement.OpacityProperty, null);
				DockShelfTranslate.BeginAnimation(TranslateTransform.YProperty, null);
				DockShelf.Opacity = 0.0;
				DockShelfTranslate.Y = 0.0;
				SetDockShelfScale(1.0);
				DockShelf.Visibility = Visibility.Collapsed;
				DockShelfIcon.Source = null;
			}
		};
		DockShelf.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void SetDockShelfLit(bool lit)
	{
		if (lit != _dockShelfLit && DockShelfSlot != null)
		{
			_dockShelfLit = lit;
			DockShelfSlot.BeginAnimation(UIElement.OpacityProperty, null);
			DoubleAnimation animation = new DoubleAnimation(lit ? 1.0 : 0.55, TimeSpan.FromMilliseconds(110.0));
			DockShelfSlot.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	private Task<Point> FlyCardIntoDockAsync(int index, bool intoHole = false, int durationMs = 280)
	{
		int count = _viewModel.DockApps.Count;
		int pageNumber = (intoHole ? (count + 1) : Math.Max(1, count));
		int index2 = Math.Clamp(index, 0, pageNumber - 1);
		if (!TryGetDockSlotLive(index2, pageNumber, out var live, out var _) || live.IsEmpty)
		{
			FloatingDragCard.Visibility = Visibility.Collapsed;
			return Task.FromResult<Point>(default);
		}
		FloatingDragCard.BeginAnimation(Canvas.LeftProperty, null);
		FloatingDragCard.BeginAnimation(Canvas.TopProperty, null);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		_dockFly = new DockFlight
		{
			Start = new Point(Canvas.GetLeft(FloatingDragCard), Canvas.GetTop(FloatingDragCard)),
			StartScale = FloatingDragScale.ScaleX,
			Index = index2,
			Count = pageNumber,
			Token = _dragLandingToken,
			StartTicks = DateTime.UtcNow.Ticks,
			DurationMs = durationMs
		};
		_flyProbeFrames = 26;
		EnsureDockFlyFrames();
		return _dockFly.Done.Task;
	}

	private void EnsureDockFlyFrames()
	{
		if (!_dockFlyHooked)
		{
			_dockFlyHooked = true;
			CompositionTarget.Rendering += OnDockFlyTick;
		}
	}

	private void StopDockFlyFrames()
	{
		_dockFlyHooked = false;
		CompositionTarget.Rendering -= OnDockFlyTick;
	}

	private void OnDockFlyTick(object? sender, EventArgs e)
	{
		DockFlight dockFly = _dockFly;
		if (dockFly == null)
		{
			StopDockFlyFrames();
			return;
		}
		if (dockFly.Token != _dragLandingToken)
		{
			_dockFly = null;
			StopDockFlyFrames();
			dockFly.Done.TrySetResult(default);
			return;
		}
		double pageNumber = (double)(DateTime.UtcNow.Ticks - dockFly.StartTicks) / (double)(10000L * (long)dockFly.DurationMs);
		if (pageNumber < 0.0)
		{
			pageNumber = 0.0;
		}
		if (pageNumber > 1.0)
		{
			pageNumber = 1.0;
		}
		double settingIndex = 1.0 - Math.Pow(1.0 - pageNumber, 3.0);
		if (!TryGetDockSlotLive(dockFly.Index, dockFly.Count, out var live, out var scale) || live.IsEmpty)
		{
			_dockFly = null;
			StopDockFlyFrames();
			FloatingDragCard.Visibility = Visibility.Collapsed;
			dockFly.Done.TrySetResult(default);
			return;
		}
		double num3;
		if (FloatingDragImage.Width > 0.0)
		{
			num3 = FloatingDragImage.Width;
		}
		else
		{
			num3 = (_dragFromDock ? _viewModel.DockIconSize : ((double)_viewModel.IconSize));
		}
		double num4 = _viewModel.DockIconSize * scale / num3;
		double num5 = live.X + live.Width / 2.0 - FloatingDragCard.Width / 2.0;
		double num6 = live.Y + live.Height / 2.0 - FloatingDragCard.Height / 2.0;
		double num7 = dockFly.StartScale + (num4 - dockFly.StartScale) * settingIndex;
		if (_flyProbeFrames > 0)
		{
			_flyProbeFrames--;
			double num8 = 1.0;
			foreach (DockSlot dockSlot in _dockSlots)
			{
				if (dockSlot.Scale.ScaleX > num8)
				{
					num8 = dockSlot.Scale.ScaleX;
				}
			}
			App.Log($"[DBG][DOCK-FLY] p={pageNumber:F2} card={num7:F3} icon={(double)_viewModel.IconSize * num7:F1} aim={_viewModel.DockIconSize * scale:F1} rowMax={num8:F2} live={_dockLiveStrength:F2} tgt={_dockTargetStrength:F2} liveX={_dockLiveX:F0} idx={dockFly.Index}/{dockFly.Count} gap={_dockGapSlot}/{_dockGapRoom}/{_dockGapLawRoom}");
		}
		FloatingDragCard.BeginAnimation(Canvas.LeftProperty, null);
		FloatingDragCard.BeginAnimation(Canvas.TopProperty, null);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		FloatingDragScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		Canvas.SetLeft(FloatingDragCard, dockFly.Start.X + (num5 - dockFly.Start.X) * settingIndex);
		Canvas.SetTop(FloatingDragCard, dockFly.Start.Y + (num6 - dockFly.Start.Y) * settingIndex);
		FloatingDragScale.ScaleX = num7;
		FloatingDragScale.ScaleY = num7;
		if (!(pageNumber < 1.0))
		{
			Point result = new Point(Canvas.GetLeft(FloatingDragCard) + FloatingDragCard.Width / 2.0, Canvas.GetTop(FloatingDragCard) + FloatingDragCard.Height / 2.0);
			_dockFly = null;
			StopDockFlyFrames();
			dockFly.Done.TrySetResult(result);
		}
	}

	private void RetireDragCard(int fadeMs)
	{
		FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, null);
		FloatingDragCard.Opacity = 1.0;
		if (fadeMs <= 0)
		{
			FloatingDragCard.Visibility = Visibility.Collapsed;
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(fadeMs))
		{
			FillBehavior = FillBehavior.HoldEnd
		};
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, null);
			FloatingDragCard.Opacity = 1.0;
			FloatingDragCard.Visibility = Visibility.Collapsed;
		};
		FloatingDragCard.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private Task WaitForDockLayout()
	{
		return Dispatcher.InvokeAsync(() =>
		{
		}, DispatcherPriority.Loaded).Task;
	}

	private void PopDockSlot(int index, double entryShift = 0.0, double popFrom = 0.72)
	{
		if (index >= 0 && index < _dockSlots.Count)
		{
			DockSlot dockSlot = _dockSlots[index];
			dockSlot.Pop = popFrom;
			dockSlot.PopVel = 0.0;
			dockSlot.PopS = ((Math.Abs(popFrom - 1.0) > 0.001) ? 0.32 : 0.32);
			dockSlot.Slide = entryShift;
			dockSlot.SlideVel = 0.0;
			HandDockRowToSprings();
		}
	}

	private void LandDockSlot(int index, double entryShift)
	{
		if (index >= 0 && index < _dockSlots.Count)
		{
			double pageNumber = ((FloatingDragImage.Width > 0.0) ? FloatingDragImage.Width : (_dragFromDock ? _viewModel.DockIconSize : ((double)_viewModel.IconSize))) * Math.Max(0.05, FloatingDragScale.ScaleX);
			double settingIndex = (TryGetDockSlotLive(index, _dockSlots.Count, out var _, out var scale) ? Math.Max(1.0, scale) : 1.0);
			double num3 = _viewModel.DockIconSize * settingIndex;
			double num4 = 1.0;
			if (num3 > 1.0 && pageNumber > 1.0)
			{
				num4 = Math.Clamp(pageNumber / num3, 0.95, 1.05);
			}
			PopDockSlot(index, entryShift, num4);
			if (index >= 0 && index < _dockSlots.Count)
			{
				DockSlot dockSlot = _dockSlots[index];
				dockSlot.Scale.ScaleX = settingIndex * num4;
				dockSlot.Scale.ScaleY = settingIndex * num4;
			}
			App.Log($"[DBG][DOCK-HAND] idx={index} icon={_viewModel.IconSize:F0} card={pageNumber:F1} tile={num3:F1} law={settingIndex:F3} popFrom={num4:F3} n={_dockSlots.Count}");
		}
	}

	private bool TryGetCellRect(AppItem app, out Rect cellRect)
	{
		return TryGetCellRectAt(_viewModel.CurrentPageApps.IndexOf(app), out cellRect);
	}

	private bool TryGetCellRectAt(int index, out Rect cellRect)
	{
		cellRect = Rect.Empty;
		if (index < 0)
		{
			return false;
		}
		UniformGrid uniformGrid = FindVisualChild<UniformGrid>(_primaryItems);
		if (uniformGrid == null || uniformGrid.ActualWidth <= 0.0 || uniformGrid.ActualHeight <= 0.0)
		{
			return false;
		}
		int pageNumber = Math.Max(1, _viewModel.Columns);
		int settingIndex = Math.Max(1, _viewModel.Rows);
		double num3 = uniformGrid.ActualWidth / (double)pageNumber;
		double num4 = uniformGrid.ActualHeight / (double)settingIndex;
		Point point = uniformGrid.TranslatePoint(new Point(0.0, 0.0), this);
		cellRect = new Rect(point.X + (double)(index % pageNumber) * num3, point.Y + (double)(index / pageNumber) * num4, num3, num4);
		return true;
	}

	private bool TryGetCardRect(AppItem app, out Rect cardRect)
	{
		return TryGetCardRectAt(_viewModel.CurrentPageApps.IndexOf(app), out cardRect);
	}

	private bool TryGetCardRectAt(int index, out Rect cardRect)
	{
		cardRect = Rect.Empty;
		if (!TryGetCellRectAt(index, out var cellRect))
		{
			return false;
		}
		double pageNumber = Math.Min(_viewModel.CardWidth, cellRect.Width);
		double settingIndex = Math.Min(_viewModel.CardHeight, cellRect.Height);
		cardRect = new Rect(cellRect.X + (cellRect.Width - pageNumber) / 2.0, cellRect.Y + (cellRect.Height - settingIndex) / 2.0, pageNumber, settingIndex);
		return true;
	}

	private static Button? ItemButtonOf(ItemsControl host, int index)
	{
		if (!(host.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement parent))
		{
			return null;
		}
		return FindVisualChild<Button>(parent);
	}

	private void StartMergePulse(AppItem app)
	{
		int pageNumber = _viewModel.CurrentPageApps.IndexOf(app);
		if (pageNumber < 0)
		{
			return;
		}
		Button button = ItemButtonOf(_primaryItems, pageNumber);
		if (button != null)
		{
			_mergePulsedElement = button;
			ScaleTransform scaleTransform = button.RenderTransform as ScaleTransform;
			if (scaleTransform == null)
			{
				scaleTransform = new ScaleTransform(1.0, 1.0);
				button.RenderTransformOrigin = new Point(0.5, 0.5);
				button.RenderTransform = scaleTransform;
			}
			DoubleAnimation animation = SpringTo(scaleTransform.ScaleX, 1.1, 180, 0.25);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
		}
	}

	private void ClearMergeHover()
	{
		ClearMergeHoverVisual();
		HideFoldHint();
		_mergeTarget = null;
		_mergeCandidate = null;
		_mergeCandidateStartTicks = 0L;
	}

	private void ClearMergeHoverVisual()
	{
		if (_mergePulsedElement == null)
		{
			return;
		}
		FrameworkElement el = _mergePulsedElement;
		_mergePulsedElement = null;
		Transform renderTransform = el.RenderTransform;
		ScaleTransform st = renderTransform as ScaleTransform;
		if (st == null)
		{
			return;
		}
		st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		DoubleAnimation doubleAnimation = SpringTo(st.ScaleX, 1.0, 220, 0.15);
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			if (el.RenderTransform == st)
			{
				el.RenderTransform = null;
			}
		};
		st.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);
		st.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
	}

	private void AnimatePageStep(int direction)
	{
		int pageNumber = PageNavigation.StepTarget(_viewModel.CurrentPage, _isPageAnimating ? _currentTransitionTarget : (-1), _isPageAnimating ? _queuedTransitionTarget : (-1), direction, _viewModel.TotalPages);
		if (!_isPageAnimating && pageNumber == _viewModel.CurrentPage)
		{
			if (_viewModel.TotalPages > 1)
			{
				AnimateBoundaryNudge(direction);
			}
		}
		else
		{
			AnimatePageTransition(pageNumber, direction);
		}
	}

	public void AnimatePageTransition(int targetPage, int direction)
	{
		targetPage = Math.Clamp(targetPage, 1, Math.Max(1, _viewModel.TotalPages));
		if (_isPageAnimating)
		{
			if (targetPage == _currentTransitionTarget)
			{
				_queuedTransitionTarget = -1;
			}
			else
			{
				_queuedTransitionTarget = targetPage;
			}
		}
		else if (targetPage != _viewModel.CurrentPage)
		{
			direction = Math.Sign(targetPage - _viewModel.CurrentPage);
			if (Math.Abs(targetPage - _viewModel.CurrentPage) > 1)
			{
				App.Log($"[PAGE] jump {_viewModel.CurrentPage} -> {targetPage} (no slide)");
				_viewModel.CommitPageChange(targetPage);
				ResetPageTransforms();
			}
			else
			{
				_isPageAnimating = true;
				_currentTransitionTarget = targetPage;
				_currentTransitionDirection = direction;
				_lastTravelDirection = direction;
				SetSlidingLayersCacheEnabled(enabled: true);
				EnsureNeighbour(direction);
				double slideDistance = GetSlideDistance();
				double fromTX = ((direction > 0) ? slideDistance : (0.0 - slideDistance));
				double endPrimaryX = ((direction > 0) ? (0.0 - slideDistance) : slideDistance);
				_swipeTargetPage = targetPage;
				var (ease, duration, _) = ResolveSettle(slideDistance, 2900.0);
				StartSlideAnimation(targetPage, direction, 0.0, fromTX, endPrimaryX, duration, ease);
			}
		}
	}

	private void AnimateBoundaryNudge(int direction)
	{
		if (!_layersReady || IsSettingsOpen || _viewModel.IsFolderOpen || _isWindowAnimating || _isSwiping || _isItemDragging || PageMotionActive)
		{
			return;
		}
		double push = (double)(-direction) * 18.0;
		_isReturning = true;
		_slideStartTicks = DateTime.UtcNow.Ticks;
		_viewModel.IsPageSwiping = true;
		_swipeTargetPage = ((direction <= 0) ? 1 : _viewModel.TotalPages);
		SetSlidingLayersCacheEnabled(enabled: true);
		BeginDrivenMotion(0.0, push, 0.0, null, Tempoed(110.0), new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		}, () =>
		{
			BeginDrivenMotion(push, 0.0, 0.0, null, Tempoed(260.0), new SpringEase
			{
				Bounce = 0.2
			}, () =>
			{
				_isReturning = false;
				_swipeTargetPage = -1;
				LogFramePhase("nudge", _slideStartTicks);
				ResetPageTransforms();
			}, Math.Abs(push));
		}, Math.Abs(push));
	}

	private void StartSlideAnimation(int targetPage, int direction, double fromPX, double fromTX, double endPrimaryX, TimeSpan duration, IEasingFunction ease)
	{
		_viewModel.IsPageSwiping = true;
		_slideFromPX = fromPX;
		_slideToPX = endPrimaryX;
		_slideFromTX = fromTX;
		_slideToTX = 0.0;
		_slideDistance = Math.Abs(endPrimaryX - fromPX);
		_slideEase = ease;
		_slideDurationMs = duration.TotalMilliseconds;
		_slideStartTicks = DateTime.UtcNow.Ticks;
		BeginDrivenMotion(fromPX, endPrimaryX, fromTX, 0.0, duration, ease, () =>
		{
			LogFramePhase("settle", _slideStartTicks);
			StallBegin();
			_isPageAnimating = false;
			if (_neighbourIdx >= 0 && _layerPage[_neighbourIdx] == targetPage)
			{
				RollLayers(direction, targetPage);
			}
			else
			{
				_viewModel.CommitPageChange(targetPage);
			}
			StallEnd($"landing page={targetPage} (rebuild={_neighbourIdx < 0 || _layerPage[_primaryIdx] != targetPage})");
			_currentTransitionTarget = -1;
			ResetPageTransforms();
			_swipeTargetPage = -1;
			SettleDragCrossPage();
			_lastSwipeEndTimeTicks = DateTime.UtcNow.Ticks;
			int queuedTransitionTarget = _queuedTransitionTarget;
			_queuedTransitionTarget = -1;
			if (queuedTransitionTarget >= 1 && queuedTransitionTarget != _viewModel.CurrentPage)
			{
				int pageNumber = PageNavigation.StepTarget(_viewModel.CurrentPage, -1, -1, queuedTransitionTarget - _viewModel.CurrentPage, _viewModel.TotalPages);
				AnimatePageTransition(pageNumber, Math.Sign(pageNumber - _viewModel.CurrentPage));
				if (pageNumber != queuedTransitionTarget)
				{
					_queuedTransitionTarget = queuedTransitionTarget;
				}
			}
		});
	}

	private void BeginDrivenMotion(double fromPX, double toPX, double fromTX, double? toTX, TimeSpan duration, IEasingFunction ease, Action landed, double? pingAmp = null)
	{
		_motionDriven = true;
		_motionSeq = ++_slideSeq;
		_motionStartSw = Stopwatch.GetTimestamp();
		_motionDurationMs = Math.Max(1.0, duration.TotalMilliseconds);
		_motionFromPX = fromPX;
		_motionToPX = toPX;
		_motionFromTX = fromTX;
		_motionToTX = toTX;
		_motionEase = ease;
		_motionLanded = landed;
		_motionPingAmp = pingAmp;
		_motionPrimary = _primaryTranslate;
		_motionNeighbour = ((!toTX.HasValue) ? null : _neighbourTranslate);
		_motionFrames = 0;
		_motionDistinctOffsets = 0;
		_motionSubPixelWrites = 0;
		_motionLastDevOffset = long.MinValue;
		_motionSampleSw = 0L;
		_motionVisualSpeed = 0.0;
		_swipeDeviceScale = Math.Max(1.0, VisualTreeHelper.GetDpi(this).DpiScaleX);
		HookFrameRendering();
		_motionMetrics.Begin(_isReturning ? "page-return" : "page-settle");
		ApplyDrivenPosition(0.0);
		App.Log($"[DRIVEN] {((!toTX.HasValue) ? "return" : "settle")} px {fromPX:F0}->{toPX:F0} scale={_swipeDeviceScale:F2} dur={_motionDurationMs:F0}ms ease={ease.GetType().Name}");
	}

	private void CancelDrivenMotion()
	{
		_motionMetrics.End();
		_motionDriven = false;
		_motionLanded = null;
		_motionPrimary = null;
		_motionNeighbour = null;
		_motionPingAmp = null;
	}

	private void AdvanceDrivenMotion()
	{
		if (!_motionDriven)
		{
			return;
		}
		if (_motionSeq != _slideSeq)
		{
			CancelDrivenMotion();
			return;
		}
		double pageNumber = (double)(Stopwatch.GetTimestamp() - _motionStartSw) * 1000.0 / (double)Stopwatch.Frequency;
		if (pageNumber >= _motionDurationMs)
		{
			ApplyDrivenPosition(1.0);
			App.Log($"[DRIVEN] done frames={_motionFrames} moved={_motionDistinctOffsets} subpx={_motionSubPixelWrites} pxPerFrame={((_motionFrames > 0) ? ((double)_motionDistinctOffsets / (double)_motionFrames) : 0.0):F1}");
			Action? obj = _motionLanded ?? ((Action)(() =>
			{
			}));
			CancelDrivenMotion();
			obj();
		}
		else
		{
			ApplyDrivenPosition(pageNumber / _motionDurationMs);
		}
	}

	private void ApplyDrivenPosition(double t)
	{
		if (_motionEase == null || _motionPrimary == null)
		{
			return;
		}
		double pageNumber = _motionEase.Ease(Math.Clamp(t, 0.0, 1.0));
		double settingIndex = _motionFromPX + (_motionToPX - _motionFromPX) * pageNumber;
		double? motionToTX = _motionToTX;
		double? num3;
		if (motionToTX.HasValue)
		{
			double valueOrDefault = motionToTX.GetValueOrDefault();
			num3 = _motionFromTX + (valueOrDefault - _motionFromTX) * pageNumber;
		}
		else
		{
			num3 = null;
		}
		double? transitionX = num3;
		long timestamp = Stopwatch.GetTimestamp();
		if (_motionSampleSw != 0L)
		{
			double num4 = (double)(timestamp - _motionSampleSw) * 1000.0 / (double)Stopwatch.Frequency;
			if (num4 > 0.5)
			{
				_motionVisualSpeed = Math.Abs(settingIndex - _motionSamplePx) / num4 * 1000.0;
			}
		}
		_motionSamplePx = settingIndex;
		_motionSampleSw = timestamp;
		WriteSnappedOffsets(settingIndex, transitionX, _motionPrimary, _motionNeighbour);
		_motionFrames++;
		double num5 = ((_swipeDeviceScale > 1.0) ? _swipeDeviceScale : 1.0);
		double num6 = settingIndex * num5;
		long num7 = (long)Math.Round(num6);
		if (Math.Abs(num6 - (double)num7) > 0.02)
		{
			_motionSubPixelWrites++;
		}
		if (_motionLastDevOffset != num7)
		{
			_motionLastDevOffset = num7;
			_motionDistinctOffsets++;
		}
		if (_swipeTargetPage >= 1)
		{
			motionToTX = _motionPingAmp;
			double value;
			if (motionToTX.HasValue)
			{
				double valueOrDefault2 = motionToTX.GetValueOrDefault();
				value = Math.Min(0.5, Math.Abs(settingIndex) / Math.Max(1.0, valueOrDefault2));
			}
			else
			{
				value = Math.Min(1.0, Math.Abs(settingIndex) / Math.Max(1.0, GetSlideDistance()));
			}
			_pendingIndicatorProgress = value;
		}
	}

	private void WriteSnappedOffsets(double primaryX, double? transitionX, TranslateTransform primary, TranslateTransform? neighbour)
	{
		double pageNumber = ((_swipeDeviceScale > 1.0) ? _swipeDeviceScale : 1.0);
		primary.X = Math.Round(primaryX * pageNumber) / pageNumber;
		if (transitionX.HasValue && neighbour != null)
		{
			neighbour.X = Math.Round(transitionX.Value * pageNumber) / pageNumber;
		}
	}

	private void ApplySwipeOffsets(double primaryX, double? transitionX = null)
	{
		WriteSnappedOffsets(primaryX, transitionX, _primaryTranslate, _neighbourTranslate);
		_pendingIndicatorProgress = Math.Min(1.0, Math.Abs(primaryX) / GetSlideDistance());
	}

	private static DoubleAnimation SpringTo(double from, double to, int settleMs, double bounce)
	{
		return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(settleMs))
		{
			EasingFunction = new SpringEase
			{
				Bounce = bounce
			}
		};
	}

	private static DoubleAnimation SpringTo(double to, int settleMs, double bounce)
	{
		return new DoubleAnimation(to, TimeSpan.FromMilliseconds(settleMs))
		{
			EasingFunction = new SpringEase
			{
				Bounce = bounce
			}
		};
	}

	private TimeSpan Tempoed(double ms)
	{
		return TimeSpan.FromMilliseconds(ms * TempoScale);
	}

	private (EasingFunctionBase Ease, TimeSpan Duration, double Peak) ResolveSettle(double distance, double releaseSpeed)
	{
		if (TempoScale <= 0.0)
		{
			return (Ease: LinearEase.Instance, Duration: TimeSpan.FromMilliseconds(1.0), Peak: 1.0);
		}
		double pageNumber = Math.Max(releaseSpeed, 900.0) / 1000.0 * 1.3;
		double value = Math.Clamp(distance / pageNumber, 230.0 * TempoScale, 650.0 * TempoScale);
		EasingFunctionBase curveOverride = CurveOverride;
		if (curveOverride != null)
		{
			return (Ease: curveOverride, Duration: TimeSpan.FromMilliseconds(value), Peak: CurvePeak);
		}
		return (Ease: new QuinticEase
		{
			EasingMode = EasingMode.EaseOut
		}, Duration: TimeSpan.FromMilliseconds(value), Peak: 5.0);
	}

	private (EasingFunctionBase Ease, TimeSpan Duration) ResolveReturn(double distance, double releaseSpeed)
	{
		if (TempoScale <= 0.0)
		{
			return (Ease: LinearEase.Instance, Duration: TimeSpan.FromMilliseconds(1.0));
		}
		double value = Math.Clamp(distance / 2.0, 180.0 * TempoScale, 320.0 * TempoScale);
		EasingFunctionBase curveOverride = CurveOverride;
		if (curveOverride != null)
		{
			return (Ease: curveOverride, Duration: TimeSpan.FromMilliseconds(value));
		}
		return (Ease: new QuinticEase
		{
			EasingMode = EasingMode.EaseOut
		}, Duration: TimeSpan.FromMilliseconds(value));
	}

	private double GetSlideDistance()
	{
		double pageNumber = ((PageViewport.ActualWidth > 0.0) ? PageViewport.ActualWidth : ActualWidth);
		if (pageNumber <= 0.0)
		{
			pageNumber = 1400.0;
		}
		return pageNumber;
	}

	private void StallBegin()
	{
		_stallTicks = Stopwatch.GetTimestamp();
	}

	private void StallEnd(string what, double warnMs = 12.0)
	{
		double pageNumber = (double)(Stopwatch.GetTimestamp() - _stallTicks) * 1000.0 / (double)Stopwatch.Frequency;
		if (pageNumber >= warnMs)
		{
			App.Log($"[STALL] {what} = {pageNumber:F0}ms");
		}
	}

	private void FillLayer(int slot, int page)
	{
		if (_warmSlot == slot)
		{
			_warmSlot = (_warmPage = -1);
		}
		if (_layerPage[slot] == page)
		{
			return;
		}
		_viewModel.LogPageIconState(page);
		bool isPointerDown = _isPointerDown;
		bool validated = isPointerDown || PageMotionActive;
		if (validated)
		{
			App.Log($"[WARM] cold fill page={page} {((isPointerDown && !PageMotionActive) ? "at-press" : "MID-MOTION")}");
			StallBegin();
		}
		List<AppItem> list = ((page >= 1 && page <= _viewModel.TotalPages) ? _viewModel.GetPageItems(page) : new List<AppItem>());
		ObservableCollection<AppItem> observableCollection = _layerItems[slot].ItemsSource as ObservableCollection<AppItem>;
		if (observableCollection == null)
		{
			observableCollection = new ObservableCollection<AppItem>(list);
			_layerItems[slot].ItemsSource = observableCollection;
		}
		else
		{
			for (int i = 0; i < Math.Min(observableCollection.Count, list.Count); i++)
			{
				if (observableCollection[i] != list[i])
				{
					observableCollection[i] = list[i];
				}
			}
			while (observableCollection.Count > list.Count)
			{
				observableCollection.RemoveAt(observableCollection.Count - 1);
			}
			for (int j = observableCollection.Count; j < list.Count; j++)
			{
				observableCollection.Add(list[j]);
			}
		}
		if (slot == _primaryIdx)
		{
			_viewModel.CurrentPageApps = observableCollection;
		}
		_layerPage[slot] = page;
		if (validated)
		{
			StallEnd($"FillLayer page={page}");
		}
	}

	private void ParkSlot(int slot)
	{
		_layerTranslates[slot].BeginAnimation(TranslateTransform.XProperty, null);
		if (slot == _primaryIdx)
		{
			_layerTranslates[slot].X = 0.0;
			_layers[slot].Visibility = Visibility.Visible;
		}
		else
		{
			double pageNumber = Math.Max(GetSlideDistance() * 2.0, 50000.0);
			_layerTranslates[slot].X = ((slot == _leftIdx) ? (0.0 - pageNumber) : pageNumber);
			_layers[slot].Visibility = ((_layerPage[slot] < 1) ? Visibility.Hidden : Visibility.Visible);
		}
	}

	private void InitPageLayers()
	{
		_layers = new Border[4] { Layer0, Layer1, Layer2, Layer3 };
		_layerTranslates = new TranslateTransform[4] { Layer0Translate, Layer1Translate, Layer2Translate, Layer3Translate };
		_layerItems = new ItemsControl[4] { Layer0Items, Layer1Items, Layer2Items, Layer3Items };
		_reserveIdx = 3;
		_primaryIdx = 0;
		_leftIdx = 1;
		_rightIdx = 2;
		_primaryLayer = _layers[0];
		_primaryTranslate = _layerTranslates[0];
		_primaryItems = _layerItems[0];
		_primaryItems.ItemsSource = _viewModel.CurrentPageApps;
		_layerPage[_primaryIdx] = _viewModel.CurrentPage;
		_layerTranslates[_leftIdx].X = -100000.0;
		_layerTranslates[_rightIdx].X = 100000.0;
		_neighbourTranslate = _layerTranslates[_rightIdx];
		PageViewport.OpacityMask = null;
		_layersReady = true;
		int currentPage = _viewModel.CurrentPage;
		if (currentPage - 1 >= 1)
		{
			FillLayer(_leftIdx, currentPage - 1);
			ParkSlot(_leftIdx);
		}
		if (currentPage + 1 <= _viewModel.TotalPages)
		{
			FillLayer(_rightIdx, currentPage + 1);
			ParkSlot(_rightIdx);
		}
		PrepareInitialReserve();
	}

	private void PrepareInitialReserve()
	{
		int pageNumber = _viewModel.CurrentPage + 2;
		_warmSlot = (_warmPage = -1);
		_layerPage[_reserveIdx] = -1;
		if (pageNumber <= _viewModel.TotalPages && !_viewModel.IsSearchActive)
		{
			FillLayer(_reserveIdx, pageNumber);
		}
		ParkSlot(_reserveIdx);
		_layers[_reserveIdx].UpdateLayout();
	}

	private void OnPageContentsReplaced()
	{
		if (!_layersReady)
		{
			return;
		}
		if (PageMotionActive)
		{
			CancelDrivenMotion();
			_isPageAnimating = (_isSwiping = (_isReturning = false));
			_queuedTransitionTarget = (_currentTransitionTarget = (_swipeTargetPage = -1));
			_viewModel.IsPageSwiping = false;
			ResetPageTransforms();
		}
		if (_primaryItems.ItemsSource != _viewModel.CurrentPageApps)
		{
			_primaryItems.ItemsSource = _viewModel.CurrentPageApps;
		}
		int currentPage = _viewModel.CurrentPage;
		_layerPage[_primaryIdx] = currentPage;
		_layerPage[_leftIdx] = (_layerPage[_rightIdx] = (_layerPage[_reserveIdx] = -1));
		_warmSlot = (_warmPage = -1);
		ParkSlot(_reserveIdx);
		if (_viewModel.IsSearchActive)
		{
			_layerPage[_leftIdx] = -1;
			_layerPage[_rightIdx] = -1;
			return;
		}
		if (currentPage - 1 >= 1)
		{
			FillLayer(_leftIdx, currentPage - 1);
			ParkSlot(_leftIdx);
		}
		else
		{
			_layerPage[_leftIdx] = -1;
			ParkSlot(_leftIdx);
		}
		if (currentPage + 1 <= _viewModel.TotalPages)
		{
			FillLayer(_rightIdx, currentPage + 1);
			ParkSlot(_rightIdx);
		}
		else
		{
			_layerPage[_rightIdx] = -1;
			ParkSlot(_rightIdx);
		}
		if (_dockFly == null && DateTime.UtcNow.Ticks - _lastDragEndTimeTicks >= TimeSpan.FromMilliseconds(500.0).Ticks)
		{
			if (!_viewModel.IsOpen)
			{
				PrepareInitialReserve();
			}
			ScheduleNeighbourWarm();
			return;
		}
		Task.Delay(500).ContinueWith((Task _) =>
		{
			try
			{
				Dispatcher.InvokeAsync(() =>
				{
					if (!_warmScheduled && NeighbourWarmPending())
					{
						ScheduleNeighbourWarm();
					}
				}, DispatcherPriority.ContextIdle);
			}
			catch
			{
			}
		});
	}

	private void EnsureNeighbour(int dir)
	{
		int pageNumber = ((dir > 0) ? _rightIdx : _leftIdx);
		if (_neighbourIdx >= 0 && _neighbourIdx != pageNumber)
		{
			ParkSlot(_neighbourIdx);
		}
		_neighbourIdx = pageNumber;
		_neighbourTranslate = _layerTranslates[pageNumber];
		FillLayer(pageNumber, _viewModel.CurrentPage + dir);
		_layers[pageNumber].Visibility = Visibility.Visible;
		double slideDistance = GetSlideDistance();
		_layerTranslates[pageNumber].BeginAnimation(TranslateTransform.XProperty, null);
		_layerTranslates[pageNumber].X = (double)dir * slideDistance;
	}

	private void RollLayers(int dir, int landedPage)
	{
		int primaryIdx = ((dir > 0) ? _rightIdx : _leftIdx);
		int primaryIdx2 = _primaryIdx;
		int pageNumber = ((dir > 0) ? _leftIdx : _rightIdx);
		int settingIndex = landedPage + dir;
		if (_layerPage[_reserveIdx] == settingIndex)
		{
			int reserveIdx = pageNumber;
			pageNumber = _reserveIdx;
			_reserveIdx = reserveIdx;
			_warmSlot = (_warmPage = -1);
		}
		_primaryIdx = primaryIdx;
		if (dir > 0)
		{
			_leftIdx = primaryIdx2;
			_rightIdx = pageNumber;
		}
		else
		{
			_rightIdx = primaryIdx2;
			_leftIdx = pageNumber;
		}
		_primaryLayer = _layers[_primaryIdx];
		_primaryTranslate = _layerTranslates[_primaryIdx];
		_primaryItems = _layerItems[_primaryIdx];
		_neighbourIdx = -1;
		_neighbourTranslate = _layerTranslates[pageNumber];
		if (_primaryItems.ItemsSource is ObservableCollection<AppItem> primaryApps)
		{
			_viewModel.RollToPage(landedPage, primaryApps);
		}
		int num3 = ((dir > 0) ? (landedPage + 1) : (landedPage - 1));
		if (num3 < 1 || num3 > _viewModel.TotalPages)
		{
			_layerPage[pageNumber] = -1;
		}
		else if (_layerPage[pageNumber] != num3)
		{
			_layerPage[pageNumber] = -1;
		}
		_lastTravelDirection = dir;
		ParkSlot(_primaryIdx);
		ParkSlot(_leftIdx);
		ParkSlot(_rightIdx);
		ParkSlot(_reserveIdx);
		ScheduleNeighbourWarm();
	}

	private void SetSlidingLayersCacheEnabled(bool enabled)
	{
		Border[] layers;
		if (!enabled)
		{
			layers = _layers;
			for (int i = 0; i < layers.Length; i++)
			{
				layers[i].CacheMode = null;
			}
			return;
		}
		if (_slideCache == null || Math.Abs(_slideCacheScale - VisualTreeHelper.GetDpi(this).DpiScaleX) > 0.001)
		{
			_slideCacheScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
			_slideCache = new BitmapCache(Math.Max(1.0, _slideCacheScale));
		}
		layers = _layers;
		foreach (Border border in layers)
		{
			if (border.CacheMode != _slideCache)
			{
				border.CacheMode = _slideCache;
			}
		}
	}

	private void HookFrameRendering()
	{
		if (!_renderHooked)
		{
			_renderHooked = true;
			_lastFrameTicks = 0L;
			CompositionTarget.Rendering += OnFrameRendering;
			StartUiBlockWatchdog();
		}
	}

	private void UnhookFrameRendering()
	{
		if (_renderHooked)
		{
			if (_motionDriven)
			{
				ApplyDrivenPosition(1.0);
				Action? obj = _motionLanded ?? ((Action)(() =>
				{
				}));
				CancelDrivenMotion();
				obj();
			}
			_renderHooked = false;
			CompositionTarget.Rendering -= OnFrameRendering;
			_motionMetrics.End();
		}
	}

	private void OnFrameRendering(object? sender, EventArgs e)
	{
		if (e is RenderingEventArgs e2)
		{
			_motionMetrics.Record(e2.RenderingTime.Ticks);
		}
		ref string watchdogPhase = ref _watchdogPhase;
		string text;
		if (_isSwiping)
		{
			text = "drag";
		}
		else if (_isPageAnimating)
		{
			text = "settle";
		}
		else if (_isReturning)
		{
			text = "return";
		}
		else
		{
			text = (_isPointerDown ? "press" : "idle");
		}
		watchdogPhase = text;
		AdvanceDrivenMotion();
		ApplyPendingFrameUpdates();
		if (!PageMotionActive || !(e is RenderingEventArgs { RenderingTime: { Ticks: var ticks } }))
		{
			return;
		}
		if (_lastFrameTicks == 0L)
		{
			_lastFrameTicks = ticks;
			_phaseStartRenderTicks = ticks;
			return;
		}
		double pageNumber = (double)(ticks - _lastFrameTicks) / 10000.0;
		_lastFrameTicks = ticks;
		if (pageNumber > 0.5 && pageNumber < 500.0)
		{
			_swipeFrameCount++;
			_swipeFrameGapTotal += pageNumber;
			if (pageNumber > _swipeFrameGapWorst)
			{
				_swipeFrameGapWorst = pageNumber;
				_swipeFrameGapWorstAt = (double)(ticks - _phaseStartRenderTicks) / 10000.0;
			}
			if (pageNumber > 8.0)
			{
				_swipeDroppedFrames++;
			}
			if (pageNumber > 9.0 && _swipeGaps.Count < 14)
			{
				_swipeGaps.Add($"+{(double)(ticks - _phaseStartRenderTicks) / 10000.0:F0}:{pageNumber:F1}");
			}
		}
	}

	private void LogFramePhase(string phase, long startTicks)
	{
		if (_swipeFrameCount > 0)
		{
			double pageNumber = (double)(DateTime.UtcNow.Ticks - startTicks) / 10000.0;
			double settingIndex = _swipeFrameGapTotal / (double)_swipeFrameCount;
			double value = pageNumber / 6.06;
			App.Log($"[SWIPE-FPS] phase={phase} held={pageNumber:F0}ms events={_swipeMoveCount} presented={_swipeFrameCount} expect={value:F0} dropped={_swipeDroppedFrames} avg={settingIndex:F1}ms({1000.0 / settingIndex:F0}fps) worst={_swipeFrameGapWorst:F1}ms start={_swipeFrameGapWorstAt - _swipeFrameGapWorst:F0}ms gaps=[{string.Join(" ", _swipeGaps)}] blocks={DrainUiBlocks()}");
		}
		_swipeFrameCount = 0;
		_swipeFrameGapTotal = 0.0;
		_swipeFrameGapWorst = 0.0;
		_swipeFrameGapWorstAt = 0.0;
		_swipeDroppedFrames = 0;
		_swipeMoveCount = 0;
		_swipeGaps.Clear();
		_lastFrameTicks = 0L;
	}

	private void BeginFrameProbe(string phase)
	{
		EndFrameProbe();
		_phaseMetrics.Begin(phase);
		_probePhase = phase;
		_probeLastTicks = 0L;
		_probeFrames = 0;
		_probeDrops = 0;
		_probeWorstGap = 0.0;
		_probeStartTicks = 0L;
		_probeWorstAt = 0.0;
		_openFirstTickMs = 0.0;
		_probeGaps.Clear();
		_probeRunning = true;
		_watchdogPhase = phase;
		StartUiBlockWatchdog();
		CompositionTarget.Rendering += OnFrameProbe;
	}

	private void OnFrameProbe(object? sender, EventArgs e)
	{
		if (!(e is RenderingEventArgs e2))
		{
			return;
		}
		_phaseMetrics.Record(e2.RenderingTime.Ticks);
		long ticks = e2.RenderingTime.Ticks;
		if (_probeLastTicks != 0L)
		{
			double pageNumber = (double)(ticks - _probeLastTicks) / 10000.0;
			if (pageNumber > 0.5 && pageNumber < 500.0)
			{
				_probeFrames++;
				double settingIndex = (double)(ticks - _probeStartTicks) / 10000.0;
				if (pageNumber > _probeWorstGap)
				{
					_probeWorstGap = pageNumber;
					_probeWorstAt = settingIndex;
				}
				if (pageNumber > 15.0 && _probeGaps.Count < 12)
				{
					_probeGaps.Add($"+{settingIndex:F0}:{pageNumber:F1}");
				}
				if (pageNumber > 12.0)
				{
					_probeDrops++;
				}
			}
		}
		else
		{
			_probeStartTicks = ticks;
		}
		if (_openWatch != null && _openFirstTickMs == 0.0)
		{
			_openFirstTickMs = _openWatch.Elapsed.TotalMilliseconds;
		}
		_probeLastTicks = ticks;
	}

	private void EndFrameProbe()
	{
		_phaseMetrics.End();
		if (_probePhase != null)
		{
			App.Log($"[FIELD-FPS] phase={_probePhase} frames={_probeFrames} drops={_probeDrops} worst={_probeWorstGap:F1}ms@{_probeWorstAt:F0}ms gaps=[{string.Join(" ", _probeGaps)}] blocks={DrainUiBlocks()}");
			CompositionTarget.Rendering -= OnFrameProbe;
			_probePhase = null;
			_probeRunning = false;
			_watchdogPhase = "idle";
		}
	}

	private void EndFrameProbeAfter(string phase, int ms)
	{
		Task.Delay(ms).ContinueWith((Task _) =>
		{
			try
			{
				Dispatcher.Invoke(() =>
				{
					if (_probePhase == phase)
					{
						EndFrameProbe();
					}
				});
			}
			catch
			{
			}
		});
	}

	private void StartUiBlockWatchdog()
	{
		if (Environment.GetEnvironmentVariable("AIRDECK_UI_WATCHDOG") != "1" || Interlocked.Exchange(ref _watchdogRunning, 1) == 1)
		{
			return;
		}
		Dispatcher dispatcher = Dispatcher;
		long freq = Stopwatch.Frequency;
		Thread thread = new Thread(() =>
		{
			while (!_windowClosed && !dispatcher.HasShutdownStarted)
			{
				if (!_renderHooked && !_probeRunning)
				{
					Thread.Sleep(100);
				}
				else if (Interlocked.CompareExchange(ref _watchdogPending, 1, 0) != 0)
				{
					Thread.Sleep(16);
				}
				else
				{
					string phase = _watchdogPhase;
					long posted = Stopwatch.GetTimestamp();
					dispatcher.BeginInvoke((Action)(() =>
					{
						double pageNumber = (double)(Stopwatch.GetTimestamp() - posted) * 1000.0 / (double)freq;
						if (pageNumber >= 15.0)
						{
							_uiBlocks.Enqueue($"{phase}:{pageNumber:F0}ms");
						}
						Interlocked.Exchange(ref _watchdogPending, 0);
					}), DispatcherPriority.Render);
					Thread.Sleep(16);
				}
			}
		});
		thread.IsBackground = true;
		thread.Name = "ui-block-watchdog";
		thread.Start();
	}

	private string DrainUiBlocks()
	{
		if (_uiBlocks.IsEmpty)
		{
			return "-";
		}
		string text = "[";
		for (int i = 0; i < 6; i++)
		{
			if (!_uiBlocks.TryDequeue(out string result))
			{
				break;
			}
			if (i > 0)
			{
				text += " ";
			}
			text += result;
		}
		string result2;
		while (_uiBlocks.TryDequeue(out result2))
		{
		}
		return text + "]";
	}

	private void ApplyPendingFrameUpdates()
	{
		if (_dragCardDirty)
		{
			Canvas.SetLeft(FloatingDragCard, _pendingCardX);
			Canvas.SetTop(FloatingDragCard, _pendingCardY);
			_dragCardDirty = false;
		}
		if (_pendingIndicatorProgress.HasValue)
		{
			double value = _pendingIndicatorProgress.Value;
			_pendingIndicatorProgress = null;
			_viewModel.UpdateSwipeProgress(_viewModel.CurrentPage, _swipeTargetPage, value);
		}
		if (_isItemDragging)
		{
			TickMergeDwell();
			SyncFoldHint();
		}
	}

	private void ScheduleIdleCleanup()
	{
		if (_idleSettleTimer == null)
		{
			_idleSettleTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(800.0)
			};
			_idleSettleTimer.Tick += (object? s, EventArgs e) =>
			{
				_idleSettleTimer.Stop();
				if (!_isPointerDown && !PageMotionActive && !_isSwiping && !_isItemDragging)
				{
					UnhookFrameRendering();
					if (_idleSettleTimer.Interval.TotalMilliseconds < 5000.0)
					{
						_idleSettleTimer.Interval = TimeSpan.FromSeconds(5.0);
						_idleSettleTimer.Start();
					}
					else
					{
						SetSlidingLayersCacheEnabled(enabled: false);
					}
				}
			};
		}
		_idleSettleTimer.Stop();
		_idleSettleTimer.Interval = TimeSpan.FromMilliseconds(800.0);
		_idleSettleTimer.Start();
	}

	private void ResetPageTransforms()
	{
		CancelDrivenMotion();
		if (!_isPointerDown)
		{
			ScheduleIdleCleanup();
		}
		else
		{
			_idleSettleTimer?.Stop();
		}
		_pendingIndicatorProgress = null;
		_liftReleasePending = true;
		ParkSlot(_primaryIdx);
		ParkSlot(_leftIdx);
		ParkSlot(_rightIdx);
		_neighbourIdx = -1;
		_viewModel.ResetIndicatorProgress();
		_lastSwipeEndTimeTicks = DateTime.UtcNow.Ticks;
		ScheduleNeighbourWarm();
	}

	private void ScheduleNeighbourWarm(bool fromRetry = false)
	{
		if (!_warmScheduled)
		{
			if (!fromRetry)
			{
				_warmRetries = 0;
			}
			_warmScheduled = true;
			if (_warmTimer == null)
			{
				_warmTimer = CreateWarmTimer();
			}
			_warmTimer.Start();
		}
	}

	private DispatcherTimer CreateWarmTimer()
	{
		DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
		{
			Interval = TimeSpan.FromMilliseconds(16.0)
		};
		timer.Tick += (object? _, EventArgs _) =>
		{
			timer.Stop();
			_warmScheduled = false;
			WarmNeighbourPage();
		};
		return timer;
	}

	private void WarmNeighbourPage()
	{
		if (!_layersReady)
		{
			return;
		}
		if (_isWindowAnimating || _isPointerDown || PageMotionActive || _isItemDragging || _dockFly != null)
		{
			if (NeighbourWarmPending() && _warmRetries < 40)
			{
				_warmRetries++;
				ScheduleNeighbourWarm(fromRetry: true);
			}
		}
		else
		{
			if (_viewModel.IsFolderOpen)
			{
				return;
			}
			int[] entries = new int[3]
			{
				_lastTravelDirection,
				-_lastTravelDirection,
				_lastTravelDirection * 2
			};
			foreach (int pageNumber in entries)
			{
				int settingIndex = _viewModel.CurrentPage + pageNumber;
				if (settingIndex < 1 || settingIndex > _viewModel.TotalPages)
				{
					continue;
				}
				int num3;
				if (Math.Abs(pageNumber) == 2)
				{
					num3 = _reserveIdx;
				}
				else
				{
					num3 = ((pageNumber > 0) ? _rightIdx : _leftIdx);
				}
				if (_layerPage[num3] == settingIndex)
				{
					continue;
				}
				List<AppItem> pageItems = _viewModel.GetPageItems(settingIndex);
				if (_warmSlot != num3 || _warmPage != settingIndex)
				{
					_warmSlot = num3;
					_warmPage = settingIndex;
					_warmItemIndex = 0;
				}
				ObservableCollection<AppItem> observableCollection = _layerItems[num3].ItemsSource as ObservableCollection<AppItem>;
				if (observableCollection == null)
				{
					observableCollection = new ObservableCollection<AppItem>();
					_layerItems[num3].ItemsSource = observableCollection;
				}
				Stopwatch stopwatch = Stopwatch.StartNew();
				int num4 = Math.Min(_warmItemIndex + 2, pageItems.Count);
				while (_warmItemIndex < num4)
				{
					if (_warmItemIndex < observableCollection.Count)
					{
						if (observableCollection[_warmItemIndex] != pageItems[_warmItemIndex])
						{
							observableCollection[_warmItemIndex] = pageItems[_warmItemIndex];
						}
					}
					else
					{
						observableCollection.Add(pageItems[_warmItemIndex]);
					}
					_warmItemIndex++;
				}
				_layers[num3].CacheMode = null;
				_layers[num3].Visibility = Visibility.Visible;
				_layers[num3].UpdateLayout();
				if (stopwatch.ElapsedMilliseconds >= 12)
				{
					App.Log($"[WARM-BATCH] page={settingIndex} cells={num4} cost={stopwatch.ElapsedMilliseconds}ms");
				}
				if (_warmItemIndex < pageItems.Count)
				{
					ScheduleNeighbourWarm(fromRetry: true);
					break;
				}
				while (observableCollection.Count > pageItems.Count)
				{
					observableCollection.RemoveAt(observableCollection.Count - 1);
				}
				_layerPage[num3] = settingIndex;
				_warmSlot = (_warmPage = -1);
				ParkSlot(num3);
				_warmRetries = 0;
				if (NeighbourWarmPending())
				{
					ScheduleNeighbourWarm();
				}
				break;
			}
		}
	}

	private bool NeighbourWarmPending()
	{
		for (int i = -1; i <= 1; i += 2)
		{
			int pageNumber = _viewModel.CurrentPage + i;
			if (pageNumber >= 1 && pageNumber <= _viewModel.TotalPages && _layerPage[(i > 0) ? _rightIdx : _leftIdx] != pageNumber)
			{
				return true;
			}
		}
		int settingIndex = _viewModel.CurrentPage + _lastTravelDirection * 2;
		if (settingIndex >= 1 && settingIndex <= _viewModel.TotalPages && _layerPage[_reserveIdx] != settingIndex)
		{
			return true;
		}
		return false;
	}

	public LaunchpadWindow(MainViewModel viewModel, SettingsViewModel settingsViewModel)
	{
		_viewModel = viewModel;
		_settingsVm = settingsViewModel;
		InitializeComponent();
		InitializeDesktopResidentMode();
		ThemeManager.Apply(ThemeManager.PeekStoredTheme());
		DataContext = _viewModel;
		SettingsPopup.DataContext = _settingsVm;
		_settingsVm.SetDeactivationBlock = (bool blocked) =>
		{
			_blocksDeactivation = blocked;
		};
		_viewModel.PickApplicationFiles = PickApplicationFiles;
		_settingsVm.PropertyChanged += OnSettingsVmPropertyChanged;
		_sheetFlightTransform = SettingsPopup.RenderTransform;
		_viewModel.RequestOpen += AnimateShow;
		_viewModel.RequestClose += OnLaunchpadCloseRequested;
		_viewModel.RequestCloseSettings += OnCloseSettingsRequested;
		_viewModel.RequestOpenSettings += ShowSettingsPanel;
		_viewModel.RequestPageTransition += AnimatePageTransition;
		_viewModel.RequestPageStep += AnimatePageStep;
		_viewModel.RequestBoundaryNudge += AnimateBoundaryNudge;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.PageContentsReplaced += OnPageContentsReplaced;
		_viewModel.DockContentsReplaced += OnDockContentsReplaced;
		_viewModel.SetDockPresentationSize(_viewModel.DockIconSize);
		_viewModel.DockSizeChangeRequested += AnimateDockSize;
		_dockBarWasUp = _viewModel.IsDockVisible;
		_dockLastSingle = ((_viewModel.DockApps.Count == 1) ? _viewModel.DockApps[0] : null);
		InitPageLayers();
		Loaded += OnLoaded;
		SourceInitialized += OnSourceInitialized;
		KeyUp += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				_settingsEscapeHeld = false;
			}
		};
		KeyDown += OnWindowKeyDown;
		PreviewKeyDown += OnWindowPreviewKeyDown;
		PreviewTextInput += OnWindowPreviewTextInput;
		MouseWheel += OnWindowMouseWheel;
		Deactivated += OnWindowDeactivated;
		PreviewMouseDown += OnWindowPreviewMouseDown;
		PreviewMouseMove += OnWindowPreviewMouseMove;
		PreviewMouseUp += OnWindowPreviewMouseUp;
		_searchIdleTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(2.8)
		};
		_searchIdleTimer.Tick += OnSearchIdleTimerTick;
		SearchBox.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			ResetSearchIdleTimer();
		};
		SearchBox.GotKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			_searchIdleTimer?.Stop();
		};
		SearchBox.LostKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			ResetSearchIdleTimer();
		};
		ContextMenuOpening += (object _, ContextMenuEventArgs _) =>
		{
			_openMenus++;
		};
		ContextMenuClosing += (object _, ContextMenuEventArgs _) =>
		{
			if (_openMenus > 0)
			{
				_openMenus--;
			}
			_menuClosedTicks = DateTime.UtcNow.Ticks;
		};
		MouseLeave += (object _, MouseEventArgs _) =>
		{
			if (!_isItemDragging)
			{
				RestDock("leave");
			}
		};
	}

	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		_selfHwnd = new WindowInteropHelper(this).Handle;
		HwndSource.FromHwnd(_selfHwnd)?.AddHook(WndProc);
		ApplyBackgroundTransparency();
	}

	public void EnsureForeground(string why)
	{
		if (DesktopResidentSurface && why != "shown")
		{
			UpdateDesktopResidentMode();
			return;
		}
		Window activeOwnedDialog = _activeOwnedDialog;
		if (activeOwnedDialog != null && activeOwnedDialog.IsVisible)
		{
			RaiseOwnedDialog(activeOwnedDialog);
		}
		else
		{
			if (!_startupReady || _isPreWarming || _selfHwnd == IntPtr.Zero)
			{
				return;
			}
			nint foregroundWindow = NativeMethods.GetForegroundWindow();
			if (foregroundWindow != _selfHwnd)
			{
				uint windowThreadProcessId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
				uint currentThreadId = NativeMethods.GetCurrentThreadId();
				bool validated = false;
				try
				{
					if (NativeMethods.IsIconic(_selfHwnd))
					{
						NativeMethods.ShowWindow(_selfHwnd, 9);
					}
					bool value = NativeMethods.SetForegroundWindow(_selfHwnd);
					if (NativeMethods.GetForegroundWindow() != _selfHwnd)
					{
						validated = windowThreadProcessId != 0 && windowThreadProcessId != currentThreadId && NativeMethods.AttachThreadInput(windowThreadProcessId, currentThreadId, fAttach: true);
						value = NativeMethods.SetForegroundWindow(_selfHwnd);
					}
					if (!DesktopResidentSurface)
					{
						NativeMethods.BringWindowToTop(_selfHwnd);
					}
					try
					{
						Activate();
					}
					catch
					{
					}
					bool failure = NativeMethods.GetForegroundWindow() == _selfHwnd;
					App.Log($"[FG] {why}: was={((foregroundWindow == IntPtr.Zero) ? "none" : $"hwnd{((IntPtr)foregroundWindow).ToInt64():x}(pid{lpdwProcessId})")} set={value} now={failure} active={IsActive}");
					return;
				}
				finally
				{
					if (validated)
					{
						NativeMethods.AttachThreadInput(windowThreadProcessId, currentThreadId, fAttach: false);
					}
				}
			}
			if (!IsActive)
			{
				try
				{
					Activate();
				}
				catch
				{
				}
			}
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 70)
		{
			KeepDesktopResidentZOrder(lParam);
		}
		if (msg == 526 && !IsSettingsOpen)
		{
			long ticks = DateTime.UtcNow.Ticks;
			short pageNumber = (short)((((IntPtr)wParam).ToInt64() >> 16) & 0xFFFF);
			handled = pageNumber != 0;
			if (_wheelGesture.TryStep(pageNumber, horizontal: true, ticks))
			{
				if (pageNumber > 0)
				{
					_lastSwipeEndTimeTicks = ticks;
					_viewModel.NextPage();
					handled = true;
				}
				else if (pageNumber < 0)
				{
					_lastSwipeEndTimeTicks = ticks;
					_viewModel.PreviousPage();
					handled = true;
				}
			}
		}
		return IntPtr.Zero;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		App.Log("LaunchpadWindow Loaded");
		App.Log($"[RENDER] desktopTier={RenderCapability.Tier >> 16} processRenderMode={RenderOptions.ProcessRenderMode} hwndRenderMode={((PresentationSource.FromVisual(this) is HwndSource hwndSource) ? hwndSource.CompositionTarget.RenderMode.ToString() : "n/a")}");
		RepositionToCurrentMonitor();
		PageViewport.SizeChanged += (object s, SizeChangedEventArgs ev) =>
		{
			if (PageViewport.RenderSize.Width > 0.0 && PageViewport.RenderSize.Height > 0.0)
			{
				_viewModel.SetGridViewportSize(PageViewport.RenderSize, VisualTreeHelper.GetDpi(this).DpiScaleX);
				if (_layersReady && !PageMotionActive)
				{
					ParkSlot(_leftIdx);
					ParkSlot(_rightIdx);
				}
			}
		};
		if (PageViewport.RenderSize.Width > 0.0 && PageViewport.RenderSize.Height > 0.0)
		{
			_viewModel.SetGridViewportSize(PageViewport.RenderSize, VisualTreeHelper.GetDpi(this).DpiScaleX);
		}
		DockFrostLayer.SizeChanged += (object s, SizeChangedEventArgs ev) =>
		{
			UpdateDockFrost();
		};
		DockBar.SizeChanged += (object s, SizeChangedEventArgs ev) =>
		{
			UpdateDockFrost();
		};
		SizeChanged += (object s, SizeChangedEventArgs ev) =>
		{
			UpdateDockFrost();
			if (IsSettingsOpen)
			{
				UpdateSheetFrost();
			}
		};
		SurfaceContent.LayoutUpdated += (object? _, EventArgs _) =>
		{
			UpdateTopBarLayout();
		};
	}

	private void UpdateTopBarLayout()
	{
		double actualWidth = SurfaceContent.ActualWidth;
		if (actualWidth <= 0.0)
		{
			return;
		}
		double pageNumber = Math.Min(400.0, Math.Max(120.0, actualWidth - 102.0));
		if (Math.Abs(SearchFieldHost.Width - pageNumber) > 0.5)
		{
			SearchFieldHost.Width = pageNumber;
		}
		string searchPosition = _viewModel.Settings.SearchPosition;
		double settingIndex = Left(actualWidth, pageNumber, SearchPanel.HorizontalAlignment, (searchPosition == "Left") ? 28 : 0, (searchPosition == "Right") ? 74 : 0);
		Thickness margin = TopWidgetsPanel.Margin;
		double num3 = Left(actualWidth, TopWidgetsPanel.ActualWidth, TopWidgetsPanel.HorizontalAlignment, margin.Left, margin.Right);
		bool validated = SearchPanel.Visibility == Visibility.Visible && TopWidgetsPanel.Visibility == Visibility.Visible && TopWidgetsPanel.ActualWidth > 0.0 && settingIndex < num3 + TopWidgetsPanel.ActualWidth + 16.0 && num3 < settingIndex + pageNumber + 16.0;
		if (validated)
		{
			Thickness thickness = new Thickness((searchPosition == "Left") ? 28 : 0, 22.0 + Math.Max(38.0, TopWidgetsPanel.ActualHeight) + 12.0, (searchPosition == "Right") ? 74 : 0, 16.0);
			if (SearchPanel.Margin != thickness)
			{
				SearchPanel.Margin = thickness;
			}
		}
		else if (_searchOnSecondRow)
		{
			SearchPanel.ClearValue(FrameworkElement.MarginProperty);
		}
		_searchOnSecondRow = validated;
		static double Left(double available, double itemWidth, HorizontalAlignment alignment, double left, double right)
		{
			return alignment switch
			{
				HorizontalAlignment.Left => left, 
				HorizontalAlignment.Right => available - right - itemWidth, 
				_ => (available - itemWidth) / 2.0, 
			};
		}
	}

	public void PreWarm()
	{
		if (_firstFramePrepared)
		{
			return;
		}
		bool showActivated = ShowActivated;
		_isPreWarming = true;
		ShowActivated = false;
		IsHitTestVisible = false;
		try
		{
			RepositionToCurrentMonitor();
			RebuildBackdrop();
			MonitorBoundsInfo currentMonitorBounds = ScreenHelper.GetCurrentMonitorBounds(this, _viewModel.Settings.OpenOnCurrentMonitor);
			Size size = new Size(currentMonitorBounds.LogicalWidth, currentMonitorBounds.LogicalHeight);
			RootGrid.Measure(size);
			RootGrid.Arrange(new Rect(new Point(0.0, 0.0), size));
			RootGrid.UpdateLayout();
			if (PageViewport.ActualWidth > 0.0 && PageViewport.ActualHeight > 0.0)
			{
				_viewModel.SetGridViewportSize(new Size(PageViewport.ActualWidth, PageViewport.ActualHeight), VisualTreeHelper.GetDpi(this).DpiScaleX);
				RootGrid.Measure(size);
				RootGrid.Arrange(new Rect(new Point(0.0, 0.0), size));
				RootGrid.UpdateLayout();
			}
			Opacity = 0.0;
			if (Visibility != Visibility.Visible)
			{
				Show();
			}
			UpdateLayout();
			LayoutDock();
			SurfaceContent.Opacity = 1.0;
			ScaleTransform contentScaleTransform = ContentScaleTransform;
			double scaleX = (ContentScaleTransform.ScaleY = 1.0);
			contentScaleTransform.ScaleX = scaleX;
			TranslateTransform contentTranslateTransform = ContentTranslateTransform;
			scaleX = (ContentTranslateTransform.Y = 0.0);
			contentTranslateTransform.X = scaleX;
			Grid rootGrid = RootGrid;
			scaleX = (BlurredBackdropGroup.Opacity = 1.0);
			rootGrid.Opacity = scaleX;
			SearchPanel.Opacity = ((_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow) ? 1 : 0);
			Button settingsButton = SettingsButton;
			StackPanel topWidgetsPanel = TopWidgetsPanel;
			double num4 = (DockLayer.Opacity = 1.0);
			scaleX = (topWidgetsPanel.Opacity = num4);
			settingsButton.Opacity = scaleX;
			DockLayerTranslate.Y = 0.0;
			for (int i = 0; i < _viewModel.CurrentPageApps.Count; i++)
			{
				if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
				{
					frameworkElement.Opacity = 1.0;
				}
			}
			UpdateLayout();
			Stopwatch stopwatch = Stopwatch.StartNew();
			new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(size.Width / 2.0)), Math.Max(1, (int)Math.Ceiling(size.Height / 2.0)), 48.0, 48.0, PixelFormats.Pbgra32).Render(RootGrid);
			_firstFramePrepared = true;
			App.Log($"[FIRST-FRAME] prepared={stopwatch.Elapsed.TotalMilliseconds:F1}ms tiles={_viewModel.CurrentPageApps.Count}");
			ParkOpenPose();
			Opacity = 0.0;
			_isPreWarmed = true;
		}
		catch (Exception ex)
		{
			App.Log("PreWarm exception: " + ex.Message);
		}
		finally
		{
			ShowActivated = showActivated;
			_isPreWarming = false;
		}
	}

	public void HoldInitialOpen()
	{
		_startupReady = false;
	}

	public void ShowPendingStartupSettings()
	{
		if (_pendingStartupSettings)
		{
			_pendingStartupSettings = false;
			ShowSettingsPanel(_pendingStartupSection);
		}
	}

	public Task PrepareStartupAsync()
	{
		return _startupPreparation ?? (_startupPreparation = PrepareStartupCoreAsync());
	}

	private async Task PrepareStartupCoreAsync()
	{
		_startupReady = false;
		await _viewModel.WaitForVisibleIconsAsync();
		Stopwatch watch = Stopwatch.StartNew();
		bool activated = ShowActivated;
		ShowActivated = false;
		int value = 1;
		nint hwnd = new WindowInteropHelper(this).EnsureHandle();
		bool cloaked = NativeMethods.SetDwmWindowAttribute(hwnd, 13, ref value, 4) == 0;
		try
		{
			PreWarm();
			_isPreWarming = true;
			ParkOpenPose();
			if (!cloaked)
			{
				Opacity = 0.0;
			}
			IsHitTestVisible = false;
			await Dispatcher.InvokeAsync(() =>
			{
			}, DispatcherPriority.ContextIdle);
			_isWindowAnimating = true;
			_animatingStartTicks = DateTime.UtcNow.Ticks;
			BeginFrameProbe("startup-prepare");
			_startupSceneReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			StartOpenMotion();
			bool failure = await Task.WhenAny(_startupSceneReady.Task, Task.Delay(2000)) == _startupSceneReady.Task;
			NativeMethods.DwmFlush();
			App.Log($"[STARTUP-READY] cost={watch.Elapsed.TotalMilliseconds:F0}ms cloaked={cloaked} sceneCompleted={failure}");
		}
		finally
		{
			EndFrameProbe();
			_isWindowAnimating = false;
			ParkOpenPose();
			Opacity = 0.0;
			IsHitTestVisible = false;
			Hide();
			value = 0;
			NativeMethods.SetDwmWindowAttribute(hwnd, 13, ref value, 4);
			ShowActivated = activated;
			_isPreWarming = false;
			_isPreWarmed = false;
			_startupReady = true;
		}
	}

	public async Task WaitForPreparedFrameAsync()
	{
		TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		TimeSpan last = TimeSpan.MinValue;
		int frames = 0;
		EventHandler handler = (object? _, EventArgs args) =>
		{
			if (args is RenderingEventArgs e && e.RenderingTime != last)
			{
				last = e.RenderingTime;
				if (++frames >= 2)
				{
					ready.TrySetResult();
				}
			}
		};
		CompositionTarget.Rendering += handler;
		try
		{
			await Task.WhenAny(ready.Task, Task.Delay(200));
		}
		finally
		{
			CompositionTarget.Rendering -= handler;
		}
	}

	public void RepositionToCurrentMonitor()
	{
		MonitorBoundsInfo currentMonitorBounds = ScreenHelper.GetCurrentMonitorBounds(this, _viewModel.Settings.OpenOnCurrentMonitor);
		Left = currentMonitorBounds.LogicalLeft;
		Top = currentMonitorBounds.LogicalTop;
		Width = currentMonitorBounds.LogicalWidth;
		Height = currentMonitorBounds.LogicalHeight;
		BackdropImage.Width = currentMonitorBounds.LogicalWidth;
		BackdropImage.Height = currentMonitorBounds.LogicalHeight;
		App.Log($"Window positioned: L={Left}, T={Top}, W={Width}, H={Height}, PhysW={currentMonitorBounds.PhysicalWidth}, PhysH={currentMonitorBounds.PhysicalHeight}, Dpi={currentMonitorBounds.DpiScaleX}");
	}

	private void ApplyBackdropMode()
	{
		AppSettings settings = _viewModel.Settings;
		bool validated = settings.Backdrop != BackdropKind.Solid;
		BackdropImage.Visibility = ((!validated) ? Visibility.Collapsed : Visibility.Visible);
		double pageNumber = ((settings.Backdrop == BackdropKind.None) ? 0.0 : Math.Max(settings.ScrimOpacity, ThemeManager.VeilFloor()));
		BackdropVeil.Opacity = (validated ? pageNumber : 0.0);
		if (BackdropAmbientOverlay != null)
		{
			BackdropAmbientOverlay.Visibility = ((!validated || settings.Backdrop == BackdropKind.None) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (settings.Backdrop != BackdropKind.Solid && ThemeManager.Effective != AppTheme.HighContrast)
		{
			FolderPopup.Background = Brushes.Transparent;
		}
		else
		{
			FolderPopup.SetResourceReference(Border.BackgroundProperty, "T.Sheet_FF");
		}
		Grid folderOverlayModal = FolderOverlayModal;
		if (folderOverlayModal != null && folderOverlayModal.Visibility == Visibility.Visible)
		{
			UpdateFolderFrost();
		}
		if (validated)
		{
			int settingIndex = ((settings.Backdrop != BackdropKind.Clear && settings.Backdrop != BackdropKind.None) ? ((int)Math.Clamp(settings.BackdropBlur, 0.0, 40.0)) : 0);
			string text = $"{settings.BackdropSource}|{settingIndex}|{settings.CustomBackdropPath}";
			if (_backdropKey != text || BackdropImage.Source == null)
			{
				MonitorBoundsInfo currentMonitorBounds = ScreenHelper.GetCurrentMonitorBounds(this, settings.OpenOnCurrentMonitor);
				BitmapSource backdrop = BackdropHelper.GetBackdrop(currentMonitorBounds, settingIndex, settings.BackdropSource, settings.CustomBackdropPath);
				BackdropImage.Source = backdrop;
				_backdropKey = ((backdrop != null) ? text : string.Empty);
				if (Visibility != Visibility.Visible || SharpBackdropImage.Source == null)
				{
					BitmapSource bitmapSource = ((Visibility == Visibility.Visible) ? BackdropHelper.GetBackdrop(currentMonitorBounds, 0, settings.BackdropSource, settings.CustomBackdropPath) : (BackdropHelper.CaptureScreen(currentMonitorBounds, 0) ?? BackdropHelper.GetBackdrop(currentMonitorBounds, 0, settings.BackdropSource, settings.CustomBackdropPath)));
					SharpBackdropImage.Source = bitmapSource;
					SharpBackdropImage.Visibility = ((bitmapSource == null) ? Visibility.Collapsed : Visibility.Visible);
				}
			}
			SyncWindowBase();
		}
		else
		{
			SharpBackdropImage.Source = null;
			SharpBackdropImage.Visibility = Visibility.Collapsed;
			_windowBaseSource = null;
			SetResourceReference(Control.BackgroundProperty, "T.Window_FF");
		}
		ApplyBackgroundTransparency();
		UpdateDockFrost();
		ScheduleSheetFrost();
	}

	private void ApplyBackgroundTransparency()
	{
		BackdropOpacityLayer.SetResourceReference(Panel.BackgroundProperty, "T.Window_FF");
		if (_viewModel.Settings.Backdrop == BackdropKind.Solid && !string.IsNullOrWhiteSpace(_viewModel.Settings.SolidBackgroundColor))
		{
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(_viewModel.Settings.SolidBackgroundColor);
				color.A = byte.MaxValue;
				BackdropOpacityLayer.Background = new SolidColorBrush(color);
			}
			catch (FormatException)
			{
			}
		}
		double backgroundOpacityPercent = _viewModel.Settings.BackgroundOpacityPercent;
		double pageNumber = (double.IsFinite(backgroundOpacityPercent) ? (Math.Clamp(backgroundOpacityPercent, 0.0, 100.0) / 100.0) : 1.0);
		BackdropOpacityLayer.Opacity = pageNumber;
		HwndSource hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
		if (hwndSource?.CompositionTarget != null)
		{
			bool validated = pageNumber < 1.0;
			hwndSource.CompositionTarget.BackgroundColor = (validated ? Colors.Transparent : Colors.Black);
			DwmApi.MARGINS pMarInset = new DwmApi.MARGINS(validated ? (-1) : 0, validated ? (-1) : 0, validated ? (-1) : 0, validated ? (-1) : 0);
			DwmApi.DwmExtendFrameIntoClientArea(hwndSource.Handle, ref pMarInset);
			if (validated)
			{
				Background = Brushes.Transparent;
				SharpBackdropImage.Visibility = Visibility.Collapsed;
			}
			else
			{
				_windowBaseSource = null;
				SyncWindowBase();
				SharpBackdropImage.Visibility = ((SharpBackdropImage.Source == null) ? Visibility.Collapsed : Visibility.Visible);
			}
		}
	}

	private void SyncWindowBase()
	{
		if (_viewModel.Settings.BackgroundOpacityPercent < 100.0)
		{
			Background = Brushes.Transparent;
			SharpBackdropImage.Visibility = Visibility.Collapsed;
			return;
		}
		if (_viewModel.Settings.Backdrop == BackdropKind.Solid)
		{
			_windowBaseSource = null;
			Background = BackdropOpacityLayer.Background;
			return;
		}
		ImageSource imageSource = SharpBackdropImage?.Source ?? BackdropImage.Source;
		if (imageSource == null)
		{
			_windowBaseSource = null;
			if (_windowBaseBrush != null)
			{
				_windowBaseBrush.ImageSource = null;
			}
			SetResourceReference(Control.BackgroundProperty, "T.Window_FF");
		}
		else if (imageSource != _windowBaseSource)
		{
			_windowBaseSource = imageSource;
			if (_windowBaseBrush == null)
			{
				_windowBaseBrush = new ImageBrush
				{
					Stretch = Stretch.Fill
				};
			}
			_windowBaseBrush.ImageSource = imageSource;
			Background = _windowBaseBrush;
		}
	}

	private void RebuildBackdrop()
	{
		_backdropKey = string.Empty;
		ApplyBackdropMode();
	}

	private void ScheduleBackdropRebuild()
	{
		if (_backdropTimer == null)
		{
			_backdropTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(260.0)
			};
			_backdropTimer.Tick += (object? s, EventArgs e) =>
			{
				_backdropTimer.Stop();
				RebuildBackdrop();
			};
		}
		_backdropTimer.Stop();
		_backdropTimer.Start();
	}

	public void AnimateShow()
	{
		if (_viewModel.Settings.DesktopResidentLaunchpad)
		{
			_residentLaunchpadPinned = _residentPinRequested;
			if (!_isWindowAnimating)
			{
				_residentPinRequested = false;
			}
			UpdateDesktopResidentMode();
		}
		if (!_startupReady)
		{
			HasStartupOpenRequest = true;
			return;
		}
		if (!_firstFramePrepared)
		{
			PreWarm();
		}
		IsHitTestVisible = true;
		App.Log($"AnimateShow called, isAnimating={_isWindowAnimating}, Visibility={Visibility}");
		long ticks = DateTime.UtcNow.Ticks;
		if (_isWindowAnimating && ticks - _animatingStartTicks > TimeSpan.FromMilliseconds(500.0).Ticks)
		{
			App.Log("[WATCHDOG] Resetting stuck _isWindowAnimating in AnimateShow");
			_isWindowAnimating = false;
		}
		if (!_isWindowAnimating || Visibility != Visibility.Visible)
		{
			_animatingStartTicks = ticks;
			_isWindowAnimating = true;
			_isAppLaunching = false;
			Stopwatch stopwatch = Stopwatch.StartNew();
			if (!_isPreWarmed)
			{
				RepositionToCurrentMonitor();
				_openStepTimings[0] = stopwatch.Elapsed.TotalMilliseconds;
				ApplyBackdropMode();
				_openStepTimings[1] = stopwatch.Elapsed.TotalMilliseconds - _openStepTimings[0];
				ResetPageTransforms();
				_openStepTimings[2] = stopwatch.Elapsed.TotalMilliseconds - _openStepTimings[0] - _openStepTimings[1];
			}
			else
			{
				_isPreWarmed = false;
			}
			BeginFrameProbe("open");
			_openWatch = stopwatch;
			ParkOpenPose();
			if (Visibility != Visibility.Visible)
			{
				Show();
			}
			UpdateLayout();
			ArmDockPileProbe();
			LayoutDock();
			EnsureForeground("shown");
			Focus();
			if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow)
			{
				SearchBox.Focus();
			}
			StartOpenMotion();
		}
	}

	private void ParkOpenPose()
	{
		BeginAnimation(UIElement.OpacityProperty, null);
		Opacity = 1.0;
		RootGrid.BeginAnimation(UIElement.OpacityProperty, null);
		RootGrid.Opacity = 1.0;
		SurfaceContent.BeginAnimation(UIElement.OpacityProperty, null);
		SurfaceContent.Opacity = 0.0;
		BlurredBackdropGroup.BeginAnimation(UIElement.OpacityProperty, null);
		BlurredBackdropGroup.Opacity = 1.0;
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ContentScaleTransform.ScaleX = 0.985;
		ContentScaleTransform.ScaleY = 0.985;
		ContentTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		ContentTranslateTransform.X = 0.0;
		ContentTranslateTransform.Y = 10.0;
		SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
		SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
		SearchPanel.Opacity = ((_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow) ? 1 : 0);
		if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow)
		{
			SearchPanelShift.Y = 0.0;
			SearchPanel.IsHitTestVisible = true;
		}
		if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType)
		{
			SearchPanelShift.Y = -70.0;
			SearchPanel.IsHitTestVisible = false;
			_viewModel.IsSearchSlidDown = false;
		}
		SettingsButton.BeginAnimation(UIElement.OpacityProperty, null);
		SettingsButton.Opacity = 1.0;
		TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, null);
		TopWidgetsPanel.Opacity = 1.0;
		DockLayer.BeginAnimation(UIElement.OpacityProperty, null);
		DockLayer.Opacity = 0.0;
		DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		DockLayerTranslate.Y = 10.0;
		for (int i = 0; i < _viewModel.CurrentPageApps.Count; i++)
		{
			if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
			{
				frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
				frameworkElement.Opacity = 1.0;
				TranslateTransform translateTransform = frameworkElement.RenderTransform as TranslateTransform;
				if (translateTransform == null)
				{
					translateTransform = (TranslateTransform)(frameworkElement.RenderTransform = new TranslateTransform());
				}
				translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				translateTransform.X = 0.0;
				translateTransform.Y = 0.0;
			}
		}
	}

	private void RestoreOpenRestPose()
	{
		SurfaceContent.BeginAnimation(UIElement.OpacityProperty, null);
		SurfaceContent.Opacity = 1.0;
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		ScaleTransform contentScaleTransform = ContentScaleTransform;
		double scaleX = (ContentScaleTransform.ScaleY = 1.0);
		contentScaleTransform.ScaleX = scaleX;
		ContentTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		TranslateTransform contentTranslateTransform = ContentTranslateTransform;
		scaleX = (ContentTranslateTransform.Y = 0.0);
		contentTranslateTransform.X = scaleX;
		DockLayer.BeginAnimation(UIElement.OpacityProperty, null);
		DockLayer.Opacity = 1.0;
		DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		DockLayerTranslate.Y = 0.0;
	}

	private void StartOpenMotion()
	{
		if (_openWatch != null)
		{
			_openMotionAtMs = _openWatch.Elapsed.TotalMilliseconds;
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(220.0);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			_isWindowAnimating = false;
			EndFrameProbe();
			RestoreOpenRestPose();
			if (_isPreWarming)
			{
				_startupSceneReady?.TrySetResult();
			}
			if (!_viewModel.IsOpen && !_isPreWarming)
			{
				AnimateHide(_isAppLaunching);
			}
			else
			{
				App.Log($"[OPEN-T] positioned={_openStepTimings[0]:F1} backdrop={_openStepTimings[1]:F1} pages={_openStepTimings[2]:F1} firstTick={_openFirstTickMs:F1} motionAt={_openMotionAtMs:F1} total={_openWatch?.Elapsed.TotalMilliseconds ?? 0.0:F0}ms");
				ScheduleNeighbourWarm();
			}
		};
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10.0, 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		SurfaceContent.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		DockLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10.0, 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		TrackDockFrost(220);
		App.Log($"[OPEN-UNIFIED] items={_viewModel.CurrentPageApps.Count} duration={220}ms");
	}

	private static void FadeInSlow(UIElement element, double delayMs)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = 0.0;
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200.0))
		{
			BeginTime = TimeSpan.FromMilliseconds(delayMs),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private static void FadeOutSlow(UIElement element, double delayMs)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = 1.0;
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200.0))
		{
			BeginTime = TimeSpan.FromMilliseconds(delayMs),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			}
		});
	}

	private void ResetSearchIdleTimer()
	{
		_searchIdleTimer?.Stop();
		if (_viewModel != null && _viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType && (_viewModel.IsSearchSlidDown || _viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow) && string.IsNullOrWhiteSpace(_viewModel.SearchText) && !SearchBox.IsKeyboardFocusWithin && !_viewModel.IsSearchRevealedByHover)
		{
			_searchIdleTimer?.Start();
		}
	}

	private void OnSearchIdleTimerTick(object? sender, EventArgs e)
	{
		_searchIdleTimer?.Stop();
		if (_viewModel != null && _viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType && string.IsNullOrWhiteSpace(_viewModel.SearchText) && _viewModel.IsSearchSlidDown && !SearchBox.IsKeyboardFocusWithin && !_viewModel.IsSearchRevealedByHover)
		{
			AnimateSearchSlide(slideDown: false);
		}
	}

	private static bool IsTypeToSearchKey(Key key)
	{
		if ((Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Windows)) != 0)
		{
			return false;
		}
		switch (key)
		{
		case Key.ImeProcessed:
			return true;
		case Key.A:
		case Key.B:
		case Key.C:
		case Key.D:
		case Key.E:
		case Key.F:
		case Key.G:
		case Key.H:
		case Key.I:
		case Key.J:
		case Key.K:
		case Key.L:
		case Key.M:
		case Key.N:
		case Key.O:
		case Key.P:
		case Key.Q:
		case Key.R:
		case Key.S:
		case Key.T:
		case Key.U:
		case Key.V:
		case Key.W:
		case Key.X:
		case Key.Y:
		case Key.Z:
			return true;
		default:
			if (key >= Key.D0 && key <= Key.D9)
			{
				return true;
			}
			if (key >= Key.NumPad0 && key <= Key.NumPad9)
			{
				return true;
			}
			return false;
		}
	}

	private void DismissEmptySearchForPaging()
	{
		if (!IsSettingsOpen && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow && string.IsNullOrWhiteSpace(_viewModel.SearchText))
		{
			_viewModel.IsSearchRevealedByHover = false;
			SearchPanel.IsHitTestVisible = false;
			if (SearchBox.IsKeyboardFocusWithin)
			{
				FocusManager.SetFocusedElement(FocusManager.GetFocusScope(SearchBox), null);
				Keyboard.ClearFocus();
				Focus();
			}
			AnimateSearchSlide(slideDown: false);
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
			SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
			SearchPanel.Opacity = 0.0;
			SearchPanelShift.Y = -70.0;
			SearchPanel.Visibility = Visibility.Collapsed;
		}
	}

	private void AnimateSearchSlide(bool slideDown, bool focus = true)
	{
		if (SearchPanel == null || SearchPanelShift == null || _viewModel == null)
		{
			return;
		}
		double opacity = SearchPanel.Opacity;
		double y = SearchPanelShift.Y;
		if (slideDown)
		{
			if (_viewModel.IsSearchSlidDown && SearchPanel.Visibility == Visibility.Visible && SearchPanel.IsHitTestVisible && SearchPanel.Opacity >= 0.99 && Math.Abs(SearchPanelShift.Y) < 1.0)
			{
				if (focus && !SearchBox.IsKeyboardFocusWithin)
				{
					SearchBox.Focus();
				}
				return;
			}
			_viewModel.IsSearchSlidDown = true;
			SearchPanel.Visibility = Visibility.Visible;
			SearchPanel.IsHitTestVisible = true;
			SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
			DoubleAnimation animation = new DoubleAnimation
			{
				From = y,
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(260.0),
				EasingFunction = new SpringEase
				{
					Bounce = 0.12
				}
			};
			DoubleAnimation animation2 = new DoubleAnimation
			{
				From = opacity,
				To = 1.0,
				Duration = TimeSpan.FromMilliseconds(200.0),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, animation);
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, animation2);
			if (TopWidgetsPanel != null && TopWidgetsScale != null && _viewModel.Settings.ShowTopWidgets)
			{
				TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, null);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				DoubleAnimation animation3 = new DoubleAnimation
				{
					To = 0.08,
					Duration = TimeSpan.FromMilliseconds(180.0),
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				DoubleAnimation animation4 = new DoubleAnimation
				{
					To = 0.96,
					Duration = TimeSpan.FromMilliseconds(180.0),
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, animation3);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation4);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation4);
			}
			if (focus)
			{
				SearchBox.Focus();
			}
			ResetSearchIdleTimer();
		}
		else
		{
			if (!string.IsNullOrWhiteSpace(_viewModel.SearchText))
			{
				return;
			}
			_viewModel.IsSearchSlidDown = false;
			_viewModel.IsSearchRevealedByHover = false;
			_searchIdleTimer?.Stop();
			SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
			DoubleAnimation animation5 = new DoubleAnimation
			{
				From = y,
				To = -70.0,
				Duration = TimeSpan.FromMilliseconds(220.0),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			DoubleAnimation doubleAnimation = new DoubleAnimation
			{
				From = opacity,
				To = 0.0,
				Duration = TimeSpan.FromMilliseconds(180.0),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			doubleAnimation.Completed += (object? s, EventArgs e) =>
			{
				if (!_viewModel.IsSearchSlidDown && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
				{
					SearchPanel.IsHitTestVisible = false;
					SearchPanel.ClearValue(UIElement.VisibilityProperty);
					if (SearchBox.IsKeyboardFocusWithin)
					{
						Focus();
					}
				}
			};
			SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, animation5);
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			if (TopWidgetsPanel != null && TopWidgetsScale != null && _viewModel.Settings.ShowTopWidgets)
			{
				TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, null);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				DoubleAnimation animation6 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(240.0),
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				DoubleAnimation animation7 = new DoubleAnimation
				{
					To = 1.0,
					Duration = TimeSpan.FromMilliseconds(240.0),
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, animation6);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation7);
				TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation7);
			}
		}
	}

	private Point CentreInRoot(FrameworkElement element)
	{
		double pageNumber = ((element.ActualWidth > 0.0) ? element.ActualWidth : element.Width);
		if (double.IsNaN(pageNumber) || pageNumber <= 0.0)
		{
			pageNumber = 80.0;
		}
		double settingIndex = ((element.ActualHeight > 0.0) ? element.ActualHeight : element.Height);
		if (double.IsNaN(settingIndex) || settingIndex <= 0.0)
		{
			settingIndex = 80.0;
		}
		Point inPoint = new Point(pageNumber / 2.0, settingIndex / 2.0);
		try
		{
			if (RootGrid != null && element != RootGrid && element.IsDescendantOf(RootGrid))
			{
				GeneralTransform generalTransform = element.TransformToVisual(RootGrid);
				if (generalTransform != null && generalTransform.TryTransform(inPoint, out var result))
				{
					return result;
				}
			}
		}
		catch
		{
		}
		double num3 = ((RootGrid != null && RootGrid.ActualWidth > 0.0) ? RootGrid.ActualWidth : ActualWidth);
		if (num3 <= 0.0)
		{
			num3 = ((Width > 0.0) ? Width : SystemParameters.PrimaryScreenWidth);
		}
		double num4 = ((RootGrid != null && RootGrid.ActualHeight > 0.0) ? RootGrid.ActualHeight : ActualHeight);
		if (num4 <= 0.0)
		{
			num4 = ((Height > 0.0) ? Height : SystemParameters.PrimaryScreenHeight);
		}
		return new Point(num3 / 2.0, num4 / 2.0);
	}

	public void AnimateHide(bool isAppLaunch = false)
	{
		App.Log($"AnimateHide called, isAppLaunch={isAppLaunch}, isAnimating={_isWindowAnimating}, Visibility={Visibility}");
		if (_dockShelfArmed)
		{
			DisarmDockShelf(instant: true);
		}
		long ticks = DateTime.UtcNow.Ticks;
		if (_isWindowAnimating && ticks - _animatingStartTicks > TimeSpan.FromMilliseconds(500.0).Ticks)
		{
			App.Log("[WATCHDOG] Resetting stuck _isWindowAnimating in AnimateHide");
			_isWindowAnimating = false;
		}
		if (Visibility != Visibility.Visible || _isWindowAnimating)
		{
			return;
		}
		if (IsSettingsOpen)
		{
			StopHotkeyCapture();
			SettingsOverlayModal.Visibility = Visibility.Collapsed;
			_surfaceBlur.BeginAnimation(BlurEffect.RadiusProperty, null);
			_surfaceBlur.Radius = 0.0;
			SurfaceContent.Effect = null;
			CancelMotionPreview();
			_settingsVm.FlushPendingSaveAsync();
		}
		_animatingStartTicks = ticks;
		_isWindowAnimating = true;
		BeginFrameProbe("close");
		bool pageNumber = !isAppLaunch && !_viewModel.IsFolderOpen;
		SpringEase easingFunction = new SpringEase
		{
			Bounce = 0.0
		};
		DoubleAnimation animation;
		DoubleAnimation animation2;
		DoubleAnimation doubleAnimation;
		if (pageNumber)
		{
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(300.0);
			CubicEase easingFunction2 = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			};
			animation = new DoubleAnimation(1.0, 0.34, timeSpan)
			{
				EasingFunction = easingFunction2
			};
			animation2 = new DoubleAnimation(1.0, 0.06, timeSpan)
			{
				EasingFunction = easingFunction2
			};
			double settingIndex = ((RootGrid.ActualWidth > 0.0) ? RootGrid.ActualWidth : ActualWidth);
			if (settingIndex <= 0.0)
			{
				settingIndex = ((Width > 0.0) ? Width : SystemParameters.PrimaryScreenWidth);
			}
			double num3 = ((RootGrid.ActualHeight > 0.0) ? RootGrid.ActualHeight : ActualHeight);
			if (num3 <= 0.0)
			{
				num3 = ((Height > 0.0) ? Height : SystemParameters.PrimaryScreenHeight);
			}
			Point point = DockAnchor();
			Point point2 = CentreInRoot(SurfaceContent);
			if (point2.X == 0.0 && point2.Y == 0.0)
			{
				point2 = new Point(settingIndex / 2.0, num3 / 2.0);
			}
			ContentTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
			ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			ContentTranslateTransform.X = 0.0;
			ContentTranslateTransform.Y = 0.0;
			ContentTranslateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, (point.X - point2.X) * 0.8, timeSpan)
			{
				EasingFunction = easingFunction2
			});
			ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, (point.Y - point2.Y) * 0.8, timeSpan)
			{
				EasingFunction = easingFunction2
			});
			int num4 = Math.Max(1, _viewModel.Columns);
			int count = _viewModel.CurrentPageApps.Count;
			int num5 = ((count > 0) ? ((count - 1) / num4) : 0) + num4 - 1;
			int num6 = 0;
			for (int i = 0; i < count; i++)
			{
				if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement frameworkElement)
				{
					TranslateTransform translateTransform = frameworkElement.RenderTransform as TranslateTransform;
					if (translateTransform == null)
					{
						translateTransform = (TranslateTransform)(frameworkElement.RenderTransform = new TranslateTransform());
					}
					double x = translateTransform.X;
					double y = translateTransform.Y;
					translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
					translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
					translateTransform.X = x;
					translateTransform.Y = y;
					Point point3 = CentreInRoot(frameworkElement);
					double num7 = (double)(num5 - (i / num4 + i % num4)) * 6.0;
					CubicEase easingFunction3 = new CubicEase
					{
						EasingMode = EasingMode.EaseIn
					};
					translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, x + (point.X - point3.X) * 0.3, TimeSpan.FromMilliseconds(220.0))
					{
						BeginTime = TimeSpan.FromMilliseconds(num7),
						EasingFunction = easingFunction3
					});
					translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, y + (point.Y - point3.Y) * 0.3, TimeSpan.FromMilliseconds(220.0))
					{
						BeginTime = TimeSpan.FromMilliseconds(num7),
						EasingFunction = easingFunction3
					});
					FadeOutSlow(frameworkElement, num7);
					num6++;
				}
			}
			SineEase easingFunction4 = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			};
			doubleAnimation = new DoubleAnimation(1.0, 0.0, timeSpan)
			{
				EasingFunction = easingFunction4
			};
			SearchPanel.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			SettingsButton.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			double y2 = DockLayerTranslate.Y;
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(y2, 68.0, TimeSpan.FromMilliseconds(210.0))
			{
				BeginTime = TimeSpan.FromMilliseconds(90.0),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			};
			doubleAnimation2.Completed += (object? s, EventArgs e) =>
			{
				double value = ((DockBar == null) ? 0.0 : DockBar.TranslatePoint(new Point(0.0, DockBar.ActualHeight), this).Y);
				App.Log($"[DBG][DOCK-SINK] y={DockLayerTranslate.Y:F1} plateBottom={value:F0} winH={ActualHeight:F0}");
			};
			DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			DockLayerTranslate.Y = y2;
			DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation2);
			DockLayer.BeginAnimation(UIElement.OpacityProperty, null);
			DockLayer.Opacity = 1.0;
			DockLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160.0))
			{
				BeginTime = TimeSpan.FromMilliseconds(140.0),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseIn
				}
			});
			TrackDockFrost(320);
			App.Log($"[CLOSE-GENIE] items={count} moved={num6} dock=({point.X:F0},{point.Y:F0}) travel={(point.Y - point2.Y) * 0.8:F0}px");
		}
		else
		{
			TimeSpan timeSpan2 = TimeSpan.FromMilliseconds(180.0);
			double toValue = (isAppLaunch ? 1.08 : 0.94);
			EasingFunctionBase easingFunction5 = (isAppLaunch ? new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			} : new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			});
			doubleAnimation = new DoubleAnimation(1.0, 0.0, timeSpan2)
			{
				EasingFunction = easingFunction5
			};
			animation = new DoubleAnimation(1.0, toValue, timeSpan2)
			{
				EasingFunction = easingFunction
			};
			animation2 = new DoubleAnimation(1.0, toValue, timeSpan2)
			{
				EasingFunction = easingFunction
			};
			SurfaceContent.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			DockLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
		DoubleAnimation doubleAnimation3 = new DoubleAnimation(1.0, 1.0, doubleAnimation.Duration);
		doubleAnimation3.Completed += (object? s, EventArgs e) =>
		{
			_isWindowAnimating = false;
			EndFrameProbe();
			Visibility = Visibility.Hidden;
			BlurredBackdropGroup.BeginAnimation(UIElement.OpacityProperty, null);
			BlurredBackdropGroup.Opacity = 1.0;
			SurfaceContent.BeginAnimation(UIElement.OpacityProperty, null);
			if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType)
			{
				SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
				SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
				SearchPanel.Opacity = 0.0;
				SearchPanelShift.Y = -70.0;
				SearchPanel.IsHitTestVisible = false;
				_viewModel.IsSearchSlidDown = false;
			}
			else
			{
				SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
				SearchPanel.Opacity = 1.0;
				SearchPanelShift.Y = 0.0;
			}
			SettingsButton.BeginAnimation(UIElement.OpacityProperty, null);
			SettingsButton.Opacity = 1.0;
			TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, null);
			TopWidgetsPanel.Opacity = 1.0;
			DockLayer.BeginAnimation(UIElement.OpacityProperty, null);
			DockLayer.Opacity = 1.0;
			DockLayerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			DockLayerTranslate.Y = 0.0;
			ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			ContentScaleTransform.ScaleX = 1.0;
			ContentScaleTransform.ScaleY = 1.0;
			ContentTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
			ContentTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			ContentTranslateTransform.X = 0.0;
			ContentTranslateTransform.Y = 0.0;
			for (int j = 0; j < _viewModel.CurrentPageApps.Count; j++)
			{
				if (_primaryItems.ItemContainerGenerator.ContainerFromIndex(j) is FrameworkElement frameworkElement2)
				{
					if (frameworkElement2.RenderTransform is TranslateTransform translateTransform2)
					{
						translateTransform2.BeginAnimation(TranslateTransform.XProperty, null);
						translateTransform2.BeginAnimation(TranslateTransform.YProperty, null);
						translateTransform2.X = 0.0;
						translateTransform2.Y = 0.0;
					}
					frameworkElement2.BeginAnimation(UIElement.OpacityProperty, null);
					frameworkElement2.Opacity = 1.0;
				}
			}
			_isAppLaunching = false;
			_isPointerDown = false;
			_isMouseDownOnApp = false;
			UnhookFrameRendering();
			ReleaseHiddenRenderMotion();
			_idleSettleTimer?.Stop();
			SetSlidingLayersCacheEnabled(enabled: false);
			_searchIdleTimer?.Stop();
			_viewModel.ClearSearch();
			_viewModel.CloseFolder();
			App.Log("AnimateHide completed, window Hidden");
		};
		BlurredBackdropGroup.BeginAnimation(UIElement.OpacityProperty, doubleAnimation3);
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		ContentScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);
	}

	private Point DockAnchor()
	{
		Rect rect = DockBarRectInWindow();
		if (!rect.IsEmpty && _dockSlots.Count > 0)
		{
			return new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
		}
		double pageNumber = ((RootGrid.ActualWidth > 0.0) ? RootGrid.ActualWidth : ActualWidth);
		if (pageNumber <= 0.0)
		{
			pageNumber = ((Width > 0.0) ? Width : SystemParameters.PrimaryScreenWidth);
		}
		double settingIndex = ((RootGrid.ActualHeight > 0.0) ? RootGrid.ActualHeight : ActualHeight);
		if (settingIndex <= 0.0)
		{
			settingIndex = ((Height > 0.0) ? Height : SystemParameters.PrimaryScreenHeight);
		}
		return new Point(pageNumber / 2.0, settingIndex + 24.0);
	}

	private void OnFolderOverlayMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource == FolderOverlayModal || e.OriginalSource == FolderOverlayScrim)
		{
			_viewModel.CloseFolder();
			_clickDismissedFolder = true;
			e.Handled = true;
		}
	}

	private void OnSettingsPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ScrollViewer scrollViewer = SettingsScroll;
		DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
		while (dependencyObject != null && dependencyObject != SettingsScroll)
		{
			if (dependencyObject is ScrollViewer { ScrollableHeight: >0.0 } scrollViewer2 && ((e.Delta < 0 && scrollViewer2.VerticalOffset < scrollViewer2.ScrollableHeight) || (e.Delta > 0 && scrollViewer2.VerticalOffset > 0.0)))
			{
				scrollViewer = scrollViewer2;
				break;
			}
			if (dependencyObject is Visual)
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}
			else
			{
				dependencyObject = ((dependencyObject is FrameworkContentElement frameworkContentElement) ? frameworkContentElement.Parent : null);
			}
		}
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (double)e.Delta * 0.5);
		e.Handled = true;
	}

	private void OnWindowMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (IsSettingsOpen)
		{
			return;
		}
		long ticks = DateTime.UtcNow.Ticks;
		e.Handled = e.Delta != 0;
		if (_wheelGesture.TryStep(e.Delta, horizontal: false, ticks))
		{
			if (e.Delta < 0)
			{
				_lastSwipeEndTimeTicks = ticks;
				_viewModel.NextPage();
			}
			else if (e.Delta > 0)
			{
				_lastSwipeEndTimeTicks = ticks;
				_viewModel.PreviousPage();
			}
		}
	}

	private void OnHotkeyCaptureClick(object sender, RoutedEventArgs e)
	{
		if (_capturingHotkey)
		{
			StopHotkeyCapture();
			return;
		}
		_capturingHotkey = true;
		_settingsVm.IsCapturingHotkey = true;
		App.SetHotkeyCapture(capturing: true);
	}

	private void StopHotkeyCapture()
	{
		if (_capturingHotkey)
		{
			_capturingHotkey = false;
			_settingsVm.IsCapturingHotkey = false;
			App.SetHotkeyCapture(capturing: false);
		}
	}

	private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (!_capturingHotkey && !_isItemDragging && e.Key == Key.R && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
		{
			if (!e.IsRepeat)
			{
				_viewModel.Settings.DesktopResidentLaunchpad = !_viewModel.Settings.DesktopResidentLaunchpad;
			}
			e.Handled = true;
			return;
		}
		if (_viewModel.Settings.DesktopResidentLaunchpad && !_capturingHotkey && !_isItemDragging && e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
		{
			if (!e.IsRepeat && !_residentLayerHotkey.IsRegistered)
			{
				ToggleResidentLayerFromHotkey();
			}
			e.Handled = true;
			return;
		}
		if (!_capturingHotkey && !_isItemDragging && e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
		{
			if (!e.IsRepeat)
			{
				_viewModel.Settings.IconsLocked = !_viewModel.Settings.IconsLocked;
			}
			e.Handled = true;
			return;
		}
		if (!_capturingHotkey && !_isItemDragging && Keyboard.Modifiers == ModifierKeys.Control && (!(Keyboard.FocusedElement is TextBoxBase textBoxBase) || (textBoxBase == SearchBox && string.IsNullOrEmpty(SearchBox.Text))))
		{
			int pageNumber;
			if (e.Key >= Key.D1 && e.Key <= Key.D9)
			{
				pageNumber = (int)(e.Key - 34);
			}
			else
			{
				int settingIndex;
				if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
				{
					settingIndex = (int)(e.Key - 74);
				}
				else
				{
					Key key = e.Key;
					bool validated = ((key == Key.D0 || key == Key.NumPad0) ? true : false);
					settingIndex = (validated ? 10 : 0);
				}
				pageNumber = settingIndex;
			}
			int num3 = pageNumber;
			int num4 = ((e.Key == Key.Left) ? (-1) : ((e.Key == Key.Right) ? 1 : 0));
			if ((num3 > 0 || num4 != 0) && e.IsRepeat)
			{
				e.Handled = true;
				return;
			}
			if ((num3 > 0 || num4 != 0) && _settingsVm.TrySwitchSceneShortcut(num3, num4))
			{
				e.Handled = true;
				return;
			}
		}
		if (e.Key == Key.Escape && (IsSettingsOpen || (_settingsEscapeHeld && e.IsRepeat)))
		{
			_settingsEscapeHeld = true;
			if (_capturingHotkey)
			{
				StopHotkeyCapture();
			}
			if (IsSettingsOpen)
			{
				CloseSettingsPanel("settings-escape");
			}
			e.Handled = true;
			return;
		}
		if (!e.IsRepeat)
		{
			_settingsEscapeHeld = false;
		}
		if (e.Key == Key.Escape && _viewModel.Settings.DesktopResidentLaunchpad && !_capturingHotkey && !_isItemDragging && (!(Keyboard.FocusedElement is TextBox textBox) || textBox == SearchBox))
		{
			OnWindowKeyDown(sender, e);
		}
		else if (!_capturingHotkey)
		{
			if (!IsSettingsOpen && (!(Keyboard.FocusedElement is TextBox textBox2) || textBox2 == SearchBox) && IsTypeToSearchKey(e.Key))
			{
				if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
				{
					AnimateSearchSlide(slideDown: true);
				}
				else if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow)
				{
					SearchBox.Focus();
				}
			}
		}
		else if (e.Key == Key.Escape)
		{
			StopHotkeyCapture();
			e.Handled = true;
		}
		else
		{
			if (Keyboard.FocusedElement is TextBox textBox3 && textBox3 != SearchBox)
			{
				return;
			}
			e.Handled = true;
			Key key2 = ((e.Key == Key.System) ? e.SystemKey : e.Key);
			if (((uint)(key2 - 70) <= 1u || (uint)(key2 - 116) <= 5u) ? true : false)
			{
				return;
			}
			uint num5 = (uint)(((Keyboard.Modifiers & ModifierKeys.Control) != 0) ? 2 : 0) | (((Keyboard.Modifiers & ModifierKeys.Alt) != 0) ? 1u : 0u) | (uint)(((Keyboard.Modifiers & ModifierKeys.Shift) != 0) ? 4 : 0) | (uint)(((Keyboard.Modifiers & ModifierKeys.Windows) != 0) ? 8 : 0);
			if (num5 == 0)
			{
				_viewModel.StatusMessage = "至少要带一个修饰键（Ctrl / Alt / Shift / Win）";
				return;
			}
			uint key3 = (uint)KeyInterop.VirtualKeyFromKey(key2);
			if (!App.TryAdoptHotkey(num5, key3, out string error))
			{
				_viewModel.StatusMessage = "注册失败，这个组合可能已被其它程序占用（" + error + "）";
			}
			StopHotkeyCapture();
		}
	}

	private void OnWindowPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		if (IsSettingsOpen || _capturingHotkey || (Keyboard.FocusedElement is TextBox textBox && textBox != SearchBox))
		{
			return;
		}
		if (e.Source == SearchBox || e.OriginalSource == SearchBox || (e.OriginalSource is DependencyObject descendant && SearchBox.IsAncestorOf(descendant)))
		{
			if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]) && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
			{
				AnimateSearchSlide(slideDown: true);
			}
		}
		else if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
		{
			if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType)
			{
				AnimateSearchSlide(slideDown: true);
			}
			else if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysHidden)
			{
				AnimateSearchSlide(slideDown: true);
			}
			else
			{
				SearchBox.Focus();
			}
			SearchBox.AppendText(e.Text);
			SearchBox.CaretIndex = SearchBox.Text.Length;
			e.Handled = true;
		}
	}

	private void OnWindowKeyDown(object sender, KeyEventArgs e)
	{
		if (IsSettingsOpen)
		{
			if (e.Key == Key.Escape || (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
			{
				CloseSettingsPanel("key-esc-or-ctrl-comma");
				e.Handled = true;
			}
			return;
		}
		bool validated = Keyboard.FocusedElement == SearchBox;
		if (validated && IsTypeToSearchKey(e.Key) && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
		{
			AnimateSearchSlide(slideDown: true);
		}
		else
		{
			if ((Keyboard.FocusedElement is TextBox && !validated) || (validated && (e.Key == Key.Home || e.Key == Key.End || e.Key == Key.Prior || e.Key == Key.Next)))
			{
				return;
			}
			if (e.Key == Key.Escape)
			{
				if (_viewModel.IsFolderOpen)
				{
					_viewModel.CloseFolder();
				}
				else if (_viewModel.Settings.DesktopResidentLaunchpad)
				{
					_viewModel.ClearSearch();
					_viewModel.IsSearchRevealedByHover = false;
					ResetSearchIdleTimer();
					if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
					{
						AnimateSearchSlide(slideDown: false);
					}
					if (!TryAutoUnpinResidentLaunchpad())
					{
						_viewModel.HideLaunchpad();
					}
				}
				else if (!string.IsNullOrWhiteSpace(_viewModel.SearchText))
				{
					_viewModel.ClearSearch();
					ResetSearchIdleTimer();
				}
				else if (_viewModel.IsSearchRevealedByHover)
				{
					_viewModel.IsSearchRevealedByHover = false;
					if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
					{
						AnimateSearchSlide(slideDown: false);
					}
				}
				else if (_viewModel.IsSearchSlidDown && _viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
				{
					AnimateSearchSlide(slideDown: false);
				}
				else
				{
					App.Log("ESC key pressed");
					_viewModel.HideLaunchpad();
				}
				e.Handled = true;
			}
			else if (!validated && (e.Key == Key.Oem2 || e.Key == Key.Divide))
			{
				_viewModel.IsSearchRevealedByHover = true;
				AnimateSearchSlide(slideDown: true);
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				_viewModel.FlushSearchNow();
				if (_viewModel.CurrentPageApps.Count > 0)
				{
					_isAppLaunching = true;
					if (_viewModel.Settings.SearchDisplayMode != SearchDisplayMode.AlwaysShow)
					{
						AnimateSearchSlide(slideDown: false);
					}
					_viewModel.LaunchApp(_viewModel.CurrentPageApps[0]);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Left || e.Key == Key.Right)
			{
				if (!validated || string.IsNullOrWhiteSpace(_viewModel.SearchText))
				{
					if (e.Key == Key.Left)
					{
						_viewModel.PreviousPage();
					}
					else
					{
						_viewModel.NextPage();
					}
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Next)
			{
				_viewModel.NextPage();
				e.Handled = true;
			}
			else if (e.Key == Key.Prior)
			{
				_viewModel.PreviousPage();
				e.Handled = true;
			}
			else if (e.Key == Key.Home)
			{
				_viewModel.SelectPage(1);
				e.Handled = true;
			}
			else if (e.Key == Key.End)
			{
				_viewModel.SelectPage(_viewModel.TotalPages);
				e.Handled = true;
			}
			else if (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
			{
				ShowSettingsPanel();
				e.Handled = true;
			}
		}
	}

	private void OnSettingsVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (!(propertyName == "ActiveSection"))
		{
			if (propertyName == "Settings" && _liveSettingsSource != _settingsVm.Settings)
			{
				if (_liveSettingsSource != null)
				{
					_liveSettingsSource.PropertyChanged -= OnLiveSettingChanged;
				}
				_liveSettingsSource = _settingsVm.Settings;
				_liveSettingsSource.PropertyChanged += OnLiveSettingChanged;
			}
		}
		else
		{
			StopHotkeyCapture();
			SettingsScroll.ScrollToTop();
			Dispatcher.BeginInvoke(new Action(SettingsScroll.ScrollToTop), DispatcherPriority.Background);
		}
	}

	private void OnLiveSettingChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == null)
		{
			return;
		}
		switch (propertyName.Length)
		{
		case 12:
			switch (propertyName[0])
			{
			default:
				return;
			case 'S':
				break;
			case 'B':
				if (propertyName == "BackdropBlur")
				{
					ScheduleBackdropRebuild();
				}
				return;
			case 'D':
				_ = propertyName == "DockIconSize";
				return;
			}
			if (!(propertyName == "ScrimPercent"))
			{
				break;
			}
			goto IL_0176;
		case 11:
			switch (propertyName[6])
			{
			default:
				return;
			case 'T':
				if (!(propertyName == "MotionTempo"))
				{
					return;
				}
				break;
			case 'C':
				if (!(propertyName == "MotionCurve"))
				{
					return;
				}
				break;
			}
			ScheduleMotionPreview();
			break;
		case 5:
			if (propertyName == "Theme")
			{
				ThemeManager.Apply(_viewModel.Settings.Theme);
				ApplyBackdropMode();
			}
			break;
		case 8:
			if (!(propertyName == "Backdrop"))
			{
				break;
			}
			goto IL_0176;
		case 24:
			if (!(propertyName == "BackgroundOpacityPercent"))
			{
				break;
			}
			goto IL_017d;
		case 20:
			if (!(propertyName == "SolidBackgroundColor"))
			{
				break;
			}
			goto IL_017d;
		case 14:
			if (!(propertyName == "BackdropSource"))
			{
				break;
			}
			goto IL_018b;
		case 18:
			if (!(propertyName == "CustomBackdropPath"))
			{
				break;
			}
			goto IL_018b;
		case 23:
			if (propertyName == "EnableDockMagnification" && !_viewModel.Settings.EnableDockMagnification)
			{
				RestDock("magnify-disabled");
			}
			break;
		case 17:
			{
				if (!(propertyName == "SearchDisplayMode"))
				{
					break;
				}
				_searchIdleTimer?.Stop();
				_viewModel.IsSearchRevealedByHover = false;
				_viewModel.IsSearchSlidDown = false;
				if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysShow)
				{
					SearchPanel.Visibility = Visibility.Visible;
					SearchPanel.IsHitTestVisible = true;
					SearchPanelShift.BeginAnimation(TranslateTransform.YProperty, null);
					SearchPanel.BeginAnimation(UIElement.OpacityProperty, null);
					SearchPanelShift.Y = 0.0;
					SearchPanel.Opacity = 1.0;
					if (TopWidgetsPanel != null && TopWidgetsScale != null)
					{
						TopWidgetsPanel.BeginAnimation(UIElement.OpacityProperty, null);
						TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
						TopWidgetsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
						TopWidgetsPanel.Opacity = 1.0;
						TopWidgetsScale.ScaleX = 1.0;
						TopWidgetsScale.ScaleY = 1.0;
					}
				}
				else if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.SlideOnType)
				{
					if (string.IsNullOrWhiteSpace(_viewModel.SearchText))
					{
						AnimateSearchSlide(slideDown: false);
					}
					else
					{
						AnimateSearchSlide(slideDown: true, focus: false);
					}
				}
				else if (_viewModel.Settings.SearchDisplayMode == SearchDisplayMode.AlwaysHidden)
				{
					if (string.IsNullOrWhiteSpace(_viewModel.SearchText))
					{
						AnimateSearchSlide(slideDown: false);
						SearchPanel.Visibility = Visibility.Collapsed;
						SearchPanel.IsHitTestVisible = false;
					}
					else
					{
						AnimateSearchSlide(slideDown: true, focus: false);
					}
				}
				break;
			}
			IL_0176:
			ApplyBackdropMode();
			break;
			IL_018b:
			SharpBackdropImage.Source = null;
			RebuildBackdrop();
			break;
			IL_017d:
			ApplyBackgroundTransparency();
			break;
		}
	}

	private void ScheduleMotionPreview()
	{
		if (!IsSettingsOpen)
		{
			return;
		}
		if (_previewTimer == null)
		{
			_previewTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(480.0)
			};
			_previewTimer.Tick += (object? s, EventArgs e) =>
			{
				_previewTimer.Stop();
				PlayMotionPreview();
			};
		}
		_previewTimer.Stop();
		_previewTimer.Start();
	}

	private void PlayMotionPreview()
	{
		if (!IsSettingsOpen || _isPageAnimating || _isReturning || _isSwiping || _motionDriven || _isItemDragging || _viewModel.TotalPages < 2)
		{
			return;
		}
		int currentPage = _viewModel.CurrentPage;
		int targetPage = ((currentPage < _viewModel.TotalPages) ? (currentPage + 1) : (currentPage - 1));
		_previewReturnPage = currentPage;
		_viewModel.SelectPage(targetPage);
		if (_previewReturnTimer == null)
		{
			_previewReturnTimer = new DispatcherTimer();
			_previewReturnTimer.Tick += (object? s, EventArgs e) =>
			{
				_previewReturnTimer.Stop();
				if (_previewReturnPage >= 0)
				{
					int previewReturnPage = _previewReturnPage;
					_previewReturnPage = -1;
					if (_viewModel.CurrentPage != previewReturnPage)
					{
						_viewModel.SelectPage(previewReturnPage);
					}
				}
			};
		}
		_previewReturnTimer.Interval = TimeSpan.FromMilliseconds(_slideDurationMs + 260.0);
		_previewReturnTimer.Stop();
		_previewReturnTimer.Start();
	}

	private void CancelMotionPreview()
	{
		_previewTimer?.Stop();
		_previewReturnTimer?.Stop();
		_previewReturnPage = -1;
	}

	private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
	{
		ShowSettingsPanel();
	}

	private void OnMenuOpenSettings(object sender, RoutedEventArgs e)
	{
		ShowSettingsPanel();
	}

	private async void OnMenuAddApplication(object sender, RoutedEventArgs e)
	{
		e.Handled = true;
		await _viewModel.AddApplicationByPickingAsync();
	}

	private void OnMenuCloseLaunchpad(object sender, RoutedEventArgs e)
	{
		_viewModel.HideLaunchpad();
	}

	private void OnReturnToDesktopClick(object sender, RoutedEventArgs e)
	{
		App.Log("[LAUNCHPAD] Return to desktop clicked");
		_viewModel.HideLaunchpad();
	}

	private void OnSettingsCloseClick(object sender, RoutedEventArgs e)
	{
		CloseSettingsPanel("close-button");
	}

	private void OnSettingsOverlayMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource == SettingsOverlayModal || e.OriginalSource == SettingsScrim)
		{
			CloseSettingsPanel("scrim-press");
			e.Handled = true;
		}
	}

	private void AttachSheetSelectionHandler()
	{
		if (!_sheetSelectionAttached)
		{
			_sheetSelectionAttached = true;
			SettingsOverlayModal.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnSheetListPreviewMouseDown), handledEventsToo: true);
		}
	}

	private void OnSheetListPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left)
		{
			return;
		}
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (IsSettingsOpen && Keyboard.FocusedElement is TextBox textBox && textBox != SearchBox && !IsActive)
			{
				EnsureForeground("field-focus");
			}
		}), DispatcherPriority.Input);
		ListBoxItem listBoxItem = AncestorOfType<ListBoxItem>(e.OriginalSource as DependencyObject);
		ListBox listBox = AncestorOfType<ListBox>(e.OriginalSource as DependencyObject);
		if (listBoxItem != null && listBox != null)
		{
			listBox.SelectedItem = listBoxItem.Content;
			if (AncestorOfType<ButtonBase>(e.OriginalSource as DependencyObject) == null && AncestorOfType<TextBoxBase>(e.OriginalSource as DependencyObject) == null)
			{
				e.Handled = true;
				App.Log($"[SHEET-CLICK] selected={listBox.SelectedItem} section={_settingsVm.ActiveSection}");
			}
		}
	}

	private static T? AncestorOfType<T>(DependencyObject? node) where T : DependencyObject
	{
		while (node != null && !(node is T))
		{
			node = VisualTreeHelper.GetParent(node);
		}
		return node as T;
	}

	private static string DescribeSource(object? source)
	{
		DependencyObject dependencyObject = source as DependencyObject;
		if (dependencyObject == null)
		{
			return source?.GetType().Name ?? "null";
		}
		StringBuilder stringBuilder = new StringBuilder();
		int pageNumber = 0;
		while (dependencyObject != null && pageNumber < 7)
		{
			stringBuilder.Append(dependencyObject.GetType().Name);
			if (dependencyObject is FrameworkElement frameworkElement && frameworkElement.Name.Length > 0)
			{
				stringBuilder.Append('#').Append(frameworkElement.Name);
			}
			if (dependencyObject is ListBoxItem listBoxItem)
			{
				stringBuilder.Append("(sel=").Append(listBoxItem.IsSelected).Append(')');
			}
			stringBuilder.Append(" < ");
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			pageNumber++;
		}
		if (stringBuilder.Length <= 3)
		{
			return "null";
		}
		return stringBuilder.ToString(0, stringBuilder.Length - 3);
	}

	private static void RaiseOwnedDialog(Window dialog)
	{
		if (!dialog.IsVisible)
		{
			return;
		}
		dialog.Topmost = true;
		dialog.Activate();
		nint handle = new WindowInteropHelper(dialog).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		uint windowThreadProcessId = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out var _);
		uint currentThreadId = NativeMethods.GetCurrentThreadId();
		bool validated = false;
		try
		{
			if (!NativeMethods.SetForegroundWindow(handle) && windowThreadProcessId != 0 && windowThreadProcessId != currentThreadId)
			{
				validated = NativeMethods.AttachThreadInput(windowThreadProcessId, currentThreadId, fAttach: true);
				NativeMethods.SetForegroundWindow(handle);
			}
			NativeMethods.BringWindowToTop(handle);
			dialog.Activate();
		}
		finally
		{
			if (validated)
			{
				NativeMethods.AttachThreadInput(windowThreadProcessId, currentThreadId, fAttach: false);
			}
		}
	}

	public void ShowOwnedDialog(Window dialog)
	{
		dialog.Owner = this;
		Window activeOwnedDialog = _activeOwnedDialog;
		bool blocksDeactivation = _blocksDeactivation;
		bool topmost = Topmost;
		Topmost = false;
		_activeOwnedDialog = dialog;
		_blocksDeactivation = true;
		dialog.Topmost = true;
		dialog.ShowActivated = true;
		dialog.ContentRendered += OnRendered;
		try
		{
			App.Log("[DIALOG] showing " + dialog.GetType().Name + " ownerVisible=" + IsVisible + " ownerTopmost=" + Topmost);
			dialog.ShowDialog();
		}
		finally
		{
			dialog.ContentRendered -= OnRendered;
			_activeOwnedDialog = activeOwnedDialog;
			_blocksDeactivation = blocksDeactivation;
			Topmost = topmost;
			if (activeOwnedDialog != null && activeOwnedDialog.IsVisible)
			{
				RaiseOwnedDialog(activeOwnedDialog);
			}
			else if (IsVisible)
			{
				Activate();
			}
		}
		void OnRendered(object? sender, EventArgs args)
		{
			RaiseOwnedDialog(dialog);
		}
	}

	public void ShowSettingsPanel(string? section = null)
	{
		if (!_startupReady)
		{
			_pendingStartupSettings = true;
			_pendingStartupSection = section;
			if (!_viewModel.IsOpen)
			{
				_viewModel.OpenLaunchpad();
			}
			return;
		}
		if (!string.IsNullOrEmpty(section))
		{
			_settingsVm.ActiveSection = section;
		}
		if (IsSettingsOpen)
		{
			UpdateDesktopResidentMode();
			EnsureForeground("sheet-reopen");
			SettingsScroll.ScrollToTop();
			return;
		}
		App.Log("Opening the preferences sheet");
		CancelMotionPreview();
		bool pageNumber = _isPageAnimating || _isReturning || _motionDriven;
		bool isSwiping = _isSwiping;
		bool isReturning = _isReturning;
		if (_isPointerDown)
		{
			_isPointerDown = false;
		}
		if (Mouse.Captured != null)
		{
			App.Log("Releasing stale mouse capture from " + DescribeSource(Mouse.Captured));
			Mouse.Capture(null);
		}
		if (pageNumber)
		{
			_slideSeq++;
			_isPageAnimating = false;
			_isReturning = false;
			_currentTransitionTarget = -1;
			_queuedTransitionTarget = -1;
			_swipeTargetPage = -1;
			LogFramePhase(isReturning ? "return-kill" : "settle-kill", _slideStartTicks);
		}
		if (pageNumber | isSwiping)
		{
			_isSwiping = false;
			_swipeTargetPage = -1;
			ResetPageTransforms();
		}
		OpenSheetAsync();
	}

	public void MarkSettingsLoaded()
	{
		_settingsLoaded = true;
	}

	private async Task OpenSheetAsync()
	{
		if (!_settingsLoaded)
		{
			_settingsLoaded = true;
			await _settingsVm.LoadSettingsAsync();
		}
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		SettingsPopupShift.BeginAnimation(TranslateTransform.XProperty, null);
		SettingsPopupShift.BeginAnimation(TranslateTransform.YProperty, null);
		SettingsPopup.BeginAnimation(UIElement.OpacityProperty, null);
		SettingsScrim.BeginAnimation(UIElement.OpacityProperty, null);
		if (_sheetFlightTransform != null)
		{
			SettingsPopup.RenderTransform = _sheetFlightTransform;
		}
		ScaleTransform settingsPopupScale = SettingsPopupScale;
		double scaleX = (SettingsPopupScale.ScaleY = 0.9);
		settingsPopupScale.ScaleX = scaleX;
		SettingsPopupShift.X = 0.0;
		SettingsPopupShift.Y = 16.0;
		SettingsPopup.Opacity = 0.0;
		SettingsScrim.Opacity = 0.0;
		SettingsOverlayModal.Opacity = 1.0;
		SettingsOverlayModal.Visibility = Visibility.Visible;
		UpdateDesktopResidentMode();
		SettingsScroll.ScrollToTop();
		AttachSheetSelectionHandler();
		UpdateSheetFrost();
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(240.0);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.9, 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			RetireSheetTransform();
		};
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
		SettingsPopupShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16.0, 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		});
		SettingsPopup.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = easingFunction
		});
		SettingsScrim.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0))
		{
			EasingFunction = easingFunction
		});
		EnsureForeground("sheet-open");
	}

	private void RetireSheetTransform()
	{
		ScaleTransform settingsPopupScale = SettingsPopupScale;
		double scaleX = (SettingsPopupScale.ScaleY = 1.0);
		settingsPopupScale.ScaleX = scaleX;
		TranslateTransform settingsPopupShift = SettingsPopupShift;
		scaleX = (SettingsPopupShift.Y = 0.0);
		settingsPopupShift.X = scaleX;
		SettingsPopup.Opacity = 1.0;
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		SettingsPopupShift.BeginAnimation(TranslateTransform.XProperty, null);
		SettingsPopupShift.BeginAnimation(TranslateTransform.YProperty, null);
		SettingsPopup.BeginAnimation(UIElement.OpacityProperty, null);
		SettingsPopup.RenderTransform = null;
	}

	private void RampSurfaceBlur(TimeSpan duration, EasingFunctionBase flightCurve, bool ascending)
	{
	}

	public void CloseSettingsPanel(string via = "?")
	{
		if (IsSettingsOpen)
		{
			App.Log("Closing the preferences sheet via=" + via + " focus=" + DescribeSource(Keyboard.FocusedElement));
			StopHotkeyCapture();
			CancelMotionPreview();
			_settingsVm.FlushPendingSaveAsync();
			if (_sheetFlightTransform != null)
			{
				SettingsPopup.RenderTransform = _sheetFlightTransform;
			}
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(160.0);
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			};
			DoubleAnimation animation = new DoubleAnimation(SettingsPopupScale.ScaleX, 0.92, timeSpan)
			{
				EasingFunction = easingFunction
			};
			SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			SettingsPopupShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SettingsPopupShift.Y, 12.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			SettingsPopup.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.0, timeSpan)
			{
				EasingFunction = easingFunction
			});
			DoubleAnimation doubleAnimation = new DoubleAnimation(SettingsScrim.Opacity, 0.0, timeSpan)
			{
				EasingFunction = easingFunction
			};
			doubleAnimation.Completed += (object? s, EventArgs e) =>
			{
				SettingsOverlayModal.Visibility = Visibility.Collapsed;
				UpdateDesktopResidentMode();
				SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				SettingsPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				SettingsPopupShift.BeginAnimation(TranslateTransform.XProperty, null);
				SettingsPopupShift.BeginAnimation(TranslateTransform.YProperty, null);
				SettingsPopup.BeginAnimation(UIElement.OpacityProperty, null);
				SettingsScrim.BeginAnimation(UIElement.OpacityProperty, null);
				ScaleTransform settingsPopupScale = SettingsPopupScale;
				double scaleX = (SettingsPopupScale.ScaleY = 1.0);
				settingsPopupScale.ScaleX = scaleX;
				SettingsPopupShift.X = 0.0;
				SettingsPopupShift.Y = 0.0;
				SettingsPopup.Opacity = 1.0;
				SettingsScrim.Opacity = 1.0;
				SheetFrostLayer.Visibility = Visibility.Collapsed;
				SheetTintLayer.Visibility = Visibility.Collapsed;
			};
			SettingsScrim.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	private void OnDockWindowPreviewMouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: AppItem dataContext } frameworkElement)
		{
			(_dockWindowPreview ?? (_dockWindowPreview = new DockWindowPreview(_viewModel, this))).Hover(frameworkElement, dataContext);
		}
	}

	private void OnOpenWindowMouseRightDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is Button { DataContext: AppItem { IsOpenWindowEntry: not false } dataContext } button)
		{
			ContextMenu contextMenu = OpenWindowMenu.Create(_viewModel, dataContext, this);
			contextMenu.PlacementTarget = button;
			button.ContextMenu = contextMenu;
			contextMenu.IsOpen = true;
			e.Handled = true;
		}
	}

	private async void OnMenuAddFolder(object sender, RoutedEventArgs e)
	{
		bool previousBlock = _blocksDeactivation;
		_blocksDeactivation = true;
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "添加文件夹到启动台",
				Multiselect = true,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
			};
			if (openFolderDialog.ShowDialog(this) == true)
			{
				await _viewModel.AddDroppedItemsAsync(openFolderDialog.FolderNames);
			}
		}
		finally
		{
			_blocksDeactivation = previousBlock;
			if (IsVisible && !DesktopResidentSurface)
			{
				Activate();
			}
		}
	}

	private string[]? PickApplicationFiles()
	{
		bool blocksDeactivation = _blocksDeactivation;
		_blocksDeactivation = true;
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "添加应用 · 选择程序或快捷方式",
				Filter = "程序与快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.com;*.msi|所有文件|*.*",
				Multiselect = true,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
			};
			return (openFileDialog.ShowDialog(this) == true) ? openFileDialog.FileNames : null;
		}
		finally
		{
			if (IsVisible)
			{
				Activate();
			}
			_blocksDeactivation = blocksDeactivation;
		}
	}

	private void OnWindowDeactivated(object? sender, EventArgs e)
	{
		if ((!_viewModel.Settings.DesktopResidentLaunchpad || IsSettingsOpen) && !_isPreWarming && IsHitTestVisible)
		{
			App.Log("OnWindowDeactivated triggered");
			if (_isItemDragging)
			{
				EndInteractiveAppDrag(Mouse.GetPosition(this));
			}
			if (_viewModel.IsOpen && !_isWindowAnimating && !_isSwiping && !_blocksDeactivation && !PinOpen)
			{
				_viewModel.CloseLaunchpad();
			}
		}
	}

	internal async void OnMenuChangeIcon(object sender, RoutedEventArgs e)
	{
		if (!(sender is MenuItem { CommandParameter: AppItem commandParameter }))
		{
			return;
		}
		_blocksDeactivation = true;
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "为 " + commandParameter.Name + " 选择图标",
				Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|所有文件 (*.*)|*.*"
			};
			if (openFileDialog.ShowDialog(this) == true)
			{
				await _viewModel.ApplyIconFileAsync(commandParameter, openFileDialog.FileName);
			}
		}
		finally
		{
			_blocksDeactivation = false;
			Activate();
		}
	}

	internal async void OnMenuRenameFolder(object sender, RoutedEventArgs e)
	{
		if (!(sender is MenuItem { CommandParameter: AppItem { IsFolder: not false } commandParameter }))
		{
			return;
		}
		_blocksDeactivation = true;
		try
		{
			TextInputDialog textInputDialog = new TextInputDialog(this, "重命名文件夹", commandParameter.Name)
			{
				Topmost = true
			};
			if (textInputDialog.ShowDialog() == true)
			{
				await _viewModel.ApplyFolderRenameAsync(commandParameter, textInputDialog.Value);
			}
		}
		finally
		{
			_blocksDeactivation = false;
			Activate();
		}
	}

	internal async void OnMenuRenameApp(object sender, RoutedEventArgs e)
	{
		if (!(sender is MenuItem { CommandParameter: AppItem { IsFolder: false } commandParameter }))
		{
			return;
		}
		_blocksDeactivation = true;
		try
		{
			TextInputDialog textInputDialog = new TextInputDialog(this, "重命名", commandParameter.DisplayName)
			{
				Topmost = true
			};
			if (textInputDialog.ShowDialog() == true)
			{
				await _viewModel.ApplyAppRenameAsync(commandParameter, textInputDialog.Value);
			}
		}
		finally
		{
			_blocksDeactivation = false;
			Activate();
		}
	}

	private void OnFolderTitleGotFocus(object sender, RoutedEventArgs e)
	{
		if (sender is TextBox textBox)
		{
			textBox.SelectAll();
		}
	}

	private void OnFolderTitleKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return || e.Key == Key.Escape)
		{
			e.Handled = true;
			if (e.Key == Key.Escape && sender is TextBox textBox)
			{
				textBox.Text = _viewModel.ActiveFolder?.Name ?? textBox.Text;
			}
			Keyboard.ClearFocus();
			Focus();
		}
	}

	private async void OnFolderTitleLostFocus(object sender, RoutedEventArgs e)
	{
		if (!(sender is TextBox tb))
		{
			return;
		}
		AppItem folder = _viewModel.ActiveFolder;
		if (folder != null)
		{
			string text = tb.Text.Trim();
			if (text != folder.Name)
			{
				await _viewModel.ApplyFolderRenameAsync(folder, text);
			}
			tb.Text = folder.Name;
		}
	}

	private static bool IsDescendantOf(DependencyObject? node, DependencyObject target)
	{
		while (node != null)
		{
			if (node == target)
			{
				return true;
			}
			node = VisualTreeHelper.GetParent(node);
		}
		return false;
	}

	private static IDropTargetHelper? TryCreateDropTargetHelper()
	{
		try
		{
			return (IDropTargetHelper)new DragDropHelper();
		}
		catch
		{
			return null;
		}
	}

	private static object? GetOleDataObject(object? data)
	{
		if (data == null)
		{
			return null;
		}
		if (data is System.Runtime.InteropServices.ComTypes.IDataObject)
		{
			return data;
		}
		try
		{
			FieldInfo field = data.GetType().GetField("_innerData", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				object value = field.GetValue(data);
				if (value is System.Runtime.InteropServices.ComTypes.IDataObject)
				{
					return value;
				}
				if (value != null)
				{
					FieldInfo field2 = value.GetType().GetField("_innerData", BindingFlags.Instance | BindingFlags.NonPublic);
					if (field2 != null)
					{
						object failure = field2.GetValue(value);
						if (failure is System.Runtime.InteropServices.ComTypes.IDataObject)
						{
							return failure;
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void OnWindowPreviewDragEnter(object sender, DragEventArgs e)
	{
		if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			_isExternalFileDragging = true;
			try
			{
				_cachedDropFiles = e.Data.GetData(DataFormats.FileDrop) as string[];
			}
			catch
			{
				_cachedDropFiles = null;
			}
		}
		else
		{
			_isExternalFileDragging = false;
			_cachedDropFiles = null;
		}
		object oleDataObject = GetOleDataObject(e.Data);
		if (_dropTargetHelper != null && oleDataObject != null && _selfHwnd != IntPtr.Zero)
		{
			try
			{
				NativeMethods.GetCursorPos(out var lpPoint);
				_dropTargetHelper.DragEnter(_selfHwnd, oleDataObject, ref lpPoint, (int)e.Effects);
				_dropTargetHelper.Show(fShow: true);
			}
			catch
			{
			}
		}
		if (_isExternalFileDragging && _cachedDropFiles != null && _cachedDropFiles.Length != 0)
		{
			ShowExternalDragGhost(_cachedDropFiles, e.GetPosition(this));
		}
		UpdateDropTargetState(e);
	}

	private void OnWindowPreviewDragOver(object sender, DragEventArgs e)
	{
		if (_dropTargetHelper != null)
		{
			try
			{
				NativeMethods.GetCursorPos(out var lpPoint);
				_dropTargetHelper.DragOver(ref lpPoint, (int)e.Effects);
			}
			catch
			{
			}
		}
		Point position = e.GetPosition(this);
		UpdateExternalDragGhostPosition(position);
		UpdateDropTargetState(e);
	}

	private void OnWindowPreviewDragLeave(object sender, DragEventArgs e)
	{
		try
		{
			_dropTargetHelper?.DragLeave();
		}
		catch
		{
		}
		Point position = e.GetPosition(this);
		if (position.X <= 0.0 || position.Y <= 0.0 || position.X >= ActualWidth || position.Y >= ActualHeight)
		{
			ClearDropTargetHighlight();
			HideExternalDragGhost();
			_isExternalFileDragging = false;
			_cachedDropFiles = null;
		}
	}

	private async void OnWindowPreviewDrop(object sender, DragEventArgs e)
	{
		try
		{
			object oleDataObject = GetOleDataObject(e.Data);
			if (_dropTargetHelper != null && oleDataObject != null)
			{
				try
				{
					NativeMethods.GetCursorPos(out var lpPoint);
					_dropTargetHelper.Drop(oleDataObject, ref lpPoint, (int)e.Effects);
				}
				catch
				{
				}
			}
			string[] entries = null;
			if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				entries = e.Data.GetData(DataFormats.FileDrop) as string[];
			}
			if (entries == null || entries.Length == 0)
			{
				entries = _cachedDropFiles;
			}
			if (entries != null && entries.Length != 0)
			{
				_cachedDropFiles = entries;
				_isExternalFileDragging = true;
				UpdateDropTargetState(e);
			}
			AppItem currentDropApp = _currentDropApp;
			bool copyToFolder = e.Effects == DragDropEffects.Copy;
			bool validated = !IsSettingsOpen && e.Effects != DragDropEffects.None;
			ClearDropTargetHighlight();
			HideExternalDragGhost();
			_isExternalFileDragging = false;
			_cachedDropFiles = null;
			if ((entries != null && entries.Length != 0) & validated)
			{
				e.Handled = true;
				await HandleExternalFilesAsync(currentDropApp, entries, copyToFolder);
			}
		}
		catch (Exception ex)
		{
			App.Log("Drag and drop open failed: " + ex.Message);
			ClearDropTargetHighlight();
			HideExternalDragGhost();
		}
	}

	internal async Task HandleExternalFilesAsync(AppItem? target, string[] files, bool copyToFolder = false)
	{
		if (files.Length == 0)
		{
			return;
		}
		if (target != null && !target.IsFolder && !target.IsWidget && Directory.Exists(target.ExecutablePath))
		{
			if (!_folderTransferInProgress)
			{
				_folderTransferInProgress = true;
				bool previousBlock = _blocksDeactivation;
				_blocksDeactivation = true;
				try
				{
					_viewModel.ReportFileOperationFeedback(copyToFolder ? "正在复制到文件夹…" : "正在移入文件夹…");
					string message = await FolderDropService.TransferAsync(_selfHwnd, target.ExecutablePath, files, copyToFolder);
					_viewModel.ReportFileOperationFeedback(message);
					return;
				}
				finally
				{
					_blocksDeactivation = previousBlock;
					_folderTransferInProgress = false;
				}
			}
			_viewModel.ReportFileOperationFeedback("正在处理文件，请稍后再拖入。");
		}
		else if (files.Any(Directory.Exists))
		{
			await _viewModel.AddDroppedItemsAsync(files);
		}
		else if (target != null && !target.IsFolder && !target.IsWidget)
		{
			_isAppLaunching = _viewModel.Settings.CloseAfterLaunching && !_viewModel.Settings.DesktopResidentLaunchpad;
			if (!_viewModel.LaunchAppWithFiles(target, files))
			{
				_isAppLaunching = false;
			}
		}
		else if (target == null)
		{
			await _viewModel.AddDroppedItemsAsync(files);
		}
	}

	private void UpdateDropTargetState(DragEventArgs e)
	{
		if (IsSettingsOpen || !_isExternalFileDragging || _cachedDropFiles == null || _cachedDropFiles.Length == 0)
		{
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			ClearDropTargetHighlight();
			UpdateExternalDragGhostHint(null);
			if (IsSettingsOpen)
			{
				ExternalDragHint.Text = "关闭设置后再添加到启动台";
			}
			return;
		}
		Point position = e.GetPosition(this);
		if (_currentDropCardElement != null && _currentDropApp != null && !_currentDropApp.IsFolder && !_currentDropApp.IsWidget && _currentDropCardBounds.Contains(position))
		{
			e.Effects = ExternalTargetEffect(_currentDropApp, e.AllowedEffects);
			e.Handled = true;
			UpdateExternalDragGhostHint(_currentDropApp);
			return;
		}
		var (frameworkElement, appItem) = FindAppDropTarget((e.OriginalSource as DependencyObject) ?? (InputHitTest(position) as DependencyObject));
		if (_cachedDropFiles.Any(Directory.Exists) && (appItem == null || !Directory.Exists(appItem.ExecutablePath)))
		{
			CancelFolderHoverTimer();
			ClearDropTargetHighlight();
			UpdateExternalDragGhostHint(null);
			ExternalDragHint.Text = "松开添加文件夹入口（不移动原文件夹）";
			e.Effects = FileImportEffect(e.AllowedEffects);
			e.Handled = true;
			return;
		}
		if (appItem != null && (appItem.IsFolder || appItem.IsWidget))
		{
			ClearDropTargetHighlight();
			UpdateExternalDragGhostHint(null);
			if (appItem.IsFolder)
			{
				ScheduleFolderHoverOpen(appItem);
			}
			ExternalDragHint.Text = "拖到空白处添加到启动台";
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			return;
		}
		CancelFolderHoverTimer();
		if (frameworkElement != null && appItem != null && !appItem.IsFolder)
		{
			try
			{
				GeneralTransform generalTransform = frameworkElement.TransformToAncestor(this);
				_currentDropCardBounds = generalTransform.TransformBounds(new Rect(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
			}
			catch
			{
				_currentDropCardBounds = Rect.Empty;
			}
			SetDropTargetHighlight(frameworkElement, appItem);
			UpdateExternalDragGhostHint(appItem);
			e.Effects = ExternalTargetEffect(appItem, e.AllowedEffects);
			e.Handled = true;
		}
		else
		{
			ClearDropTargetHighlight();
			UpdateExternalDragGhostHint(null);
			e.Effects = FileImportEffect(e.AllowedEffects);
			e.Handled = true;
		}
	}

	internal static DragDropEffects FileImportEffect(DragDropEffects allowed)
	{
		if ((allowed & DragDropEffects.Link) == 0)
		{
			if ((allowed & DragDropEffects.Copy) == 0)
			{
				return DragDropEffects.None;
			}
			return DragDropEffects.Copy;
		}
		return DragDropEffects.Link;
	}

	internal static DragDropEffects FolderTransferEffect(DragDropEffects allowed, bool copyRequested)
	{
		if (!copyRequested || (allowed & DragDropEffects.Copy) == 0)
		{
			if ((allowed & DragDropEffects.Move) == 0)
			{
				if ((allowed & DragDropEffects.Copy) == 0)
				{
					return DragDropEffects.None;
				}
				return DragDropEffects.Copy;
			}
			return DragDropEffects.Move;
		}
		return DragDropEffects.Copy;
	}

	private static DragDropEffects ExternalTargetEffect(AppItem target, DragDropEffects allowed)
	{
		if (!Directory.Exists(target.ExecutablePath))
		{
			return DragDropEffects.Link;
		}
		return FolderTransferEffect(allowed, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
	}

	private static (FrameworkElement? cardElement, AppItem? app) FindAppDropTarget(DependencyObject? hit)
	{
		FrameworkElement item = null;
		AppItem item2 = null;
		int pageNumber = 0;
		while (hit != null && !(hit is Window) && pageNumber++ < 16)
		{
			if (hit is FrameworkElement { DataContext: AppItem dataContext } frameworkElement)
			{
				item = frameworkElement;
				item2 = dataContext;
				if (frameworkElement is Button)
				{
					break;
				}
			}
			hit = VisualTreeHelper.GetParent(hit);
		}
		return (cardElement: item, app: item2);
	}

	private void SetDropTargetHighlight(FrameworkElement cardElement, AppItem app)
	{
		if (_currentDropCardElement != cardElement)
		{
			ClearDropTargetHighlight();
			_currentDropCardElement = cardElement;
			_currentDropApp = app;
			ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
			cardElement.RenderTransformOrigin = new Point(0.5, 0.5);
			cardElement.RenderTransform = scaleTransform;
			_dropHighlightScale = scaleTransform;
			_highlightedElement = cardElement;
			DoubleAnimation animation = new DoubleAnimation(1.0, 1.08, TimeSpan.FromMilliseconds(120.0))
			{
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			if (app.IsDock)
			{
				SetDockTipApp(app);
			}
		}
	}

	private void ClearDropTargetHighlight()
	{
		if (_highlightedElement != null && _dropHighlightScale != null)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(_dropHighlightScale.ScaleX, 1.0, TimeSpan.FromMilliseconds(100.0))
			{
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			FrameworkElement elemToClear = _highlightedElement;
			ScaleTransform? dropHighlightScale = _dropHighlightScale;
			doubleAnimation.Completed += (object? s, EventArgs e) =>
			{
				if (_highlightedElement == null)
				{
					elemToClear.RenderTransform = null;
				}
			};
			dropHighlightScale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation);
			dropHighlightScale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation);
		}
		_highlightedElement = null;
		_dropHighlightScale = null;
		_currentDropCardElement = null;
		_currentDropApp = null;
		_currentDropCardBounds = Rect.Empty;
		CancelFolderHoverTimer();
		if (_dockTipApp != null && !_isPointerDown)
		{
			SetDockTipApp(null);
		}
	}

	private void ScheduleFolderHoverOpen(AppItem folder)
	{
		if (_hoveredFolder == folder)
		{
			return;
		}
		CancelFolderHoverTimer();
		_hoveredFolder = folder;
		_folderHoverTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_folderHoverTimer.Tick += (object? s, EventArgs e) =>
		{
			CancelFolderHoverTimer();
			if (_hoveredFolder != null && _hoveredFolder.IsFolder && !_viewModel.IsFolderOpen)
			{
				_viewModel.OpenFolder(_hoveredFolder);
			}
		};
		_folderHoverTimer.Start();
	}

	private void CancelFolderHoverTimer()
	{
		if (_folderHoverTimer != null)
		{
			_folderHoverTimer.Stop();
			_folderHoverTimer = null;
		}
		_hoveredFolder = null;
	}

	private void ShowExternalDragGhost(string[] files, Point initialPos)
	{
		if (files != null && files.Length != 0 && ExternalDragGhost != null)
		{
			string text = files[0];
			string commandLinePath = Path.GetFileName(text);
			if (string.IsNullOrEmpty(commandLinePath) && Directory.Exists(text))
			{
				commandLinePath = new DirectoryInfo(text).Name;
			}
			if (string.IsNullOrEmpty(commandLinePath))
			{
				commandLinePath = text;
			}
			ExternalDragFileName.Text = commandLinePath;
			ImageSource source = null;
			try
			{
				source = ShellIcons.GetFileIcon(text);
			}
			catch
			{
			}
			ExternalDragIcon.Source = source;
			if (files.Length > 1)
			{
				ExternalDragBadgeText.Text = $"+{files.Length - 1}";
				ExternalDragBadge.Visibility = Visibility.Visible;
			}
			else
			{
				ExternalDragBadge.Visibility = Visibility.Collapsed;
			}
			ExternalDragHint.Text = "松开添加到启动台";
			try
			{
				ExternalDragHint.Foreground = (Brush)FindResource("T.Fg_80");
			}
			catch
			{
			}
			UpdateExternalDragGhostPosition(initialPos);
			ExternalDragGhost.Visibility = Visibility.Visible;
			ExternalDragGhost.BeginAnimation(UIElement.OpacityProperty, null);
			ExternalDragGhost.Opacity = 1.0;
		}
	}

	private void UpdateExternalDragGhostPosition(Point mousePos)
	{
		if (ExternalDragGhost != null)
		{
			double pageNumber = ((ExternalDragGhost.ActualWidth > 0.0) ? ExternalDragGhost.ActualWidth : 180.0);
			double settingIndex = ((ExternalDragGhost.ActualHeight > 0.0) ? ExternalDragGhost.ActualHeight : 48.0);
			double num3 = mousePos.X + 16.0;
			double num4 = mousePos.Y + 16.0;
			if (ActualWidth > 0.0 && num3 + pageNumber > ActualWidth - 12.0)
			{
				num3 = mousePos.X - pageNumber - 12.0;
			}
			if (ActualHeight > 0.0 && num4 + settingIndex > ActualHeight - 12.0)
			{
				num4 = mousePos.Y - settingIndex - 12.0;
			}
			Canvas.SetLeft(ExternalDragGhost, Math.Max(8.0, num3));
			Canvas.SetTop(ExternalDragGhost, Math.Max(8.0, num4));
		}
	}

	private void UpdateExternalDragGhostHint(AppItem? targetApp)
	{
		if (ExternalDragHint == null)
		{
			return;
		}
		if (targetApp != null && !targetApp.IsFolder)
		{
			ExternalDragHint.Text = (Directory.Exists(targetApp.ExecutablePath) ? ((((Keyboard.Modifiers & ModifierKeys.Control) != 0) ? "复制到" : "移入") + " " + targetApp.DisplayName + "（Ctrl：复制）") : ("用 " + targetApp.DisplayName + " 打开"));
			try
			{
				ExternalDragHint.Foreground = (Brush)FindResource("T.AccText_FF");
				return;
			}
			catch
			{
				return;
			}
		}
		ExternalDragHint.Text = "松开添加到启动台";
		try
		{
			ExternalDragHint.Foreground = (Brush)FindResource("T.Fg_80");
		}
		catch
		{
		}
	}

	private void HideExternalDragGhost()
	{
		if (ExternalDragGhost == null || ExternalDragGhost.Visibility == Visibility.Collapsed)
		{
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation(ExternalDragGhost.Opacity, 0.0, TimeSpan.FromMilliseconds(120.0))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		doubleAnimation.Completed += (object? s, EventArgs e) =>
		{
			if (ExternalDragGhost.Opacity <= 0.01)
			{
				ExternalDragGhost.Visibility = Visibility.Collapsed;
			}
		};
		ExternalDragGhost.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	protected override void OnClosed(EventArgs e)
	{
		_windowClosed = true;
		ReleaseHiddenRenderMotion();
		_warmTimer?.Stop();
		_idleSettleTimer?.Stop();
		_searchIdleTimer?.Stop();
		CancelDrivenMotion();
		UnhookFrameRendering();
		EndFrameProbe();
		_viewModel.RequestPageStep -= AnimatePageStep;
		_viewModel.RequestPageTransition -= AnimatePageTransition;
		CancelFolderHoverTimer();
		_viewModel.RequestOpen -= AnimateShow;
		_viewModel.RequestClose -= OnLaunchpadCloseRequested;
		_viewModel.RequestCloseSettings -= OnCloseSettingsRequested;
		_viewModel.RequestOpenSettings -= ShowSettingsPanel;
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		_settingsVm.PropertyChanged -= OnSettingsVmPropertyChanged;
		if (_liveSettingsSource != null)
		{
			_liveSettingsSource.PropertyChanged -= OnLiveSettingChanged;
		}
		_liveSettingsSource = null;
		_settingsVm.SetDeactivationBlock = null;
		_viewModel.RequestBoundaryNudge -= AnimateBoundaryNudge;
		_viewModel.PageContentsReplaced -= OnPageContentsReplaced;
		_dockSizeTimer?.Stop();
		_viewModel.DockSizeChangeRequested -= AnimateDockSize;
		_viewModel.DockContentsReplaced -= OnDockContentsReplaced;
		CompositionTarget.Rendering -= OnDockRenderTick;
		CompositionTarget.Rendering -= OnFrostTrackTick;
		CompositionTarget.Rendering -= OnDockFlyTick;
		CompositionTarget.Rendering -= OnDockPileProbe;
		base.OnClosed(e);
	}

	private void ReleaseHiddenRenderMotion()
	{
		DockAtRest();
		SetDockTipApp(null);
		_frostTrackHooked = false;
		CompositionTarget.Rendering -= OnFrostTrackTick;
		if (_dockFly != null)
		{
			DockFlight? dockFly = _dockFly;
			_dockFly = null;
			_dragLandingToken++;
			StopDockFlyFrames();
			dockFly.Done.TrySetResult(default);
		}
	}

	private void OnLaunchpadCloseRequested()
	{
		AnimateHide(_isAppLaunching);
	}

	private void OnCloseSettingsRequested()
	{
		CloseSettingsPanel("request");
	}
}
