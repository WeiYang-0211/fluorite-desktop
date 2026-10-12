using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launchpad.Models;

public class AppItem : ObservableObject
{
	[ObservableProperty]
	private string _id = Guid.NewGuid().ToString();

	[ObservableProperty]
	[NotifyPropertyChangedFor("DisplayName")]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _executablePath = string.Empty;

	[ObservableProperty]
	private string? _arguments;

	[ObservableProperty]
	private string? _iconPath;

	[ObservableProperty]
	private int _iconIndex;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasIconOverride")]
	private string? _iconOverridePath;

	[ObservableProperty]
	private bool _startsOpenWindowSection;

	[ObservableProperty]
	[NotifyPropertyChangedFor("DisplayName")]
	[NotifyPropertyChangedFor("HasCustomName")]
	private string? _customName;

	[ObservableProperty]
	private string _source = "StartMenu";

	[ObservableProperty]
	private string _category = "General";

	[ObservableProperty]
	private bool _systemToolGroupingHandled;

	[ObservableProperty]
	private bool _isDock;

	[ObservableProperty]
	private int _dockIndex;

	[ObservableProperty]
	private bool _isRunning;

	[ObservableProperty]
	private double _dockLeft;

	[ObservableProperty]
	private int _sortIndex;

	[ObservableProperty]
	private DateTime _lastUsedTime = DateTime.MinValue;

	[ObservableProperty]
	private bool _isFolder;

	[ObservableProperty]
	private bool _isWidget;

	[ObservableProperty]
	private string? _widgetType;

	[ObservableProperty]
	[NotifyPropertyChangedFor("IsInFolder")]
	private string? _folderId;

	[ObservableProperty]
	private bool _isHidden;

	[ObservableProperty]
	private ObservableCollection<AppItem> _children = new ObservableCollection<AppItem>();

	[ObservableProperty]
	private ImageSource? _icon;

	[JsonIgnore]
	public bool HasIconOverride => !string.IsNullOrEmpty(IconOverridePath);

	[JsonIgnore]
	public bool IsOpenWindowEntry { get; set; }

	[JsonIgnore]
	public string? OpenWindowApplicationId { get; set; }

	[JsonIgnore]
	public bool HasCustomName => !string.IsNullOrEmpty(CustomName);

	[JsonIgnore]
	public bool IsInFolder => !string.IsNullOrEmpty(FolderId);

	[JsonIgnore]
	public string DisplayName
	{
		get
		{
			if (string.IsNullOrWhiteSpace(CustomName))
			{
				if (!string.IsNullOrWhiteSpace(Name))
				{
					return Name;
				}
				return "Unknown";
			}
			return CustomName;
		}
	}

	[JsonIgnore]
	public IEnumerable<AppItem> PreviewChildren => Children.Take(PreviewGridSize * PreviewGridSize);

	[JsonIgnore]
	public int PreviewGridSize
	{
		get
		{
			if (Children.Count > 4)
			{
				return 3;
			}
			return 2;
		}
	}

	[JsonIgnore]
	public string? SearchLowerName { get; set; }

	[JsonIgnore]
	public string? SearchPinyinInitials { get; set; }

	[JsonIgnore]
	public string[]? SearchWords { get; set; }

	[JsonIgnore]
	public string? SearchAcronym { get; set; }

	[JsonIgnore]
	public string? SearchExeName { get; set; }

	[JsonIgnore]
	public string? SearchLowerCustomName { get; set; }

	[JsonIgnore]
	public string? SearchPinyinCustomInitials { get; set; }

	[JsonIgnore]
	public string[]? SearchCustomWords { get; set; }

	[JsonIgnore]
	public string? SearchCustomAcronym { get; set; }

