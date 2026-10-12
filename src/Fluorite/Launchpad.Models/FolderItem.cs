using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launchpad.Models;

public class FolderItem : ObservableObject
{
	[ObservableProperty]
	private string _id = Guid.NewGuid().ToString();

	[ObservableProperty]
	private string _name = "New Folder";

	[ObservableProperty]
	private int _sortIndex;

	[ObservableProperty]
	private List<string> _appIds = new List<string>();

	[ObservableProperty]
	private ObservableCollection<AppItem> _apps = new ObservableCollection<AppItem>();

	[JsonIgnore]
	public IEnumerable<AppItem> PreviewApps => Apps;

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
				_name = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Name);
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

	public List<string> AppIds
	{
		get
		{
			return _appIds;
		}
		[MemberNotNull("_appIds")]
		set
		{
			if (!EqualityComparer<List<string>>.Default.Equals(_appIds, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AppIds);
				_appIds = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AppIds);
			}
		}
	}

	[JsonIgnore]
	public ObservableCollection<AppItem> Apps
	{
		get
		{
			return _apps;
		}
		[MemberNotNull("_apps")]
		set
		{
			if (!EqualityComparer<ObservableCollection<AppItem>>.Default.Equals(_apps, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Apps);
				_apps = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Apps);
			}
		}
	}
}
