using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Launchbox;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core
{
    internal class PluginHost
    {
        private static readonly object _padlock = new object();
        private static PluginHost _instance = null;

        // Store your services as private fields
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
            var serviceProvider = services.BuildServiceProvider();

            // Resolve and store your services
            _loggingService = serviceProvider.GetRequiredService<LoggingService>();

            // Start logging
            _loggingService.LogClear();
            _loggingService.Log($"Logging started at {DateTime.Now:dd.MM.yy - HH:mm:ss}");
            _loggingService.Log("PluginHost initialized and services configured.");
            _loggingService.Log("Settings:");
            _loggingService.Log($"  LogLevel: {Properties.Settings.Default.LogLevel.ToString()}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<LoggingService>();
            // Add other services here
        }

        internal void LaunchboxMenuItemSelected(LaunchboxMenuItem menuItem)
        {
            switch (menuItem)
            {
                case LaunchboxMenuItem.SyncPlatform:
                    _loggingService.Log("Sync Platform menu item selected.");
                    // Implement your logic for Sync Platform here
                    break;

                case LaunchboxMenuItem.Settings:
                    _loggingService.Log("Settings menu item selected.");
                    break;
            }
        }

        internal void LaunchboxEventReceived(string eventType)
        {
            _loggingService.Log($"Launchbox event received: {eventType}", Properties.LogLevel.Verbose);
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