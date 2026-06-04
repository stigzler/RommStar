using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Extensions.DependencyInjection;
using RommStar.Core.Launchbox;
using RommStar.Core.Services;
using RommStar.Core.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core
{
    internal class PluginHost
    {
        private static readonly object _padlock = new object();
        private static PluginHost? _instance;

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
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            //AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            //{
            //    // Get the clean name of the assembly requested (e.g., "iNKORE.UI.WPF")
            //    string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;

            //    // Match against the internal embedded resource logical naming
            //    string requestedDll = assemblyName + ".dll";

            //    var currentAssembly = Assembly.GetExecutingAssembly();

            //    string? resourceName = currentAssembly.GetManifestResourceNames()
            //        .FirstOrDefault(name => name.EndsWith(requestedDll, StringComparison.OrdinalIgnoreCase));

            //    if (resourceName == null) return null;

            //    // Set up our safe dependencies subfolder
            //    string pluginDir = Path.GetDirectoryName(currentAssembly.Location) ?? AppContext.BaseDirectory;
            //    string targetDir = Path.Combine(pluginDir, "Dependencies");

            //    // Append a custom extension so LaunchBox's boot scanner skips right over it
            //    string safeFileName = assemblyName + ".dll.dep";
            //    string targetPath = Path.Combine(targetDir, safeFileName);

            //    // Extract if it doesn't exist yet
            //    if (!File.Exists(targetPath))
            //    {
            //        Directory.CreateDirectory(targetDir);
            //        using (var stream = currentAssembly.GetManifestResourceStream(resourceName))
            //        {
            //            if (stream == null) return null;
            //            using (var fileStream = File.Create(targetPath))
            //            {
            //                stream.CopyTo(fileStream);
            //            }
            //        }
            //    }

            //    // Load the assembly. LoadFrom handles custom extensions perfectly
            //    // while preserving the location context WPF needs for themes.
            //    return Assembly.LoadFrom(targetPath);
            //};

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
            _loggingService.Log($"  LogLevel: {Properties.Settings.Default.LoggingLevel.ToString()}");
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            // Get the clean name of the assembly requested (e.g., "iNKORE.UI.WPF")
            string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;

            // Match against the internal embedded resource logical naming
            string requestedDll = assemblyName + ".dll";

            var currentAssembly = Assembly.GetExecutingAssembly();

            string? resourceName = currentAssembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(requestedDll, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null) return null;

            // Updated target directory name
            string pluginDir = Path.GetDirectoryName(currentAssembly.Location) ?? AppContext.BaseDirectory;
            string targetDir = Path.Combine(pluginDir, "inkoreDlls");

            // Disguise extension to keep LaunchBox from aggressively loading it on boot
            string safeFileName = assemblyName + ".dll.dep";
            string targetPath = Path.Combine(targetDir, safeFileName);

            // Extract the embedded asset if it doesn't exist on disk yet
            if (!File.Exists(targetPath))
            {
                Directory.CreateDirectory(targetDir);
                using (var stream = currentAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using (var fileStream = File.Create(targetPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }

            // Load directly from the safe folder with full context for WPF layout bindings
            return Assembly.LoadFrom(targetPath);
        }

        //private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        //{
        //    // Get the clean name of the assembly being requested (e.g., "iNKORE.UI.WPF.Modern")
        //    string assemblyName = new AssemblyName(args.Name).Name;

        //    // Only intercept requests for iNKORE libraries
        //    if (assemblyName.StartsWith("iNKORE", StringComparison.OrdinalIgnoreCase))
        //    {
        //        // Look inside the 'lib' subfolder relative to your main plugin DLL
        //        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        //        string expectedPath = Path.Combine(pluginDir, "lib", $"{assemblyName}.dll");

        //        if (File.Exists(expectedPath))
        //        {
        //            return Assembly.LoadFrom(expectedPath);
        //        }
        //    }

        //    return null;
        //}

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

                case LaunchboxMenuItem.ToolsMenuRommStar:
                    _loggingService.Log("Tools>RommStar selected.");
                    LaunchAdminWindow();

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

        private void LaunchAdminWindow()
        {
            // Ensure we are running on the UI thread and Application.Current exists
            // Ensure we are running on the UI thread and Application.Current exists
            //if (Application.Current != null)
            //{
            //    // Check if you've already added the resources so you don't duplicate them
            //    bool alreadyLoaded = false;
            //    foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            //    {
            //        // Check by type since the library uses custom ResourceDictionary classes
            //        if (dictionary.GetType().Name == "ThemeResources" || dictionary.GetType().Name == "XamlControlsResources")
            //        {
            //            alreadyLoaded = true;
            //            break;
            //        }
            //    }

            //    if (!alreadyLoaded)
            //    {
            //        // Instantiate the library's custom ResourceDictionary classes directly
            //        var themeStyles = new ThemeResources();
            //        var controlStyles = new XamlControlsResources();

            //        Application.Current.Resources.MergedDictionaries.Add(themeStyles);
            //        Application.Current.Resources.MergedDictionaries.Add(controlStyles);
            //    }
            //}

            // Now safely open your window
            var adminWindow = new MainWindowView();
            adminWindow.ShowDialog();
        }
    }
}