using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Sync
{
    public class MediaDownloadManager
    {
        private const string MediaStubPath = "/assets/romm/resources";

        private static readonly Dictionary<MediaType, Func<RomDTO, string?>> AssetSelectors = new()
        {
            { MediaType.Manual, rom => rom.MediaManual },
            { MediaType.Video, rom => rom.MediaVideo ?? rom.ScreenscrapeMetadata?.MediaVideo },
            { MediaType.BoxFront, rom => rom.MediaBoxFront ?? rom.MediaBoxFrontSmall },
            { MediaType.Box3D, rom => rom.ScreenscrapeMetadata?.MediaBox3D },
            { MediaType.BoxBack, rom => rom.ScreenscrapeMetadata?.MediaBoxBack },
            { MediaType.Bezel, rom => rom.ScreenscrapeMetadata?.MediaBezel },
            { MediaType.FanArt, rom => rom.ScreenscrapeMetadata?.MediaFanArt },
            { MediaType.Logo, rom => rom.ScreenscrapeMetadata?.MediaLogo },
            { MediaType.Marquee, rom => rom.ScreenscrapeMetadata?.MediaMarquee },
            { MediaType.MixImage, rom => rom.ScreenscrapeMetadata?.MediaMixImage },
            { MediaType.PhysicalMedia, rom => rom.ScreenscrapeMetadata?.MediaPhysicalMedia },
            { MediaType.TitleScreen, rom => rom.ScreenscrapeMetadata?.MediaTitleScreen },
            { MediaType.Screenshot, rom => rom.MergedScreenshots != null && rom.MergedScreenshots.Count > 0 ? rom.MergedScreenshots[0] : null }
        };

        private static readonly Dictionary<MediaType, string> LaunchboxTypeMap = new()
        {
            { MediaType.BoxFront, "Box - Front" },
            { MediaType.Box3D, "Box - 3D" },
            { MediaType.BoxBack, "Box - Back" },
            { MediaType.Screenshot, "Screenshot - Gameplay" },
            { MediaType.TitleScreen, "Screenshot - Game Title" },
            { MediaType.Logo, "Clear Logo" },
            { MediaType.Marquee, "Arcade - Marquee" },
            { MediaType.FanArt, "Fanart - Background" },
            { MediaType.Bezel, "Amazon Background" },
            { MediaType.MixImage, "Box - Front - Reconstructed" },
            { MediaType.PhysicalMedia, "Disc" },
            { MediaType.Manual, "Manuals" },
            { MediaType.Music, "Music" },
            { MediaType.Video, "Videos" }
        };

        public MediaDownloadManager() { }

        public List<MediaDownloadItem> BuildDownloadItems(
            RomDTO rom,
            MediaSelectionProfile profile,
            string baseUrl,
            string launchboxPlatformName,
            IPlatformFolder[] launchboxMediaFolders,
            string romFilename, // Explicitly passed file name (without extension)
            bool forceMediaPriority)
        {
            var items = new List<MediaDownloadItem>();

            if (rom == null || profile?.EnabledTypes == null || string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(launchboxPlatformName))
                return items;

            string baseResourceEndpoint = $"{baseUrl.TrimEnd('/')}{MediaStubPath}";

            // Determine our primary naming token based on UI configuration flag
            string baseTargetName = rom.Name;

            foreach (var type in profile.EnabledTypes)
            {
                // Edge Case: Screenshots collection handling
                if (type == MediaType.Screenshot && rom.MergedScreenshots != null && rom.MergedScreenshots.Count > 0)
                {
                    int screenshotIndex = 1;
                    foreach (var screenshotPath in rom.MergedScreenshots)
                    {
                        if (string.IsNullOrWhiteSpace(screenshotPath)) continue;

                        // Multi-asset suffix rule logic combined with force override flag
                        string suffix = string.Empty;
                        if (forceMediaPriority)
                        {
                            suffix = screenshotIndex == 1 ? "-00" : $"-00_{screenshotIndex}";
                        }
                        else
                        {
                            suffix = screenshotIndex == 1 ? "" : $"-{screenshotIndex}";
                        }

                        items.Add(new MediaDownloadItem
                        {
                            MediaType = type,
                            DownloadUrl = CleanAndCombineUrl(baseResourceEndpoint, screenshotPath),
                            TargetLocalPath = ResolveLaunchboxPath(type, launchboxPlatformName, baseTargetName, suffix, Path.GetExtension(CleanQueryString(screenshotPath)), launchboxMediaFolders)
                        });
                        screenshotIndex++;
                    }
                    continue;
                }

                // Standard Property Execution via Strategy Matrix
                if (!AssetSelectors.TryGetValue(type, out var selector)) continue;

                string? relativePath = selector(rom);
                if (string.IsNullOrWhiteSpace(relativePath)) continue;

                // For single files, apply -00 priority suffix if toggled on
                string fileSuffix = forceMediaPriority ? "-00" : "";

                items.Add(new MediaDownloadItem
                {
                    MediaType = type,
                    DownloadUrl = CleanAndCombineUrl(baseResourceEndpoint, relativePath),
                    TargetLocalPath = ResolveLaunchboxPath(type, launchboxPlatformName, baseTargetName, fileSuffix, Path.GetExtension(CleanQueryString(relativePath)), launchboxMediaFolders)
                });
            }

            return items;
        }

        private string CleanAndCombineUrl(string baseEndpoint, string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            if (rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return rawPath;
            }

            rawPath = CleanQueryString(rawPath);

            if (rawPath.StartsWith(MediaStubPath, StringComparison.OrdinalIgnoreCase))
            {
                rawPath = rawPath.Substring(MediaStubPath.Length);
            }

            return $"{baseEndpoint}/{rawPath.TrimStart('/')}";
        }

        private string CleanQueryString(string path)
        {
            return path.Contains('?') ? path.Split('?')[0] : path;
        }

        private string ResolveLaunchboxPath(
                            MediaType type,
                            string platform,
                            string filenameOrTitle,
                            string suffix,
                            string extension,
                            IPlatformFolder[] launchboxMediaFolders)
        {
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";

            // 1. Sanitize filenameOrTitle using LaunchBox rules:
            // This pattern catches all illegal OS chars plus any existing underscores, 
            // and collapses consecutive matches down to a single underscore.
            if (!string.IsNullOrEmpty(filenameOrTitle))
            {
                filenameOrTitle = Regex.Replace(filenameOrTitle, @"[\\/:*?""<>|']+", "_");

                // Trim any trailing or leading underscores if LaunchBox does so 
                // (Optional: leave .Trim('_') out if you want to preserve edge case placements)
                // filenameOrTitle = filenameOrTitle;
            }

            string folderPath = string.Empty;

            if (LaunchboxTypeMap.TryGetValue(type, out string lbTypeStr) && launchboxMediaFolders != null)
            {
                var matchedFolder = launchboxMediaFolders.FirstOrDefault(f =>
                    string.Equals(f.MediaType, lbTypeStr, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Platform, platform, StringComparison.OrdinalIgnoreCase));

                if (matchedFolder != null)
                {
                    folderPath = matchedFolder.FolderPath;
                }
            }

            if (string.IsNullOrEmpty(folderPath))
            {
                string fallbackSubFolder = type switch
                {
                    MediaType.BoxFront => Path.Combine("Images", platform, "Box - Front"),
                    MediaType.BoxBack => Path.Combine("Images", platform, "Box - Back"),
                    MediaType.Video => Path.Combine("Videos", platform),
                    MediaType.Manual => Path.Combine("Manuals", platform),
                    MediaType.Music => Path.Combine("Music", platform),
                    _ => Path.Combine("Images", platform, "Other")
                };
                folderPath = Path.Combine(Constants.LaunchboxRootDir, fallbackSubFolder);
            }
            else
            {
                folderPath = NormalizeFolderPath(Constants.LaunchboxRootDir, folderPath);
            }

            return Path.Combine(folderPath, $"{filenameOrTitle}{suffix}{extension}");
        }

        private string NormalizeFolderPath(string baseRootDir, string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return baseRootDir;
            if (Path.IsPathRooted(rawPath)) return Path.GetFullPath(rawPath);

            string combined = Path.Combine(baseRootDir, rawPath);
            return Path.GetFullPath(combined);
        }
    }
}