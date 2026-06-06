using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos
{
    public class RommPlatformDTO
    {
        [JsonPropertyName("id")]
        public int RommId { get; set; }

        [JsonPropertyName("name")]
        public string RommName { get; set; }

        /// <summary>
        /// Console, Computer, Arcade etc
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("rom_count")]
        public int? RomCount { get; set; }

        [JsonPropertyName("custom_name")]
        public string? CustomName { get; set; }

        /// <summary>
        /// NOTE: As of time of writing, this is always null in Romm's API response.
        /// It may be a placeholder for future functionality or a legacy field that is no longer used.
        /// For now, it should be treated as optional and may not contain meaningful data.
        /// </summary>
        [JsonPropertyName("launchbox_id")]
        public int? LaunchboxId { get; set; }

        /// <summary>
        /// NOTE: Included here in case of future expansion. Eg. scraping additional assets from screenscraper.
        /// </summary>
        [JsonPropertyName("ss_id")]
        public int? ScreenscraperId { get; set; }

        /// <summary>
        /// NOTE: For possible future development. Eg. downloading and installing Platform Firmwares in launchbox's emulators
        /// </summary>
        [JsonPropertyName("firmware_count")]
        public int? FirmwareCount { get; set; }

        /// <summary>
        /// The accumulated filesize in bytes of all roms for this system
        /// </summary>
        [JsonPropertyName("fs_size_bytes")]
        public long? AllRomsFileSizeBytes { get; set; }
    }
}