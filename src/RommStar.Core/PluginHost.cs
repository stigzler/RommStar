using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Launchbox;
using RommStar.Core.Mappers;
using RommStar.Core.Primitives;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using RommStar.Core.UI.ViewModels.Pages;
using RommStar.Core.UI.ViewModels.UserControls;
using RommStar.Core.UI.ViewModels.Windows;
using RommStar.Core.UI.Views.UserControls;
using RommStar.Core.UI.Views.Windows;
using System.IO;
using System.Reflection;
using System.Windows;
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

        private readonly LaunchboxStateService _launchboxStateService;
        private readonly LoggingService _loggingService;
        private readonly RomBatchService _romBatchService;
        private readonly IServiceProvider _serviceProvider;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationsService;

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
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            _loggingService = _serviceProvider.GetRequiredService<LoggingService>();
            _settingsService = _serviceProvider.GetRequiredService<SettingsService>();
            _launchboxStateService = _serviceProvider.GetRequiredService<LaunchboxStateService>();
            _romBatchService = _serviceProvider.GetRequiredService<RomBatchService>();
            _notificationsService = _serviceProvider.GetRequiredService<NotificationService>();

            _loggingService.LogClear();
            _loggingService.Log($"Logging started at {DateTime.Now:dd.MM.yy - HH:mm:ss}");
            _loggingService.Log("PluginHost initialized and services configured.");
            _loggingService.Log("Settings:");
            _loggingService.Log($"  LogLevel: {_settingsService.Settings.LoggingLevel.ToString()}");
        }

        internal async void LaunchboxEventReceived(string eventType)
        {
            _loggingService.Log($"Launchbox event received: {eventType}", LoggingLevel.Verbose);
            switch (eventType)
            {
                case SystemEventTypes.PluginInitialized:
                    break;
                case SystemEventTypes.LaunchBoxStartupCompleted:
                    _romBatchService.StartService();
                    break;
                case SystemEventTypes.GameStarting:
                    break;
                case SystemEventTypes.BigBoxShutdownBeginning:
                    _romBatchService.StopService();
                    _launchboxStateService.DoShutdownOperations();
                    break;
                case SystemEventTypes.LaunchBoxShutdownBeginning:
                    _romBatchService.StopService();
                    _launchboxStateService.DoShutdownOperations();
                    break;
                case SystemEventTypes.SelectionChanged:
                    await _launchboxStateService.OnGameSelectionChanged();
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

        internal async Task OnGameLaunchingEvent(GameLaunchingEvent gameLaunchingEvent, IGame? game = null,
                                        IAdditionalApplication? app = null, IEmulator? emulator = null)
        {
            switch (gameLaunchingEvent)
            {
                case GameLaunchingEvent.BeforeLaunch:

                    await _launchboxStateService.OnBeforeLaunch(game, emulator, app);

                    break;

                case GameLaunchingEvent.AfterLaunch:

                    break;

                case GameLaunchingEvent.AfterExit:

                    // Deal with any Game install logic implemented in GameLaunchingEvent.BeforeLaunch above
                    _launchboxStateService.RestoreGameLaunchEmulatorExe();
                    break;
            }

        }

        internal async void SyncPlatform(string platformName)
        {
            await _launchboxStateService.SyncPlatform(platformName);
        }



        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<LoggingService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<RommService>();
            services.AddSingleton<CryptoService>();
            services.AddSingleton<RomMapper>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<LaunchboxDataService>();
            services.AddSingleton<LaunchboxStateService>();
            services.AddSingleton<RomBatchService>();

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
            services.AddSingleton<RomQueuePageVM>();
            services.AddSingleton<AddNewPlatformUcVM>();


            // Register Views as singletons
            // MainWindowView requires all three ViewModels, so use a factory
            services.AddSingleton<MainWindowView>(sp =>
                new MainWindowView(
                    sp.GetRequiredService<MainWindowVM>(),
                    sp.GetRequiredService<HomePageVM>(),
                    sp.GetRequiredService<SettingsPageVM>(),
                    sp.GetRequiredService<JobsPageVM>(),
                    sp.GetRequiredService<ServersPageVM>(),
                    sp.GetRequiredService<PlatformsPageVM>(),
                    sp.GetRequiredService<RomQueuePageVM>()
                )
            );

            services.AddTransient<AddNewPlatformUcView>(sp =>
                new AddNewPlatformUcView(
                    sp.GetRequiredService<AddNewPlatformUcVM>()
                ));



        }

        /// <summary>
        /// The extracted method for assembly resolution, completely independent of plugin instantiation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
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

        // Left in given possible ongoign difficulties with inkore givne this is plugin not an app. 
        //private void EnsureInkoreResourcesLoaded()
        //{
        //    var app = Application.Current;
        //    if (app == null) return;

        //    // Avoid duplicates
        //    bool alreadyLoaded = app.Resources.MergedDictionaries
        //        .OfType<iNKORE.UI.WPF.Modern.ThemeResources>()
        //        .Any();

        //    if (alreadyLoaded) return;

        //    // Add inkore dictionaries to the application resources so templates can always find them.
        //    app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ThemeResources());
        //    app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.Controls.XamlControlsResources());
        //    //app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ColorPaletteResources());
        //    //app.Resources.MergedDictionaries.Add(new iNKORE.UI.WPF.Modern.ResourceDictionaryEx());
        //}



        internal async Task LaunchAdminWindow()
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

    }
}