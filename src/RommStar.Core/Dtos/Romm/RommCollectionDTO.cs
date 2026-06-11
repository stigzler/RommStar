using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomCollectionDTO
    {
        [JsonPropertyName("items")]
        public List<RomDTO>?
                            Items { get; set; }

        [JsonPropertyName("total")]
        public int?
                            Total { get; set; }

        [JsonPropertyName("limit")]
        public int?
                            Limit { get; set; }

        [JsonPropertyName("offset")]
        public int?
                            Offset { get; set; }

        [JsonPropertyName("rom_id_index")]
        public List<int>?
                            RomIdIndex { get; set; }

        [JsonPropertyName("filter_values")]
        public RomFilterValuesDTO?
                            FilterValues { get; set; }

    }
}
