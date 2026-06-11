using RommStar.Core.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomMetadatumDTO
    {
        [JsonPropertyName("age_ratings")]
        public List<string>? 
                            AgeRatings { get; set; }

        [JsonPropertyName("average_rating")]
        public double? 
                            AverageRating { get; set; }

        [JsonPropertyName("collections")]
        public List<string>? 
                            Collections { get; set; }

        [JsonPropertyName("companies")]
        public List<string>? 
                            Companies { get; set; }

        [JsonPropertyName("first_release_date")]
        [JsonConverter(typeof(UnixMillisecondsDateTimeConverter))]
        public DateTime? 
                            FirstReleaseDate { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? 
                            Franchises { get; set; }

        [JsonPropertyName("game_modes")]
        public List<string>? 
                            GameModes { get; set; }

        [JsonPropertyName("genres")]
        public List<string>?
                            Genres { get; set; }

        [JsonPropertyName("player_count")]
        public int? 
                            PlayerCount { get; set; }
    }
}
