using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;

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

        public MediaDownloadManager() { }

        public List<MediaDownloadItem> BuildDownloadItems(
            RomDTO rom,
            MediaSelectionProfile profile,
            string baseUrl,
            string launchboxPlatformName)
        {
            var items = new List<MediaDownloadItem>();

            if (rom == null || profile?.EnabledTypes == null || string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(launchboxPlatformName))
                return items;

            string baseResourceEndpoint = $"{baseUrl.TrimEnd('/')}{MediaStubPath}";

            foreach (var type in profile.EnabledTypes)
            {
                // Edge Case: Screenshots collection handling
                if (type == MediaType.Screenshot && rom.MergedScreenshots != null && rom.MergedScreenshots.Count > 0)
                {
                    int screenshotIndex = 1;
                    foreach (var screenshotPath in rom.MergedScreenshots)
                    {
                        if (string.IsNullOrWhiteSpace(screenshotPath)) continue;

                        string suffix = screenshotIndex == 1 ? "" : $"-{screenshotIndex}";

                        items.Add(new MediaDownloadItem
                        {
                            MediaType = type,
                            DownloadUrl = CleanAndCombineUrl(baseResourceEndpoint, screenshotPath),
                            TargetLocalPath = ResolveLaunchboxPath(type, launchboxPlatformName, rom.Name, suffix, Path.GetExtension(CleanQueryString(screenshotPath)))
                        });
                        screenshotIndex++;
                    }
                    continue;
                }

                // Standard Property Execution via Strategy Matrix
                if (!AssetSelectors.TryGetValue(type, out var selector)) continue;

                string? relativePath = selector(rom);
                if (string.IsNullOrWhiteSpace(relativePath)) continue;

                items.Add(new MediaDownloadItem
                {
                    MediaType = type,
                    DownloadUrl = CleanAndCombineUrl(baseResourceEndpoint, relativePath),
                    TargetLocalPath = ResolveLaunchboxPath(type, launchboxPlatformName, rom.Name, "", Path.GetExtension(CleanQueryString(relativePath)))
                });
            }

            return items;
        }

        /// <summary>
        /// Combines endpoints, strips redundant stubs, clears query stamps, and checks absolute URLs.
        /// </summary>
        private string CleanAndCombineUrl(string baseEndpoint, string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            // 1. Strip absolute protocol bypasses completely
            if (rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return rawPath;
            }

            // 2. Clear out any query cache string parameters (?ts=...)
            rawPath = CleanQueryString(rawPath);

            // 3. FIX: Strip out the duplicate prefix if it exists in the raw property
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

        private string ResolveLaunchboxPath(MediaType type, string platform, string gameName, string suffix, string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";

            string folderName = type switch
            {
                MediaType.BoxFront => Path.Combine("Images", platform, "Box - Front"),
                MediaType.Box3D => Path.Combine("Images", platform, "Box - Front - 3D"),
                MediaType.BoxBack => Path.Combine("Images", platform, "Box - Back"),
                MediaType.Screenshot => Path.Combine("Images", platform, "Screenshot - Gameplay"),
                MediaType.TitleScreen => Path.Combine("Images", platform, "Screenshot - Title"),
                MediaType.Logo => Path.Combine("Images", platform, "Clear Logo"),
                MediaType.Marquee => Path.Combine("Images", platform, "Arcade - Marquee"),
                MediaType.FanArt => Path.Combine("Images", platform, "Fanart - Background"),
                MediaType.Bezel => Path.Combine("Images", platform, "Arcade - Bezel"),
                MediaType.MixImage => Path.Combine("Images", platform, "Front - Reconstructed"),
                MediaType.PhysicalMedia => Path.Combine("Images", platform, "Cart - Front"),
                MediaType.Manual => Path.Combine("Manuals", platform),
                MediaType.Music => Path.Combine("Music", platform),
                MediaType.Video => Path.Combine("Videos", platform),
                _ => Path.Combine("Images", platform, "Other")
            };

            string targetDir = Path.Combine(Constants.LaunchboxRootDir, folderName);
            return Path.Combine(targetDir, $"{gameName}{suffix}{extension}");
        }
    }
}