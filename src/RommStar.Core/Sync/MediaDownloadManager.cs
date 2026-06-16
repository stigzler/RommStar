using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public class MediaDownloadManager
    {
        public MediaDownloadManager()
        {
        }

        /// <summary>
        /// Transforms a RomDTO's active assets into fully-qualified download/save maps.
        /// </summary>
        public List<MediaDownloadItem> BuildDownloadItems(
            RomDTO rom,
            MediaSelectionProfile profile,
            string baseUrl,
            string launchboxPlatformName)
        {
            var items = new List<MediaDownloadItem>();

            if (rom == null || string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(launchboxPlatformName))
                return items;

            // Ensure our resource directory base path is formed cleanly
            string baseResourceEndpoint = $"{baseUrl.TrimEnd('/')}/assets/romm/resources";

            // TODO: Populate our 14 mapping steps linearly right here

            return items;
        }

        /// <summary>
        /// Combines base addresses, strips off API query parameters, and verifies prefixes.
        /// </summary>
        private string CleanAndCombineUrl(string baseEndpoint, string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            // Step A: Strip query string components out entirely (?ts=2026-06-16...)
            if (rawPath.Contains('?'))
            {
                rawPath = rawPath.Split('?')[0];
            }

            // Step B: Route absolute fallback URIs natively, otherwise combine with base endpoint
            if (rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return rawPath;
            }

            return $"{baseEndpoint}/{rawPath.TrimStart('/')}";
        }

    }
}
