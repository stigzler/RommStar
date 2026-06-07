using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unbroken.LaunchBox.Plugins;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using System.Xml.Linq;

namespace RommStar.Core.Services
{
    public class LaunchboxService
    {
        public LaunchboxSettings LaunchboxSettings { get; set; } = new LaunchboxSettings();

        public LaunchboxService()
        {
            PopulateLaunchboxSettings();
        }

        public List<LaunchboxPlatformDTO> GetPlatforms()
        {
            var livePlatforms = PluginHelper.DataManager.GetAllPlatforms();
            if (livePlatforms == null) return new List<LaunchboxPlatformDTO>();

            return livePlatforms.Select(p => new LaunchboxPlatformDTO
            {
                Name = p.Name,
                ScrapeAs = p.ScrapeAs,
                SortTitle = p.SortTitle,
                // If you need NestedName or SortTitleOrTitle, calculate them cleanly here
            })
            .OrderBy(p => p.Name)
            .ToList();
        }

        private void PopulateLaunchboxSettings()
        {
            XDocument doc = XDocument.Load(Path.Combine(Constants.LaunchboxRootDir, "Data\\Settings.xml"));
            var settings = doc.Root?.Element("Settings");
            if (settings?.Element("PlatformIconPack")?.Value is string iconPack && !string.IsNullOrWhiteSpace(iconPack))
            {
                LaunchboxSettings.PlatformIconPack = iconPack;
            }
        }

        //public string ResolvePlatformIconPath(string platformName)
        //{
        //    string imageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Platforms", platformName, "Clear Logo");
        //    if (!Directory.Exists(imageFolder)) return string.Empty;

        //    return Directory.EnumerateFiles(imageFolder)
        //        .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        //                             f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        //}
    }
}