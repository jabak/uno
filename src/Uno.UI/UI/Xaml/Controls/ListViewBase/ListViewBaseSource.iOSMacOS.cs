﻿using System;
using System.Collections.Generic;
using System.Text;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using Uno.Extensions;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
#if __IOS__
using UIKit;
#else
using AppKit;
#endif

namespace Windows.UI.Xaml.Controls
{
	[Bindable]
	public partial class ListViewBaseSource
	{
		/// <summary>
		/// Key used to represent a null DataTemplate in _templateCache and _templateCells dictionaries (because null is a not a valid key) 
		/// </summary>
		private readonly static DataTemplate _nullDataTemplateKey = new DataTemplate(() => null);

		/// <summary>
		/// We include one extra section to ensure Header and Footer can display even when we have no sections (ie an empty grouped source).
		/// </summary>
		internal const int SupplementarySections = 1;

		private DataTemplateSelector _currentSelector;
		private Dictionary<DataTemplate, CGSize> _templateCache = new Dictionary<DataTemplate, CGSize>(DataTemplate.FrameworkTemplateEqualityComparer.Default);
		private Dictionary<DataTemplate, NSString> _templateCells = new Dictionary<DataTemplate, NSString>(DataTemplate.FrameworkTemplateEqualityComparer.Default);
		/// <summary>
		/// The furthest item in the source which has already been materialized. Items up to this point can safely be retrieved.
		/// </summary>
		private NSIndexPath _lastMaterializedItem = NSIndexPath.FromItemSection(0, 0);

		private WeakReference<NativeListViewBase> _owner;
		public NativeListViewBase Owner
		{
			get { return _owner?.GetTarget(); }
			set { _owner = new WeakReference<NativeListViewBase>(value); }
		}

		/// <summary>
		/// Visual element which stops layout requests from propagating up. Used for measuring templates.
		/// </summary>
		private BlockLayout BlockLayout { get; } = new BlockLayout();

		internal CGSize GetHeaderSize(Size availableSize)
		{
			return Owner.HeaderTemplate != null ? GetTemplateSize(Owner.HeaderTemplate, NativeListViewBase.ListViewHeaderElementKindNS, availableSize) : CGSize.Empty;
		}

		internal CGSize GetFooterSize(Size availableSize)
		{
			return Owner.FooterTemplate != null ? GetTemplateSize(Owner.FooterTemplate, NativeListViewBase.ListViewFooterElementKindNS, availableSize) : CGSize.Empty;
		}

		internal CGSize GetSectionHeaderSize(int section, Size availableSize)
		{
			var template = GetTemplateForGroupHeader(section);
			return template.SelectOrDefault(ht => GetTemplateSize(ht, NativeListViewBase.ListViewSectionHeaderElementKindNS, availableSize), CGSize.Empty);
		}


		internal CGSize GetItemSize(NativeListViewBase collectionView, NSIndexPath indexPath, Size availableSize)
		{
			DataTemplate itemTemplate = GetTemplateForItem(indexPath);

			if (_currentSelector != Owner.ItemTemplateSelector)
			{
				// If the templateSelector has changed, clear the cache
				_currentSelector = Owner.ItemTemplateSelector;
				_templateCache.Clear();
				_templateCells.Clear();
			}

			var size = GetTemplateSize(itemTemplate, NativeListViewBase.ListViewItemElementKindNS, availableSize);

			if (size == CGSize.Empty)
			{
				// The size of the template is usually empty for items that have not been displayed yet when using ItemTemplateSelector.
				// The reason why we can't measure the template is because we do not resolve it, 
				// as it would require enumerating through all items of a possibly virtualized ItemsSource.
				// To ensure a first display (after which they will be properly measured), we need them to have a non-empty size. 
				size = new CGSize(44, 44); // 44 is the default MinHeight/MinWidth of ListViewItem/GridViewItem on UWP.
			}

			return size;
		}

