using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Launchpad.Models;
using Launchpad.ViewModels;

namespace Launchpad.Views;

public static class SceneListDrag
{
	private sealed class ScenePreview : Adorner
	{
		private readonly ImageBrush _snapshot;

		private readonly double _height;

		private readonly double _grab;

		public double PointerY { get; set; }

		public double InsertY { get; set; } = -1.0;

		public ScenePreview(ListBox list, ListBoxItem row, double grab)
			: base(list)
		{
			IsHitTestVisible = false;
			_height = row.ActualHeight;
			_grab = grab;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(Math.Max(1, (int)row.ActualWidth), Math.Max(1, (int)row.ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(row);
			renderTargetBitmap.Freeze();
			_snapshot = new ImageBrush(renderTargetBitmap);
			_snapshot.Freeze();
		}

		protected override void OnRender(DrawingContext dc)
		{
			Brush brush = (((FrameworkElement)AdornedElement).TryFindResource("AccentBrush") as Brush) ?? Brushes.DodgerBlue;
			if (InsertY >= 0.0)
			{
				dc.DrawRoundedRectangle(brush, null, new Rect(4.0, InsertY - 1.0, Math.Max(1.0, AdornedElement.RenderSize.Width - 8.0), 3.0), 1.5, 1.5);
			}
			double y = Math.Clamp(PointerY - _grab, 0.0, Math.Max(0.0, AdornedElement.RenderSize.Height - _height));
			Rect rectangle = new Rect(3.0, y, Math.Max(1.0, AdornedElement.RenderSize.Width - 6.0), _height);
			dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)), null, new Rect(rectangle.X + 2.0, rectangle.Y + 4.0, rectangle.Width, rectangle.Height), 9.0, 9.0);
			dc.PushOpacity(0.97);
			dc.DrawRoundedRectangle(_snapshot, new Pen(brush, 1.5), rectangle, 8.0, 8.0);
			dc.Pop();
		}
	}

	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SceneListDrag), new PropertyMetadata(false, Changed));

	public static void SetEnabled(DependencyObject d, bool value)
	{
		d.SetValue(EnabledProperty, value);
	}

	public static bool GetEnabled(DependencyObject d)
	{
		return (bool)d.GetValue(EnabledProperty);
	}

	private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ListBox list = d as ListBox;
		if (list == null || !(bool)e.NewValue)
		{
			return;
		}
		Point start = default;
		WorkspaceEntry source = null;
		bool dragging = false;
		bool releasing = false;
		ListBoxItem sourceRow = null;
		ScenePreview preview = null;
		AdornerLayer layer = null;
		Dictionary<string, double> positions = new Dictionary<string, double>();
		WorkspaceEntry targetScene = null;
		list.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)((object _, MouseButtonEventArgs args) =>
		{
			source = null;
			DependencyObject dependencyObject = args.OriginalSource as DependencyObject;
			while (dependencyObject != null && dependencyObject != list && ((!(dependencyObject is ButtonBase) && !(dependencyObject is TextBoxBase)) || 1 == 0))
			{
				if (dependencyObject is ListBoxItem listBoxItem)
				{
					if (list.DataContext is SettingsViewModel { WorkspaceBusy: false })
					{
						source = listBoxItem.DataContext as WorkspaceEntry;
						sourceRow = listBoxItem;
						start = args.GetPosition(list);
						if (source != null)
						{
							list.CaptureMouse();
							args.Handled = true;
						}
					}
					break;
				}
				dependencyObject = Parent(dependencyObject);
			}
		}), handledEventsToo: true);
		list.AddHandler(UIElement.PreviewMouseMoveEvent, (MouseEventHandler)((object _, MouseEventArgs args) =>
		{
			if (!(source == null))
			{
				if (args.LeftButton != MouseButtonState.Pressed)
				{
					Release();
				}
				else if (dragging || !((args.GetPosition(list) - start).Length < 6.0))
				{
					if (!dragging)
					{
						positions.Clear();
						foreach (WorkspaceEntry item in (IEnumerable)list.Items)
						{
							if (list.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
							{
								positions[item.Id] = listBoxItem.TranslatePoint(default, list).Y;
							}
						}
						if (sourceRow != null)
						{
							preview = new ScenePreview(list, sourceRow, start.Y - positions[source.Id]);
							layer = AdornerLayer.GetAdornerLayer(list);
							layer?.Add(preview);
							sourceRow.Opacity = 0.25;
						}
					}
					dragging = true;
					list.Cursor = Cursors.SizeNS;
					Point point = args.GetPosition(list);
					targetScene = ((point.Y < 0.0 || point.Y > list.ActualHeight) ? null : (from WorkspaceEntry workspaceEntry2 in list.Items
						where positions.ContainsKey(workspaceEntry2.Id)
						orderby Math.Abs(positions[workspaceEntry2.Id] + (sourceRow?.ActualHeight ?? 40.0) / 2.0 - point.Y)
						select workspaceEntry2).FirstOrDefault());
					int pageNumber = list.Items.IndexOf(source);
					int settingIndex = ((targetScene == null) ? pageNumber : list.Items.IndexOf(targetScene));
					double targetPage = (sourceRow?.ActualHeight ?? 40.0) + (sourceRow?.Margin.Bottom ?? 0.0);
					for (int num4 = 0; num4 < list.Items.Count; num4++)
					{
						if (list.ItemContainerGenerator.ContainerFromIndex(num4) is ListBoxItem listBoxItem2)
						{
							ListBoxItem row = listBoxItem2;
							double to = ((num4 > pageNumber && num4 <= settingIndex) ? (0.0 - targetPage) : ((num4 >= settingIndex && num4 < pageNumber) ? targetPage : 0.0));
							Shift(row, to);
						}
					}
					if (preview != null)
					{
						preview.PointerY = point.Y;
						preview.InsertY = ((targetScene == null) ? (-1.0) : (positions[targetScene.Id] + ((settingIndex > pageNumber) ? targetPage : 0.0)));
						preview.InvalidateVisual();
					}
					args.Handled = true;
				}
			}
		}), handledEventsToo: true);
		list.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)(async (object _, MouseButtonEventArgs args) =>
		{
			if (source == null)
			{
				return;
			}
			WorkspaceEntry source2 = source;
			WorkspaceEntry workspaceEntry = targetScene;
			bool commit = dragging;
			Dictionary<string, double> oldPositions = new Dictionary<string, double>(positions);
			releasing = true;
			if (list.IsMouseCaptured)
			{
				list.ReleaseMouseCapture();
			}
			releasing = false;
			source = null;
			dragging = false;
			args.Handled = true;
			try
			{
				if (commit && workspaceEntry != null && list.DataContext is SettingsViewModel settingsViewModel)
				{
					await settingsViewModel.MoveSceneAsync(source2, workspaceEntry);
				}
			}
			finally
			{
				Cleanup();
				list.Cursor = Cursors.Arrow;
				list.UpdateLayout();
				if (commit && !Reduced())
				{
					foreach (WorkspaceEntry item2 in (IEnumerable)list.Items)
					{
						if (oldPositions.TryGetValue(item2.Id, out var value) && list.ItemContainerGenerator.ContainerFromItem(item2) is ListBoxItem listBoxItem)
						{
							double y = listBoxItem.TranslatePoint(default, list).Y;
							Shift(listBoxItem, value - y, immediate: true);
							Shift(listBoxItem, 0.0);
						}
					}
				}
			}
		}), handledEventsToo: true);
		list.LostMouseCapture += (object _, MouseEventArgs _) =>
		{
			if (!releasing)
			{
				Release();
			}
		};
		list.Unloaded += (object _, RoutedEventArgs _) =>
		{
			Release();
		};
		void Cleanup()
		{
			if (sourceRow != null)
			{
				sourceRow.Opacity = 1.0;
			}
			foreach (object item3 in (IEnumerable)list.Items)
			{
				if (list.ItemContainerGenerator.ContainerFromItem(item3) is ListBoxItem row)
				{
					Shift(row, 0.0, immediate: true);
				}
			}
			if (preview != null)
			{
				layer?.Remove(preview);
			}
			preview = null;
			sourceRow = null;
			targetScene = null;
		}
		bool Reduced()
		{
			if (list.DataContext is SettingsViewModel settingsViewModel)
			{
				return settingsViewModel.Settings.ReduceMotion;
			}
			return false;
		}
		void Release()
		{
			Cleanup();
			source = null;
			dragging = false;
			if (list.IsMouseCaptured)
			{
				list.ReleaseMouseCapture();
			}
			list.Cursor = Cursors.Arrow;
		}
		void Shift(ListBoxItem row, double to, bool immediate = false)
		{
			TranslateTransform translateTransform = row.RenderTransform as TranslateTransform;
			if (translateTransform == null)
			{
				translateTransform = (TranslateTransform)(row.RenderTransform = new TranslateTransform());
			}
			if (immediate || Reduced())
			{
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				translateTransform.Y = to;
			}
			else
			{
				translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				});
			}
		}
	}

	private static DependencyObject? Parent(DependencyObject d)
	{
		if (!(d is Visual))
		{
			return LogicalTreeHelper.GetParent(d);
		}
		return VisualTreeHelper.GetParent(d);
	}
}
