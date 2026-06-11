using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos.Romm
{
    public class RomFileDTO
    {
        /// <summary>
        /// This is Romm's local id for the file
        /// </summary>
        [JsonPropertyName("id")]
        public int Id
        {
            get; set;
        }

        [JsonPropertyName("file_size_bytes")]
        public long FileSizeBytes
        {
            get; set;
        }

        [JsonPropertyName("crc_hash")]
        public string CrcHash
        {
            get; set;
        }

        [JsonPropertyName("md5_hash")]
        public string Md5Hash
        {
            get; set;
        }

        [JsonPropertyName("sha1_hash")]
        public string Sha1Hash
        {
            get; set;
        }
    }
}