		private DataTemplate GetTemplateForItem(NSIndexPath indexPath)
		{
			if (IsMaterialized(indexPath))
			{
				return Owner?.XamlParent?.ResolveItemTemplate(Owner.XamlParent.GetDisplayItemFromIndexPath(indexPath.ToIndexPath()));
			}
			else
			{
				// Ignore ItemTemplateSelector since we do not know what the item is
				return Owner?.XamlParent?.ItemTemplate;
			}
		}

		private DataTemplate GetTemplateForGroupHeader(int section)
		{
			var groupStyle = Owner.GroupStyle;
			if (IsMaterialized(section))
			{
				return DataTemplateHelper.ResolveTemplate(
					groupStyle?.HeaderTemplate,
					groupStyle?.HeaderTemplateSelector,
					Owner.XamlParent.GetGroupAtDisplaySection(section).Group,
					Owner);
			}
			else
			{
				return groupStyle?.HeaderTemplate;
			}
		}

		/// <summary>
		/// Gets the actual item template size, using a non-databound materialized
		/// view of the template.
		/// </summary>
		/// <param name="dataTemplate">A data template</param>
		/// <returns>The actual size of the template</returns>
		private CGSize GetTemplateSize(DataTemplate dataTemplate, NSString elementKind, Size availableSize)
		{
			CGSize size;

			// Cache the sizes to avoid creating new templates every time.
			if (!_templateCache.TryGetValue(dataTemplate ?? _nullDataTemplateKey, out size))
			{
				var container = CreateContainerForElementKind(elementKind);

				// Force a null DataContext so the parent's value does not flow
				// through when temporarily adding the container to Owner.XamlParent
				container.SetValue(FrameworkElement.DataContextProperty, null);

				Style style = null;
				if (elementKind == NativeListViewBase.ListViewItemElementKind)
				{
					style = Owner.ItemContainerStyle;
				}
				else if (elementKind == NativeListViewBase.ListViewSectionHeaderElementKind)
				{
					style = Owner.GroupStyle?.HeaderContainerStyle;
				}
				if (style != null)
				{
					container.Style = style;
				}

				container.ContentTemplate = dataTemplate;
				try
				{
					// Attach templated container to visual tree while measuring. This works around the bug that default Style is not 
					// applied until view is loaded.
					Owner.XamlParent.AddSubview(BlockLayout);
					BlockLayout.AddSubview(container);
					// Measure with PositiveInfinity rather than MaxValue, since some views handle this better.
					size = Owner.NativeLayout.Layouter.MeasureChild(container, availableSize);

					if ((size.Height > nfloat.MaxValue / 2 || size.Width > nfloat.MaxValue / 2) &&
						this.Log().IsEnabled(LogLevel.Warning)
					)
					{
						this.Log().LogWarning($"Infinite item size reported, this can crash native collection view.");
					}
				}
				finally
				{
					Owner.XamlParent.RemoveChild(BlockLayout);
					BlockLayout.RemoveChild(container);

					// Reset the DataContext for reuse.
					container.ClearValue(FrameworkElement.DataContextProperty);
				}

				_templateCache[dataTemplate ?? _nullDataTemplateKey] = size;
			}

			return size;
		}

		/// <summary>
		/// Get item container corresponding to an element kind (header, footer, list item, etc)
		/// </summary>
		private ContentControl CreateContainerForElementKind(NSString elementKind)
		{
			if (elementKind == NativeListViewBase.ListViewSectionHeaderElementKindNS)
			{
				return Owner?.XamlParent?.GetGroupHeaderContainer(null);
			}
			else if (elementKind == NativeListViewBase.ListViewItemElementKindNS)
			{
				return Owner?.XamlParent?.GetContainerForIndex(-1) as ContentControl;
			}
			else
			{
				//Used for header and footer
				return ContentControl.CreateItemContainer();
			}
		}

		internal void ReloadData()
		{
			_lastMaterializedItem = NSIndexPath.FromItemSection(0, 0);
		}

		/// <summary>
		/// Is item in the range of already-materialized items?
		/// </summary>
		private bool IsMaterialized(NSIndexPath itemPath) => itemPath.Compare(_lastMaterializedItem) <= 0;

		// Consider group header to be materialized if first item in group is materialized
		private bool IsMaterialized(int section) => section <= _lastMaterializedItem.Section;
	}
}