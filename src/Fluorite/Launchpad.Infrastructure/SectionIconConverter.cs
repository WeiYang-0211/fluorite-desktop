using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Launchpad.Infrastructure;

public sealed class SectionIconConverter : IValueConverter
{
	private static readonly Dictionary<string, Geometry> Icons = new Dictionary<string, Geometry>
	{
		["通用"] = Parse("M4,7 L8,7 M12,7 L20,7 M4,17 L14,17 M18,17 L20,17 M10,4 L10,10 M16,14 L16,20"),
		["程序坞"] = Parse("M4,17 L20,17 M5,21 L19,21 M5,5 L10,5 L10,13 L5,13 Z M14,5 L19,5 L19,13 L14,13 Z"),
		["外观与图标"] = Parse("M12,3 C7,3 3,7 3,12 C3,17 7,21 12,21 L14,21 C16,21 16,18 14,17 C12,16 14,14 16,14 L18,14 C22,14 22,10 20,7 C18,4 15,3 12,3 Z M7,9 L7.1,9 M11,6 L11.1,6 M16,7 L16.1,7 M6,14 L6.1,14"),
		["布局与网格"] = Parse("M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z"),
		["小组件"] = Parse("M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 Z M12,7 L12,12 L16,14"),
		["性能监控"] = Parse("M3,4 L21,4 L21,17 L3,17 Z M8,21 L16,21 M12,17 L12,21 M5,11 L8,11 L10,7 L13,14 L15,10 L19,10"),
		["动效与手势"] = Parse("M5,3 L5,19 L9,15 L12,21 L15,19 L12,13 L18,13 Z"),
		["应用管理"] = Parse("M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M17,14 L17,20 M14,17 L20,17"),
		["专业版"] = Parse("M3,9 L7,4 L17,4 L21,9 L12,21 Z M3,9 L21,9 M7,4 L9,9 L12,21 M17,4 L15,9 L12,21"),
		["账户"] = Parse("M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 Z M12,7 A3,3 0 1 1 12,13 A3,3 0 1 1 12,7 Z M6,18 C8,14 16,14 18,18")
	};

	private static Geometry Parse(string data)
	{
		Geometry geometry = Geometry.Parse(data);
		geometry.Freeze();
		return geometry;
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = value as string;
		bool validated = text != null;
		Geometry failure = default;
		if (validated)
		{
			validated = Icons.TryGetValue(text switch
			{
				"外观" => "外观与图标", 
				"时钟" => "小组件", 
				"应用" => "应用管理", 
				_ => text, 
			}, out failure);
		}
		if (!validated)
		{
			return Geometry.Empty;
		}
		return failure;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
