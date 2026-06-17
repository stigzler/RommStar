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
        private readonly SettingsService _settingsService;

        private readonly IServiceProvider _serviceProvider;

        private MainWindowView _mainWindowView;

        internal static PluginHost Instance
        {
            get
            {
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

            }
        }

        internal void PluginInitialised()
        {
           // Tests.MediaDownloadManagerTest();
        }

        private bool _mainWindowInitialised = false;

        private async Task LaunchAdminWindow()
        {
            // Ensure the Inkore resources are loaded
            //EnsureInkoreResourcesLoaded();

            //Tests.MediaDownloadManagerTest();
            //return;

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