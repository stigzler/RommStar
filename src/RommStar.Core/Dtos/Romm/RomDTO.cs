using System.Text.Json.Serialization;

namespace RommStar.Core.Dtos.Romm
{
    /// <summary>
    /// The Rom object is kind of the Game. 
    /// sibling_roms: Diff versions of a Game (eg. V2, cracked etc) are stored as separate Roms and ref'd in 
    /// 
    /// </summary>
    public class RomDTO
    {
        [JsonPropertyName("alternative_names")]
        public List<string>?
                            AlternativeNames { get; set; }

        [JsonPropertyName("files")]
        public List<RomFileDTO>? 
                            Files { get; set; }

        [JsonPropertyName("fs_name")]
        public string 
                            RommFilename { get; set; }    

        [JsonPropertyName("has_multiple_files")]
        public bool?
                            HasMultipleFiles { get; set; }

        [JsonPropertyName("has_nested_single_file")]
        public bool?
                            HasNestedSingleFile { get; set; }

        [JsonPropertyName("has_simple_single_file")]
        public bool?
                            HasSimpleSingleFile { get; set; }

        /// <summary>
        /// This is the rommId local to the Romm Sever and used in API calls
        /// There is no universal, canon Id in romm (it doesn't have it's own db)
        /// </summary>
        [JsonPropertyName("id")]
        public int?
                            Id { get; set; }

        [JsonPropertyName("is_identified")]
        public bool?
                            IsIdentified { get; set; }

        [JsonPropertyName("languages")]
        public List<string>?
                            Languages { get; set; }

        [JsonPropertyName("launchbox_id")]
        public int?
                            LaunchboxId { get; set; }

        [JsonPropertyName("metadatum")]
        public RomMetadatumDTO?
                            Metadatum { get; set; }

        [JsonPropertyName("missing_from_fs")]
        public bool?
                            MissingFromFileSystem { get; set; }

        /// <summary>
        /// Romm.Name (Romm's specific Rom name)
        /// </summary>
        [JsonPropertyName("name")]
        public string
                            Name { get; set; }

        [JsonPropertyName("regions")]
        public List<string>?
                            Regions { get; set; }

        [JsonPropertyName("rom_user")]
        public RomUserDTO?
                            RomUserData { get; set; }

        [JsonPropertyName("ss_id")]
        public int?
                            ScreenscraperId { get; set; }

        [JsonPropertyName("sibling_roms")]
        public List<SiblingRomDTO>?
                            SiblingRoms { get; set; }

        [JsonPropertyName("slug")]
        public string? 
                            Slug { get; set; }


        [JsonPropertyName("summary")]
        public string
                            Summary { get; set; }

        [JsonPropertyName("youtube_video_id")]
        public string 
                            YoutubeVideoId { get; set; }
    }
}