using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RommStar.Core.Dtos.Romm
{
    public class RomFileDTO
    {
        /// <summary>
        /// This is Romm's local id for the file
        /// </summary>
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public long? FileSizeBytes { get; set; }

        [JsonPropertyName("crc_hash")]
        public string? CrcHash { get; set; }

        [JsonPropertyName("md5_hash")]
        public string? Md5Hash { get; set; }

        [JsonPropertyName("sha1_hash")]
        public string? Sha1Hash { get; set; }

        [JsonPropertyName("is_top_level")]
        public bool IsTopLevel { get; set; }

        public override string ToString()
        {
            return $"[{FileName} ({Category})] Filepath: [{FilePath}], IsTopLevel: [{IsTopLevel}], SizeBytes: [{FileSizeBytes}], Sha1: [{Sha1Hash}]";
        }

    }
}