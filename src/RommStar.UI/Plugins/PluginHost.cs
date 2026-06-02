using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.UI.Plugins
{
    public class PluginHost
    {
        private static readonly object _padlock = new object();
        private static PluginHost _instance = null;

        // Store your services as private fields
        private readonly Core.Services.LoggingService _loggingService;

        public static PluginHost Instance
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
            _loggingService.Log($"  LogLevel: {Core.Properties.Settings.Default.LogLevel.ToString()}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<LoggingService>();
            // Add other services here
        }

        internal void ToolsMenuItemSelected(LaunchboxMenuItem menuItem)
        {
            switch (menuItem)
            {
                case LaunchboxMenuItem.SyncPlatform:
                    _loggingService.Log("Sync Platform menu item selected.");
                    // Implement your logic for Sync Platform here
                    break;

                case LaunchboxMenuItem.ToolsSettings:
                    _loggingService.Log("Settings menu item selected.");
                    break;
            }
        }
    }
}