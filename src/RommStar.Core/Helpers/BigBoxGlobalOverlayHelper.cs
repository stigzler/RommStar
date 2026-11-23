using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Helpers
{
    public static class BigBoxGlobalOverlayHelper
    {
        private static Border _overlayBorder;
        private static TextBlock _overlayText;
        private static DispatcherTimer _hideTimer;
        private static bool _isInitialized = false;

        /// <summary>
        /// Injects a persistent overlay border directly into the root Grid of Big Box's MainWindow.
        /// Call this once when your plugin initializes.
        /// </summary>
        public static void InitializeGlobalOverlay()
        {
            if (_isInitialized) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow as Window;
                if (mainWindow == null) return;

                // Find the root panel of the main window (usually a Grid or FrameworkElement)
                var rootContent = mainWindow.Content as FrameworkElement;
                if (rootContent == null) return;

                // If the root is a Panel (like a Grid), we can inject our overlay directly into it
                Panel rootPanel = rootContent as Panel;
                if (rootPanel == null)
                {
                    // If rootContent isn't a panel, wrap it or find the inner grid
                    rootPanel = FindVisualChild<Panel>(rootContent);
                }

                if (rootPanel == null) return;

                // Create the persistent overlay elements once
                _overlayBorder = new Border
                {
                    Width = 450,
                    MaxWidth = 550,          // Keeps it from stretching too wide on massive screens
                    Background = new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)), // 80% Black
                    BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Mid-grey
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(20),      // Comfortable inner breathing room
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed, // Hidden by default
                    IsHitTestVisible = false,         // Never block clicks or navigation
                    Opacity = 0.95
                };

                _overlayText = new TextBlock
                {
                    Foreground = Brushes.White,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10)
                };

                _overlayBorder.Child = _overlayText;

                // If rootPanel is a Grid, make sure our overlay spans all rows/columns so it centers correctly
                if (rootPanel is Grid grid)
                {
                    Grid.SetRowSpan(_overlayBorder, Math.Max(1, grid.RowDefinitions.Count));
                    Grid.SetColumnSpan(_overlayBorder, Math.Max(1, grid.ColumnDefinitions.Count));
                }

                // Add it directly to the root tree
                rootPanel.Children.Add(_overlayBorder);

                // Setup the auto-hide timer
                _hideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _hideTimer.Tick += (s, args) =>
                {
                    _hideTimer.Stop();
                    if (_overlayBorder != null)
                    {
                        _overlayBorder.Visibility = Visibility.Collapsed;
                    }
                };

                _isInitialized = true;
            });
        }

        public static void ShowMessage(string message, int durationSeconds = 3)
        {
            if (!PluginHelper.StateManager.IsBigBox) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Ensure it's initialized if called early
                if (!_isInitialized)
                {
                    InitializeGlobalOverlay();
                }

                if (_overlayText == null || _overlayBorder == null) return;

                _hideTimer.Stop();
                _overlayText.Text = message;
                _overlayBorder.Visibility = Visibility.Visible;

                _hideTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
                _hideTimer.Start();
            });
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}