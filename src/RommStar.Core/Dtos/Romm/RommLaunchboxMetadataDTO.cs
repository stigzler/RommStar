using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RommLaunchboxMetadataDTO
    {
        [JsonPropertyName("wikipedia_url")]
        public string?
                    WikipediaUrl { get; set; }

        [JsonPropertyName("release_type")]
        public string?
                    ReleaseType { get; set; }

    }
}
