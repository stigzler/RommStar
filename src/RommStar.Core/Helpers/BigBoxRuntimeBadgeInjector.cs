using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Helpers
{
    public static class BigBoxRuntimeBadgeInjector
    {
        private static bool _isInjected = false;

        public static void TryInjectBadge()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                var targetStackPanel = FindBadgeStackPanel(mainWindow);
                if (targetStackPanel == null) return;

                // Prevent duplicate injection
                if (targetStackPanel.FindName("RommStarSyncedDynamicBorder") != null) return;

                var flowBadgeType = Type.GetType("Unbroken.LaunchBox.Windows.Controls.CoverFlow.FlowBadge, Unbroken.LaunchBox.Windows");
                if (flowBadgeType == null) return;

                var flowBadge = Activator.CreateInstance(flowBadgeType) as FrameworkElement;
                if (flowBadge == null) return;

                flowBadge.Name = "RommStarSynced";
                flowBadgeType.GetProperty("BadgeName")?.SetValue(flowBadge, "rommStarSynced");

                flowBadge.SetBinding(FrameworkElement.DataContextProperty, new Binding("SelectedGame")
                {
                    Source = targetStackPanel.DataContext
                });

                var style = CreateBadgeVisibilityStyle(flowBadgeType);
                flowBadge.Style = style;

                var wrapperBorder = new Border
                {
                    Name = "RommStarSyncedDynamicBorder",
                    Background = new SolidColorBrush(Color.FromArgb(64, 19, 19, 19)),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 20, 0),
                    Child = flowBadge
                };

                // Insert at the beginning or end of the stack
                targetStackPanel.Children.Add(wrapperBorder);

                // Force parent layout propagation
                if (targetStackPanel.Parent is FrameworkElement parentElement)
                {
                    parentElement.InvalidateMeasure();
                    parentElement.InvalidateVisual();
                }

                targetStackPanel.InvalidateMeasure();
                targetStackPanel.InvalidateArrange();
                targetStackPanel.UpdateLayout();
            });
        }

        private static StackPanel FindBadgeStackPanel(DependencyObject parent)
        {
            // Traverse visual tree to find the StackPanel housing the native feature badges
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is StackPanel sp)
                {
                    // Check if this stack panel contains elements related to badges
                    foreach (var inner in sp.Children)
                    {
                        if (inner.GetType().Name.Contains("FlowBadge"))
                        {
                            return sp;
                        }
                    }
                }

                var found = FindBadgeStackPanel(child);
                if (found != null) return found;
            }
            return null;
        }

        private static Style CreateBadgeVisibilityStyle(Type targetType)
        {
            var style = new Style(targetType);
            style.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible));

            // Add DataTrigger for HasBadge == False
            var trigger = new DataTrigger
            {
                Binding = new Binding("HasBadge") { RelativeSource = RelativeSource.Self },
                Value = false
            };
            trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            style.Triggers.Add(trigger);

            return style;
        }
    }
}