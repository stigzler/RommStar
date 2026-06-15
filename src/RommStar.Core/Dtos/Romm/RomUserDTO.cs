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


    }
}
