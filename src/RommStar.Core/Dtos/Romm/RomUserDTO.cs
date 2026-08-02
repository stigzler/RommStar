using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomUserDTO
    {
        [JsonPropertyName("is_main_sibling")]
        public bool?
                            IsMainSibling { get; set; }

        [JsonPropertyName("status")]
        public string?
                    Status { get; set; }

        [JsonPropertyName("backlogged")]
        public bool?
            Backlogged { get; set; }

        [JsonPropertyName("now_playing")]
        public bool?
            NowPlaying { get; set; }

        [JsonPropertyName("hidden")]
        public bool?
            Hidden { get; set; }

    }
}
