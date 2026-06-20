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

        private readonly LoggingService _loggingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly SettingsService _settingsService;
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
                    if (selectedGames[0].Status != "Installing")
                    {
                        ResetPlayButton(PlayButton());
                    }

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
            }

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

       
        private Button? PlayButton()
        {
            var gameDetailsView = PluginHelper.LaunchBoxMainViewModel.GameDetailsView as DependencyObject;
            return FindButtonByCommand<Button>(gameDetailsView, "PlayCommand");
        }


        internal void OnGameLaunchingEvent(GameLaunchingEvent gameLaunchingEvent, IGame? game = null,
                                IAdditionalApplication? app = null, IEmulator? emulator = null)
        {

            var playButton = PlayButton();


            switch (gameLaunchingEvent)
            {
                case GameLaunchingEvent.BeforeLaunch:

                    DoPreGameLaunchOperations(game, emulator);
                    break;

                case GameLaunchingEvent.AfterLaunch:                   


                    if (playButton != null)
                    {
                        playButton.IsEnabled = false;

                        var textBlock = FindVisualChild<TextBlock>(playButton);

                        if (textBlock != null)
                        {
                            // Override the text directly
                            textBlock.Text = "INSTALLING...";

                            // Force the UI to reflect this change immediately
                            playButton.InvalidateVisual();
                            playButton.UpdateLayout();
                        }
                    }

     

                    break;

                case GameLaunchingEvent.AfterExit:

                    // Deal with any Game install logic implemented in GameLaunchingEvent.BeforeLaunch above
                    RestoreGameLaunchEmulatorExe();

                    ResetPlayButton(playButton);

                    break;
            }

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



        //public void ForceInstallingState()
        //{
        //    // 1. Get the View and extract its DataContext (The hidden ViewModel)
        //    var view = PluginHelper.LaunchBoxMainViewModel.GameDetailsView as FrameworkElement;
        //    if (view == null || view.DataContext == null) return;

        //    var viewModel = view.DataContext;

        //    try
        //    {
        //        // 2. Reflect into the ViewModel to find the "Game" property shown in your XAML binding
        //        PropertyInfo gameProp = viewModel.GetType().GetProperty("Game");
        //        if (gameProp == null) return;

        //        object internalGameObject = gameProp.GetValue(viewModel);
        //        if (internalGameObject == null) return;

        //        // 3. Reflect into the internal Game object to find the "InstallState" property
        //        PropertyInfo installStateProp = internalGameObject.GetType().GetProperty("InstallState");
        //        if (installStateProp == null) return;

        //        // 4. InstallState is almost certainly an internal Enum. 
        //        // We use Enum.Parse to convert your target string ("Installing") into that hidden Enum type.
        //        object installingEnumValue = Enum.Parse(installStateProp.PropertyType, "Installing");

        //        // 5. Inject the state by bypassing read-only restrictions
        //        if (installStateProp.CanWrite)
        //        {
        //            // The property has a public setter (Unlikely, but good to check)
        //            installStateProp.SetValue(internalGameObject, installingEnumValue);
        //        }
        //        else
        //        {
        //            // Attempt 1: Look for a private or internal setter
        //            MethodInfo privateSetter = installStateProp.GetSetMethod(nonPublic: true);

        //            if (privateSetter != null)
        //            {
        //                // Force invoke the private setter
        //                privateSetter.Invoke(internalGameObject, new object[] { installingEnumValue });
        //            }
        //            else
        //            {
        //                // Attempt 2: Brute force the compiler-generated backing field
        //                // In C#, auto-properties (like 'public InstallState InstallState { get; private set; }')
        //                // create a hidden field named "<PropertyName>k__BackingField".
        //                string backingFieldName = $"<{installStateProp.Name}>k__BackingField";

        //                FieldInfo backingField = internalGameObject.GetType().GetField(
        //                    backingFieldName,
        //                    BindingFlags.Instance | BindingFlags.NonPublic);

        //                if (backingField != null)
        //                {
        //                    // Forcefully inject the value directly into the hidden field
        //                    backingField.SetValue(internalGameObject, installingEnumValue);
        //                }
        //                else
        //                {
        //                    MessageBox.Show("Failed to inject state: No setter or backing field found. It may be a purely calculated property.");
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Reflection can throw exceptions if types mismatch or properties change in future LB updates.
        //        // Log the exception here if needed, or fail silently.
        //    }
        //}

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
    }
}