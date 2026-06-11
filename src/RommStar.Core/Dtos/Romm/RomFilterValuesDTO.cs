using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomFilterValuesDTO
    {
        [JsonPropertyName("genres")]
        public List<string>? 
                            Genres { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? 
                            Franchises { get; set; }


        [JsonPropertyName("collections")]
        public List<string>? 
                            Collections { get; set; }

        [JsonPropertyName("companies")]
        public List<string>? 
                            Companies { get; set; }

        [JsonPropertyName("game_modes")]
        public List<string>? 
                            GameModes { get; set; }

        [JsonPropertyName("age_ratings")]
        public List<string>? 
                            AgeRatings { get; set; }

        [JsonPropertyName("player_counts")]
        public List<string>? 
                            PlayerCounts { get; set; }

        [JsonPropertyName("languages")]
        public List<string>? 
                            Languages { get; set; }

        [JsonPropertyName("regions")]
        public List<string>? 
                            Regions { get; set; }

        [JsonPropertyName("platforms")]
        public List<int>? 
                            Platforms { get; set; }
    }
}
