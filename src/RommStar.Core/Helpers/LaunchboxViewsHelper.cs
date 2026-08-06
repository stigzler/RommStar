using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Helpers
{
    internal static class LaunchboxViewsHelper
    {
        /// <summary>
        /// Forces a gentle visual refresh of the LaunchBox UI without resetting the user's scroll position or rebuilding the lists.
        /// </summary>
        internal static async Task SoftRefreshUi()
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.MainWindow;

                if (window != null)
                {
                    // Hack 1: Force the WPF rendering engine to re-evaluate the visual tree
                    window.InvalidateVisual();
                    window.UpdateLayout();
                }

                var contentView = PluginHelper.LaunchBoxMainViewModel.ContentView as FrameworkElement;
                if (contentView != null)
                {
                    var currentContext = contentView.DataContext;              
                    contentView.DataContext = null;
                    contentView.DataContext = currentContext;
                }

            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Recursively searches the visual tree for a FrameworkElement of type T 
        /// that has a specific Binding path on the given DependencyProperty.
        /// </summary>
        public static T FindElementByBinding<T>(DependencyObject parent, DependencyProperty dependencyProperty, string bindingPath) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // Check if the child is the correct type
                if (child is T element)
                {
                    // Check if the binding matches what we are looking for
                    var binding = BindingOperations.GetBinding(element, dependencyProperty);
                    if (binding != null && binding.Path.Path == bindingPath)
                    {
                        return element;
                    }
                }

                // Recursive search deeper into the tree
                T foundChild = FindElementByBinding<T>(child, dependencyProperty, bindingPath);
                if (foundChild != null) return foundChild;
            }
            return null;
        }

        /// <summary>
        /// Updates the StatusText TextBlock in the MainView.
        /// </summary>
        internal static async Task UpdateStatusTextUi(string newText)
        {
            // Ensure this runs on the UI thread
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Retrieve the root window to start the search
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                // Search for the TextBlock bound to "StatusText" 
                var statusTextBlock = FindElementByBinding<TextBlock>(mainWindow, TextBlock.TextProperty, "StatusText");
                if (statusTextBlock == null) return;

                // Clear the original binding to the inaccessible ControlsViewModel
                statusTextBlock.ClearValue(TextBlock.TextProperty);

                // Apply the new hardcoded text
                statusTextBlock.Text = newText;

            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public static T FindButtonByCommand<T>(DependencyObject parent, string commandName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // Check if this child is a Button
                if (child is Button btn)
                {
                    // Check if the command binding path matches
                    var binding = BindingOperations.GetBinding(btn, Button.CommandProperty);
                    if (binding != null && binding.Path.Path == commandName)
                    {
                        return (T)child;
                    }
                }

                // Recursive search
                T foundChild = FindButtonByCommand<T>(child, commandName);
                if (foundChild != null) return foundChild;
            }
            return null;
        }
        internal static async Task RefreshPlayButtonUi()
        {
            var view = PluginHelper.LaunchBoxMainViewModel.GameDetailsView as FrameworkElement;
            if (view == null) return;

            var playButton = FindButtonByCommand<Button>(view, "PlayCommand");
            if (playButton == null) return;

            playButton.InvalidateVisual();
        }



        internal static async Task UpdatePlayButtonUi(IGame game)
        {
            // Pre-calculate state to minimize UI thread work
            bool isInstalling = (game.Status == "Installing");

            // Ensure the passed game object reflects its updated status safely
            if (game.Installed == true && isInstalling)
            {
                game.Status = "Installed";
                isInstalling = false;
            }

            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var view = PluginHelper.LaunchBoxMainViewModel.GameDetailsView as FrameworkElement;
                if (view == null) return;

                var playButton = FindButtonByCommand<Button>(view, "PlayCommand");
                if (playButton == null) return;

                var parent = VisualTreeHelper.GetParent(playButton) as Panel;
                if (parent == null) return;

                var overlayContainer = parent.Children.OfType<Border>().FirstOrDefault(x => (string)x.Tag == "InstallingOverlay");

                // --- OPTIMIZATION 1: Exit Early ---
                // If the game isn't installing, and there is no overlay to hide, do absolutely nothing. 
                // This eliminates all lag/blinking when the user is just browsing normal games.
                if (!isInstalling && overlayContainer == null) return;

                // --- OPTIMIZATION 2: Lazy Overlay Creation ---
                if (overlayContainer == null && isInstalling)
                {
                    overlayContainer = new Border
                    {
                        Tag = "InstallingOverlay",
                        Height = playButton.ActualHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a0553e98")),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e0371f69")),
                        Focusable = false,
                        Opacity = 0,
                        IsHitTestVisible = false
                    };

                    if (parent is Grid) Grid.SetColumnSpan(overlayContainer, 3);

                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    stackPanel.Children.Add(CreateSpinner());
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = "INSTALLING",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 25,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    });

                    overlayContainer.Child = stackPanel;
                    parent.Children.Add(overlayContainer);
                }

                // --- OPTIMIZATION 3: State-Aware UI Updates ---
                // Only update the opacities if they are not already in the correct state.
                if (overlayContainer != null)
                {
                    double targetOverlayOpacity = isInstalling ? 1 : 0;
                    double targetButtonOpacity = isInstalling ? 0 : 1;

                    if (overlayContainer.Opacity != targetOverlayOpacity)
                    {
                        overlayContainer.Opacity = targetOverlayOpacity;
                        overlayContainer.IsHitTestVisible = isInstalling;

                        playButton.Opacity = targetButtonOpacity;
                        playButton.IsHitTestVisible = !isInstalling;

                        var dropdownButton = parent.Children.OfType<Button>().FirstOrDefault(b => b != playButton);
                        if (dropdownButton != null)
                        {
                            dropdownButton.Opacity = targetButtonOpacity;
                            dropdownButton.IsHitTestVisible = !isInstalling;
                        }
                    }
                }

            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static FrameworkElement CreateSpinner()
        {
            // A simple arc path
            var spinner = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 10,0 A 10,10 0 1 1 0,10"),
                Stroke = Brushes.White,
                StrokeThickness = 3,
                Width = 20,
                Height = 20,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Apply rotation
            var rotate = new RotateTransform();
            spinner.RenderTransform = rotate;

            // Animate rotation (Uses GPU-accelerated composition)
            // The constructor for DoubleAnimation defaults to linear interpolation automatically
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };

            rotate.BeginAnimation(RotateTransform.AngleProperty, anim);

            return spinner;
        }



        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                // If this child is the type we are looking for, return it
                if (child != null && child is T)
                    return (T)child;

                // Otherwise, keep searching recursively
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        //public static void ResetPlayButton(Button playButton)
        //{
        //    var textBlock = FindVisualChild<TextBlock>(playButton);
        //    if (textBlock != null)
        //    {
        //        // This clears your hardcoded "Installing..." override.
        //        // WPF immediately falls back to the next priority: the XAML Style/Binding.
        //        textBlock.ClearValue(TextBlock.TextProperty);
        //    }

        //    // Re-enable the button
        //    playButton.IsEnabled = true;

        //    playButton.InvalidateVisual();
        //    playButton.UpdateLayout();

        //    // Remove your "Trap" if you added one
        //    //playButton.PreviewMouseLeftButtonDown -= BlockClick;
        //}


    }
}
