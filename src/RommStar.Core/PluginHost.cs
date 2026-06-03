using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Launchbox;
using RommStar.Core.Services;
using RommStar.Core.UI.ViewModels;
using RommStar.Core.UI.Views;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RommStar.Core
{
    internal class PluginHost
    {
        private static readonly object _padlock = new object();
        private static PluginHost? _instance;

        // Store your services as private fields
        private readonly IServiceProvider _serviceProvider;

        private readonly LoggingService _loggingService;

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
            var services = new ServiceCollection();
            ConfigureServices(services);

            // Build the provider ONCE and keep it for the lifetime of the singleton
            _serviceProvider = services.BuildServiceProvider();

            // Resolve and store your services
            _loggingService = _serviceProvider.GetRequiredService<LoggingService>();

            // Start logging
            _loggingService.LogClear();
            _loggingService.Log($"Logging started at {DateTime.Now:dd.MM.yy - HH:mm:ss}");
            _loggingService.Log("PluginHost initialized and services configured.");
            _loggingService.Log("Settings:");
            _loggingService.Log($"  LogLevel: {Properties.Settings.Default.LoggingLevel.ToString()}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // App Services
            services.AddSingleton<LoggingService>();

            // Wpf-Ui Navigation Services
            services.AddSingleton<INavigationViewPageProvider, NavigationViewPageProvider>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();

            // Views and ViewModel
            services.AddScoped<MainWindowVM>();
            services.AddScoped<MainWindowView>();

            services.AddScoped<DashboardPageVM>();
            services.AddScoped<DashboardPageView>();

            services.AddScoped<SettingsPageVM>();
            services.AddScoped<SettingsPageView>();
        }

        internal void LaunchboxMenuItemSelected(LaunchboxMenuItem menuItem)
        {
            switch (menuItem)
            {
                case LaunchboxMenuItem.SyncPlatform:
                    _loggingService.Log("Sync Platform menu item selected.");
                    // Implement your logic for Sync Platform here
                    break;

                case LaunchboxMenuItem.ToolsMenuRommStar:
                    _loggingService.Log("Tools>RommStar selected.");

                    var mainWindow = _serviceProvider.GetRequiredService<MainWindowView>();
                    mainWindow.ShowDialog();

                    break;
            }
        }

        internal void LaunchboxEventReceived(string eventType)
        {
            _loggingService.Log($"Launchbox event received: {eventType}", LoggingLevel.Verbose);
            // Implement your logic for handling different Launchbox events here
            switch (eventType)
            {
                case Unbroken.LaunchBox.Plugins.Data.SystemEventTypes.PluginInitialized:
                    //PluginHost.Instance.PluginInitialized();
                    break;

                default:
                    break;
            }
        }
    }
}