using Riok.Mapperly.Abstractions;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

// TODO: Publisher/Developer (romm only has "organisaitons" - have raised an issue on romm github
// https://github.com/rommapp/romm/issues/3518 - he agreed to do it.

namespace RommStar.Core.Mappers
{

    [Mapper(PropertyNameMappingStrategy = PropertyNameMappingStrategy.CaseInsensitive,
            RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class RomMapper
    {
        private SettingsService _settingsService;
        public RomMapper(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [MapProperty(nameof(romDto.Name), nameof(iGame.Title), Use = nameof(PassthroughMapping))]
        [MapProperty(nameof(romDto.Summary), nameof(iGame.Notes), Use = nameof(PassthroughMapping))]
        [MapProperty(nameof(romDto.LaunchboxId), nameof(iGame.LaunchBoxDbId))]


        [MapProperty("LaunchboxMetadata.WikipediaUrl", nameof(iGame.WikipediaUrl))]
        [MapProperty("LaunchboxMetadata.ReleaseType", nameof(iGame.ReleaseType))]

        [MapProperty(nameof(romDto.RomUserData), nameof(iGame.Progress), Use = nameof(RommStatusToLaunchboxProgress))]

        [MapProperty("Metadatum.FirstReleaseDate", nameof(IGame.ReleaseDate))]
        [MapProperty("Metadatum.Companies", nameof(IGame.Developer), Use = nameof(FlattenToSemicolonString))]
        [MapProperty("Metadatum.Genres", nameof(IGame.Genres), Use = nameof(MapBlockingCollection))]
        [MapProperty("Metadatum.GameModes", nameof(IGame.PlayMode), Use = nameof(FlattenToSemicolonString))]
        [MapProperty("Metadatum.Franchises", nameof(IGame.Series), Use = nameof(FlattenToSemicolonString))]
        [MapProperty("Metadatum.AverageRating", nameof(IGame.StarRatingFloat), Use = nameof(MapRatingToFloat))]
        [MapProperty("Metadatum.AgeRatings", nameof(IGame.Rating), Use = nameof(MapAgeRating))]
        [MapProperty("Metadatum.PlayerCount", nameof(IGame.MaxPlayers), Use = nameof(MapMaxPlayers))]
        [MapProperty(nameof(romDto.Regions), nameof(IGame.Region), Use = nameof(MapFirstListItem))]
        [MapProperty(nameof(romDto.YoutubeVideoId), nameof(IGame.VideoUrl), Use = nameof(MapYouTubeUrl))]
        public partial void RommRomDtoToIGame(RomDTO romDto, IGame iGame);


        [UserMapping]
        public string RommStatusToLaunchboxProgress(RomUserDTO? userDTO)
        {
            // Fail-safe if the DTO is completely null
            if (userDTO == null) return string.Empty;

            // Convert nullable bools to strict true/false for cleaner matching
            bool nowPlaying = userDTO.NowPlaying ?? false;
            bool backlogged = userDTO.Backlogged ?? false;

            // Normalize the status string (trimming whitespace prevents accidental mismatches)
            string? status = userDTO.Status?.Trim();

            // Tuple pattern matching acts exactly like your truth table
            return (nowPlaying, backlogged, status) switch
            {
                // --- ACTIVE STATES ---
                (true, false, "incomplete") => "Active / In Progress",
                (true, false, null) => "Active / Continuous",
                (true, true, null) => "Active / Paused",
                (true, true, "incomplete") => "Active / Paused",

                // --- DONE STATES ---
                (false, false, "finished") => "Done / Beaten",
                (false, false, "completed_100") => "Done / Completed",
                (false, false, "retired") => "Done / Dropped",

                // --- NOT STARTED STATES ---
                (false, false, "never_playing") => "Not Started / Won't Play",

                // Want to Play: Backlogged is true, status is either incomplete or null
                (false, true, "incomplete") => "Not Started / Want to Play",
                (false, true, null) => "Not Started / Want to Play",

                // Unplayed: Backlogged is false, status is either incomplete or null
                (false, false, "incomplete") => "Not Started / Unplayed",
                (false, false, null) => "Not Started / Unplayed",

                // --- FALLBACK ---
                // If a combination occurs that isn't on the truth table, return empty
                _ => string.Empty
            };
        }


        [UserMapping]
        public string PassthroughMapping(string? value)
        {
            return value;
        }

        [UserMapping]
        public string MapYouTubeUrl(string? youtubeVideoId)
        {
            if (string.IsNullOrWhiteSpace(youtubeVideoId))
            {
                return string.Empty;
            }

            // Grab the stub from your injected settings service
            string stub = _settingsService.Settings?.YouTubeStub ?? string.Empty;

            // Ensure we handle trimming and slash consistency cleanly
            stub = stub.Trim();
            string videoId = youtubeVideoId.Trim();

            return stub + videoId;

            
        }


        [UserMapping]
        public string MapFirstListItem(List<string>? source)
        {
            if (source == null || source.Count == 0)
            {
                return string.Empty; // Or "Unknown", depending on your preference for LaunchBox
            }

            // Grab the very first item that isn't null or whitespace, and clean it up
            var firstValue = source.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim();

            return firstValue ?? string.Empty;
        }

        [UserMapping]
        public int? MapMaxPlayers(string? sourcePlayerCount)
        {
            if (string.IsNullOrWhiteSpace(sourcePlayerCount))
            {
                return null;
            }

            // 1. Split by hyphen to handle ranges like "1-32"
            string[] parts = sourcePlayerCount.Split('-');

            // 2. Grab the last part (will be "1" for single values, or "32" for ranges)
            string maxPart = parts[^1].Trim(); // Uses the C# hat operator to take the last element

            // 3. Attempt to parse it cleanly into a native nullable int
            if (int.TryParse(maxPart, out int maxPlayers))
            {
                return maxPlayers;
            }

            // Fallback: If RomM sends something completely unexpected (like "Unknown" or "4+"),
            // return null so LaunchBox leaves it safely unassigned.
            return null;
        }

        [UserMapping]
        public string MapAgeRating(List<string>? sourceRatings)
        {
            if (sourceRatings == null || sourceRatings.Count == 0)
            {
                return "Not Rated";
            }

            RatingStandard activeScheme = _settingsService.Settings.RatingStandard;
            string? genericFallbackName = null;

            // Pass 1: Scan for an exact match in your preferred scheme, 
            // while keeping track of any other valid standard we encounter along the way.
            foreach (var rawRating in sourceRatings)
            {
                if (string.IsNullOrWhiteSpace(rawRating)) continue;

                string cleanKey = rawRating.Trim();

                if (Constants.AgeRatingLookup.TryGetValue(cleanKey, out var definition))
                {
                    // Perfect match found! Return it immediately.
                    if (definition.Standard == activeScheme)
                    {
                        return definition.LaunchboxName;
                    }

                    // Secondary match found (e.g., PEGI instead of ESRB). 
                    // Save it just in case we don't find our preferred scheme later in the list.
                    genericFallbackName ??= definition.LaunchboxName;
                }
            }

            // Pass 2: If we didn't find the preferred scheme, use the alternate mapped standard name
            if (genericFallbackName != null)
            {
                return genericFallbackName;
            }

            // Pass 3: Ultimate Fallback. If the code wasn't even in our dictionary at all,
            // grab the raw string value or mark it unrated.
            var rawFallback = sourceRatings.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim();
            return !string.IsNullOrEmpty(rawFallback) ? rawFallback : "Not Rated";
        }


        [UserMapping]
        public float MapRatingToFloat(float? sourceRating)
        {
            if (sourceRating == null)
            {
                return 0.0f;
            }

            // 1. Scale down from 0-100 to 0-5 (e.g., 85.872f / 20.0f = 4.2936f)
            float scaledValue = sourceRating.Value / 20.0f;

            // 2. Round precisely to 1 decimal place (4.2936f becomes 4.3f)
            return MathF.Round(scaledValue, 1, MidpointRounding.AwayFromZero);
        }


        /// <summary>
        /// Converts a list of strings into a standardized semicolon-delimited single string
        /// For Launchbox quirk. With some collections (eg. PlayModes), the collection itself
        /// is read-only. You have to add a semicolon sep single string to the singular field 
        /// of the collection to populate the collection (eg. PlayMode = "Single PLayer; Split Screen; Online PvP")
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        [UserMapping]
        public string FlattenToSemicolonString(List<string>? source)
        {
            if (source == null || source.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", source
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim().Replace(";", ","))
                .Distinct());
        }

        [UserMapping]
        public string[] StringListToArray(List<string>? source)
        {
            return source.ToArray();
        }

        /// <summary>
        /// MEthod to convert IEnumerable<string> property from rommAPI to IGame BlockingCollection<string>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        [UserMapping]
        private void MapBlockingCollection(List<string>? source, BlockingCollection<string> target)
        {
            if (target == null) return;

            // 1. Clear existing items to prevent duplicates on resync
            while (target.Count > 0)
            {
                target.TryTake(out _);
            }

            if (source == null) return;

            // 2. Populate the live LaunchBox collection instance
            foreach (var item in source)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                var cleanedItem = item.Trim();

                // Check if it already exists (ignoring upper/lowercase differences just in case)
                bool alreadyExists = target.Any(x => x.Equals(cleanedItem, StringComparison.OrdinalIgnoreCase));

                if (!alreadyExists)
                {
                    target.Add(cleanedItem);
                }
            }
        }
    }
}