	public string Id
	{
		get
		{
			return _id;
		}
		[MemberNotNull("_id")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_id, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Id);
				_id = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Id);
			}
		}
	}

	public string Name
	{
		get
		{
			return _name;
		}
		[MemberNotNull("_name")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_name, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Name);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DisplayName);
				_name = value;
				OnNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Name);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DisplayName);
			}
		}
	}

	public string ExecutablePath
	{
		get
		{
			return _executablePath;
		}
		[MemberNotNull("_executablePath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_executablePath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ExecutablePath);
				_executablePath = value;
				OnExecutablePathChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ExecutablePath);
			}
		}
	}

	public string? Arguments
	{
		get
		{
			return _arguments;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_arguments, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Arguments);
				_arguments = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Arguments);
			}
		}
	}

	public string? IconPath
	{
		get
		{
			return _iconPath;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_iconPath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconPath);
				_iconPath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconPath);
			}
		}
	}

	public int IconIndex
	{
		get
		{
			return _iconIndex;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_iconIndex, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconIndex);
				_iconIndex = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconIndex);
			}
		}
	}

	public string? IconOverridePath
	{
		get
		{
			return _iconOverridePath;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_iconOverridePath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconOverridePath);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasIconOverride);
				_iconOverridePath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconOverridePath);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasIconOverride);
			}
		}
	}

	[JsonIgnore]
	public bool StartsOpenWindowSection
	{
		get
		{
			return _startsOpenWindowSection;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_startsOpenWindowSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StartsOpenWindowSection);
				_startsOpenWindowSection = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StartsOpenWindowSection);
			}
		}
	}

	public string? CustomName
	{
		get
		{
			return _customName;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_customName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CustomName);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DisplayName);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasCustomName);
				_customName = value;
				OnCustomNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CustomName);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DisplayName);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasCustomName);
			}
		}
	}

	public string Source
	{
		get
		{
			return _source;
		}
		[MemberNotNull("_source")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_source, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Source);
				_source = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Source);
			}
		}
	}

	public string Category
	{
		get
		{
			return _category;
		}
		[MemberNotNull("_category")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_category, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Category);
				_category = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Category);
			}
		}
	}

	public bool SystemToolGroupingHandled
	{
		get
		{
			return _systemToolGroupingHandled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_systemToolGroupingHandled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SystemToolGroupingHandled);
				_systemToolGroupingHandled = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SystemToolGroupingHandled);
			}
		}
	}

	public bool IsDock
	{
		get
		{
			return _isDock;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isDock, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDock);
				_isDock = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDock);
			}
		}
	}

	public int DockIndex
	{
		get
		{
			return _dockIndex;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_dockIndex, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockIndex);
				_dockIndex = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockIndex);
			}
		}
	}

	[JsonIgnore]
	public bool IsRunning
	{
		get
		{
			return _isRunning;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isRunning, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRunning);
				_isRunning = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRunning);
			}
		}
	}

	[JsonIgnore]
	public double DockLeft
	{
		get
		{
			return _dockLeft;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_dockLeft, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DockLeft);
				_dockLeft = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DockLeft);
			}
		}
	}

	public int SortIndex
	{
		get
		{
			return _sortIndex;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_sortIndex, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SortIndex);
				_sortIndex = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SortIndex);
			}
		}
	}

	public DateTime LastUsedTime
	{
		get
		{
			return _lastUsedTime;
		}
		set
		{
			if (!EqualityComparer<DateTime>.Default.Equals(_lastUsedTime, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LastUsedTime);
				_lastUsedTime = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LastUsedTime);
			}
		}
	}

	public bool IsFolder
	{
		get
		{
			return _isFolder;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isFolder, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsFolder);
				_isFolder = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsFolder);
			}
		}
	}

	public bool IsWidget
	{
		get
		{
			return _isWidget;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isWidget, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsWidget);
				_isWidget = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsWidget);
			}
		}
	}

	public string? WidgetType
	{
		get
		{
			return _widgetType;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_widgetType, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.WidgetType);
				_widgetType = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.WidgetType);
			}
		}
	}

	public string? FolderId
	{
		get
		{
			return _folderId;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_folderId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FolderId);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsInFolder);
				_folderId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FolderId);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsInFolder);
			}
		}
	}

	public bool IsHidden
	{
		get
		{
			return _isHidden;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isHidden, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsHidden);
				_isHidden = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsHidden);
			}
		}
	}

	public ObservableCollection<AppItem> Children
	{
		get
		{
			return _children;
		}
		[MemberNotNull("_children")]
		set
		{
			if (!EqualityComparer<ObservableCollection<AppItem>>.Default.Equals(_children, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Children);
				_children = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Children);
			}
		}
	}

	[JsonIgnore]
	public ImageSource? Icon
	{
		get
		{
			return _icon;
		}
		set
		{
			if (!EqualityComparer<ImageSource>.Default.Equals(_icon, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Icon);
				_icon = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Icon);
			}
		}
	}

	public void InvalidateSearchCache()
	{
		SearchLowerName = null;
		SearchPinyinInitials = null;
		SearchWords = null;
		SearchAcronym = null;
		SearchExeName = null;
		SearchLowerCustomName = null;
		SearchPinyinCustomInitials = null;
		SearchCustomWords = null;
		SearchCustomAcronym = null;
	}

	private void OnNameChanged(string value)
	{
		InvalidateSearchCache();
	}

	private void OnExecutablePathChanged(string value)
	{
		InvalidateSearchCache();
	}

	private void OnCustomNameChanged(string? value)
	{
		InvalidateSearchCache();
	}
}
