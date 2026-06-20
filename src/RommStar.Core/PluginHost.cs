using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Launchbox;
using RommStar.Core.Mappers;
using RommStar.Core.Primitives;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using RommStar.Core.Temp;
using RommStar.Core.UI.ViewModels.Pages;
using RommStar.Core.UI.ViewModels.Windows;
using RommStar.Core.UI.Views;
using RommStar.Core.UI.Views.Windows;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core
{
    /// <summary>
    /// Note: Messy atm with INkore stuff it's a pain and poor support, so stuff kept in as ongoign fight!
    /// </summary>
    internal class PluginHost
    {
        private static readonly object _padlock = new object();
        private static PluginHost? _instance;

        private readonly IServiceProvider _serviceProvider;


        private readonly LoggingService _loggingService;
        private readonly SettingsService _settingsService;
        private readonly LaunchService _launchService;


        string _lastEmulatorApplicationPath;
        IEmulator _lastGameLaunchEmulator;
        private bool _mainWindowInitialised = false;
        private MainWindowView _mainWindowView;

        internal static PluginHost Instance {
            get {
                lock (_padlock)
                {
                    if (_instance == null)
                    {
                        _instance = new PluginHost();
                    }
                    return _instance;
                }
            }
        }

        private PluginHost()
        {
            // This picks up the assembly resolution for the iNKORE assemblies, which are disguised with a .dll.dep extension.
            // This necessary because loading them normally (at plugin instantiation) interferes with launchbox libraries
            // and produces an error. By using this method, we can load the iNKORE assemblies only when they are actually needed (when the admin window is launched), and avoid any conflicts with launchbox's assembly loading.
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            _loggingService = _serviceProvider.GetRequiredService<LoggingService>();
            _settingsService = _serviceProvider.GetRequiredService<SettingsService>();

            _loggingService.LogClear();
            _loggingService.Log($"Logging started at {DateTime.Now:dd.MM.yy - HH:mm:ss}");
            _loggingService.Log("PluginHost initialized and services configured.");
            _loggingService.Log("Settings:");
            _loggingService.Log($"  LogLevel: {_settingsService.Settings.LoggingLevel.ToString()}");
        }
        public T FindButtonByCommand<T>(DependencyObject parent, string commandName) where T : DependencyObject
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

        public T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
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

        public void ResetPlayButton(Button playButton)
        {
            var textBlock = FindVisualChild<TextBlock>(playButton);
            if (textBlock != null)
            {
                // This clears your hardcoded "Installing..." override.
                // WPF immediately falls back to the next priority: the XAML Style/Binding.
                textBlock.ClearValue(TextBlock.TextProperty);
            }

            // Re-enable the button
            playButton.IsEnabled = true;

            playButton.InvalidateVisual();
            playButton.UpdateLayout();

            // Remove your "Trap" if you added one
            //playButton.PreviewMouseLeftButtonDown -= BlockClick;
        }

        internal void LaunchboxEventReceived(string eventType)
        {
            _loggingService.Log($"Launchbox event received: {eventType}", LoggingLevel.Verbose);
            switch (eventType)
            {
                case SystemEventTypes.PluginInitialized:
                    PluginInitialised();
                    break;
                case SystemEventTypes.GameStarting:
                    _loggingService.Log("Game starting event received.");
                    break;
                case SystemEventTypes.BigBoxShutdownBeginning:
                    DoShutdownOperations();
                    break;
                case SystemEventTypes.LaunchBoxShutdownBeginning:
                    DoShutdownOperations();
                    break;
                case SystemEventTypes.SelectionChanged:
                    var selectedGames = PluginHelper.StateManager.GetAllSelectedGames();
                    if (selectedGames.Count() == 0) return;
                    UpdatePlayButtonUi(selectedGames[0]);

                    break;

            }
        }

        internal async void LaunchboxMenuItemSelected(LaunchboxMenuItem menuItem)
        {
            switch (menuItem)
            {
                case LaunchboxMenuItem.SyncPlatform:
                    _loggingService.Log("Sync Platform menu item selected.");
                    break;

                case LaunchboxMenuItem.ToolsMenuRommStar:
                    _loggingService.Log("Tools>RommStar selected.");
                    await LaunchAdminWindow();
                    break;
            }
        }

        internal void OnGameLaunchingEvent(GameLaunchingEvent gameLaunchingEvent, IGame? game = null,
                                        IAdditionalApplication? app = null, IEmulator? emulator = null)
        {

            //var playButton = PlayButton();


            switch (gameLaunchingEvent)
            {
                case GameLaunchingEvent.BeforeLaunch:

                    DoPreGameLaunchOperations(game, emulator);
                 
                    break;

                case GameLaunchingEvent.AfterLaunch:


                    //if (playButton != null)
                    //{
                    //    playButton.IsEnabled = false;

                    //    var textBlock = FindVisualChild<TextBlock>(playButton);

                    //    if (textBlock != null)
                    //    {
                    //        // Override the text directly
                    //        textBlock.Text = "INSTALLING...";

                    //        // Force the UI to reflect this change immediately
                    //        playButton.InvalidateVisual();
                    //        playButton.UpdateLayout();
                    //    }
                    //}


                    break;

                case GameLaunchingEvent.AfterExit:

                    // Deal with any Game install logic implemented in GameLaunchingEvent.BeforeLaunch above
                    RestoreGameLaunchEmulatorExe();

                    //ResetPlayButton(playButton);

                    break;
            }

        }

        internal void PluginInitialised()
        {
            // Tests.MediaDownloadManagerTest();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<LoggingService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<RommService>();
            services.AddSingleton<CryptoService>();
            services.AddSingleton<RomMapper>();
            services.AddSingleton<LaunchboxService>();

            // Register initial RommServerConfig & SyncManager
            services.AddSingleton<RommStar.Core.Models.RommServer>(sp => new RommStar.Core.Models.RommServer
            {
                BaseUrl = "http://localhost:8080", // placeholder defaults
                ApiToken = "",
                ServerName = "Default Romm Server"
            });
            services.AddSingleton<SyncManager>();

            // Register ViewModels as singletons
            services.AddSingleton<MainWindowVM>();
            services.AddSingleton<SettingsPageVM>();
            services.AddSingleton<HomePageVM>();
            services.AddSingleton<JobsPageVM>();
            services.AddSingleton<ServersPageVM>();
            services.AddSingleton<PlatformsPageVM>();

            // Register Views as singletons
            // MainWindowView requires all three ViewModels, so use a factory
            services.AddSingleton<MainWindowView>(sp =>
                new MainWindowView(
                    sp.GetRequiredService<MainWindowVM>(),
                    sp.GetRequiredService<HomePageVM>(),
                    sp.GetRequiredService<SettingsPageVM>(),
                    sp.GetRequiredService<JobsPageVM>(),
                    sp.GetRequiredService<ServersPageVM>(),
                    sp.GetRequiredService<PlatformsPageVM>()
                )
            );

            //services.AddSingleton<MainWindowView>();

            //services.AddSingleton<SettingsPageView>();
            //services.AddSingleton<HomePageView>();
            //services.AddSingleton<JobsPageView>();
        }

        // The extracted method for assembly resolution, completely independent of plugin instantiation
        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;

            // Strict filter: only handle the specific iNKORE assemblies
            if (assemblyName != "iNKORE.UI.WPF" &&
                assemblyName != "iNKORE.UI.WPF.Modern" &&
                assemblyName != "iNKORE.UI.WPF.Modern.Controls")
            {
                return null;
            }

            // Look directly inside your custom subfolder for the renamed file
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            string targetPath = Path.Combine(pluginDir, assemblyName + ".dll.dep");

            if (File.Exists(targetPath))
            {
                return Assembly.LoadFrom(targetPath);
            }

            return null;
        }

        private void DoPreGameLaunchOperations(IGame game, IEmulator emulator)
        {
            // 
            if (game == null || emulator == null) return;

            // Check that game's emulator has not been set to the KillGameLaunchExe as a 
            // result of game Installation logic failing
            if (emulator.ApplicationPath == Constants.KillGameLaunchExe && PluginHelper.StateManager.IsBigBox == false)
            {
                // Show in Launchbox
                MessageBox.Show($"It appears that this game's emulator has been set to an operational file used by RommStar. " +
                    $"You will need to re-instate the correct Application Path for this emulator: {emulator.Title}",
                    "RommStar Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _lastEmulatorApplicationPath = emulator.ApplicationPath; // order important here - beware  emulator.ApplicationPath = Constants.KillGameLaunchExe;
            _lastGameLaunchEmulator = emulator;

            // Check if Rom Installation required
            if (game?.Installed == false)
            {
                game.Status = "Installing";

                // TODO: Do install stuff

                // Now set the emulator to an essentially empty exe to fake game launch
                // (No game launch cancel facility in LB sadly)
                emulator.ApplicationPath = Constants.KillGameLaunchExe;

                PluginHelper.DataManager.Save();
                //PluginHelper.LaunchBoxMainViewModel.RefreshData();
                UpdatePlayButtonUi(game);

            }

        }

        private void DoShutdownOperations()
        {
            // Ensure that any manipulation of the last launch Emulator's application path
            // as part of the Game Install strategy is restored 
            RestoreGameLaunchEmulatorExe();
        }

        private void EnsureInkoreResourcesLoaded()
        {
            var app = Application.Current;
            if (app == null) return;

            // Avoid duplicates
            bool alreadyLoaded = app.Resources.MergedDictionaries
                .OfType<iNKORE.UI.WPF.Modern.ThemeResources>()
                .Any();

            if (alreadyLoaded) return;

            // Add inkore dictionaries to the application resources so templates can always find them.
            app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ThemeResources());
            app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.Controls.XamlControlsResources());
            //app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ColorPaletteResources());
            //app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ResourceDictionaryEx());
        }

        private T FindScrollVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private async Task LaunchAdminWindow()
        {
            var adminWindow = _serviceProvider.GetRequiredService<MainWindowView>();

            if (adminWindow.IsVisible)
            {
                if (adminWindow.WindowState == WindowState.Minimized)
                    adminWindow.WindowState = WindowState.Normal;
                adminWindow.Activate();
            }
            else
                adminWindow.Show();
        }



        private void RestoreGameLaunchEmulatorExe()
        {
            if (_lastGameLaunchEmulator != null && _lastEmulatorApplicationPath != Constants.KillGameLaunchExe)
            {
                _lastGameLaunchEmulator.ApplicationPath = _lastEmulatorApplicationPath;
                PluginHelper.DataManager.Save();
            }
        }

        // Use this pattern in your event handlers
        private void UpdatePlayButtonUi(IGame game)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var view = PluginHelper.LaunchBoxMainViewModel.GameDetailsView as FrameworkElement;
                if (view == null) return;

                var playButton = FindButtonByCommand<Button>(view, "PlayCommand");
                if (playButton == null) return;

                var parent = VisualTreeHelper.GetParent(playButton) as Panel;
                if (parent == null) return;

                // 1. FIND OR CREATE (Only add to the visual tree ONCE)
                var overlayContainer = parent.Children.OfType<Border>().FirstOrDefault(x => x.Tag as string == "InstallingOverlay");

                if (overlayContainer == null)
                {
                    overlayContainer = new Border
                    {
                        Tag = "InstallingOverlay",
                        Height = playButton.ActualHeight,
                        Width = playButton.ActualWidth,
                        Margin = playButton.Margin,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#803183E1")),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d03183E1")),
                        Focusable = false,
                        Opacity = 0, // Hidden by default
                        IsHitTestVisible = false
                    };

                    // FIX: Use StackPanel for layout
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
                        Margin = new Thickness(5, 0, 0, 0) // Space between spinner and text
                    });

                    overlayContainer.Child = stackPanel;
                    parent.Children.Add(overlayContainer);
                }

                // 2. TOGGLE (Don't collapse, just change Opacity)
                bool isInstalling = (game.Status == "Installing");

                playButton.Opacity = isInstalling ? 0 : 1;
                playButton.IsHitTestVisible = !isInstalling;

                overlayContainer.Opacity = isInstalling ? 1 : 0;
                overlayContainer.IsHitTestVisible = isInstalling;

            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private FrameworkElement CreateSpinner()
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

    }
}