using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GGs.Desktop.Services;

namespace GGs.Desktop.Views.Controls
{
    public class ActivityFeed : Control
    {
        private ItemsControl? _itemsControl;
        private ObservableCollection<ActivityItem> _items = new();

        static ActivityFeed()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ActivityFeed), new FrameworkPropertyMetadata(typeof(ActivityFeed)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _itemsControl = GetTemplateChild("PART_Items") as ItemsControl;
            if (_itemsControl != null)
            {
                _itemsControl.ItemsSource = _items;
            }
        }

        public void LoadActivities(IReadOnlyList<ActivityItem> activities)
        {
            _items.Clear();
            foreach (var a in activities)
                _items.Add(a);
        }
    }
}