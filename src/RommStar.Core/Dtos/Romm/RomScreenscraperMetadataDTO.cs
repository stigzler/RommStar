using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomScreenscraperMetadataDTO
    {
        [JsonPropertyName("bezel_path")]
        public string? MediaBezel { get; set; }

        [JsonPropertyName("box2d_back_path")]
        public string? MediaBoxBack { get; set; }

        [JsonPropertyName("box3d_path")]
        public string? MediaBox3D { get; set; }

        [JsonPropertyName("fanart_path")]
        public string? MediaFanArt { get; set; }

        [JsonPropertyName("miximage_path")]
        public string? MediaMixImage { get; set; }

        [JsonPropertyName("physical_path")]
        public string? MediaPhysicalMedia { get; set; }

        [JsonPropertyName("marquee_path")]
        public string? MediaMarquee { get; set; }

        [JsonPropertyName("logo_path")]
        public string? MediaLogo { get; set; }

        [JsonPropertyName("title_screen_path")]
        public string? MediaTitleScreen { get; set; }

        [JsonPropertyName("video_normalized_path")]
        public string? MediaVideo { get; set; }


    }
}
