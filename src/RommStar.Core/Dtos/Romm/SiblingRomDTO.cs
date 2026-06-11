using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class SiblingRomDTO
    {
        [JsonPropertyName("fs_name_no_ext")]
        public string?
                            FsNameNoExt { get; set; }

        [JsonPropertyName("fs_name_no_tags")]
        public string?
                            FsNameNoTags { get; set; }

        [JsonPropertyName("id")]
        public int?
                            Id { get; set; }

        [JsonPropertyName("name")]
        public string?
                            Name { get; set; }
        [JsonPropertyName("sort_comparator")]
        public string?
                            SortComparator { get; set; }
    }
}
