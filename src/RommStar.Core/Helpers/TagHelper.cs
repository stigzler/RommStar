using RommStar.Core.Launchbox;
using System.IO;
using System.Text.RegularExpressions;


namespace RommStar.Core.Helpers
{
    public static class TagHelper
    {
        private static readonly Regex TagsAtEndRegex = new Regex(
            @"(?:[ \t]*(?:\[[^\]]*\]|\{[^\}]*\}|\([^\)]*\)|<[^>]*>))+$",
            RegexOptions.Compiled);

        // Individual bracket extractor to process tags one by one from left to right
        private static readonly Regex IndividualTagRegex = new Regex(
            @"([\[\{\(<][^\]\}\)>]*[\]\}\)>])",
            RegexOptions.Compiled);

        // Broad set of standard No-Intro/Redump/TOSEC regions (including historical variants)
        private static readonly HashSet<string> KnownRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USA", "Europe", "Japan", "Asia", "Brazil", "Australia", "Canada", "Korea", "China", "Taiwan",
            "France", "Germany", "Spain", "Italy", "United Kingdom", "UK", "Netherlands", "Sweden",
            "Denmark", "Norway", "Finland", "Oceania", "World", "Global", "North America", "Latin America"
        };

        public static string ExtractEndTagsFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            Match match = TagsAtEndRegex.Match(nameWithoutExtension);
            return match.Success ? match.Value.Trim() : string.Empty;
        }

        private static readonly Regex MediaKeywordRegex = new Regex(
            @"\b(Disc|Disk|Part|Tape|Card|File)\s+([0-9A-Z]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SideRegex = new Regex(
            @"\bSide\s+([A-B])\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FallbackMediaRegex = new Regex(
            @"^\(([0-9A-Z])\)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Analyzes a filename from No-Intro, Redump, or TOSEC and distills its media layout into LaunchBox-ready properties.
        /// </summary>
        public static AdditionalApplicationDetails ParseFilename(string fileName)
        {
            var result = new AdditionalApplicationDetails();

            if (string.IsNullOrWhiteSpace(fileName))
                return result;

            string cleanName = Path.GetFileNameWithoutExtension(fileName);

            // Track 1: Identify explicit Side A / Side B assignments
            Match sideMatch = SideRegex.Match(cleanName);
            if (sideMatch.Success)
            {
                string sideLetter = sideMatch.Groups[1].Value.ToUpper();
                result.IsSideA = (sideLetter == "A");
                result.IsSideB = (sideLetter == "B");
            }

            // Track 2: Identify structural media counts (Disc, Disk, Part, etc.)
            Match mediaMatch = MediaKeywordRegex.Match(cleanName);
            if (mediaMatch.Success)
            {
                string rawValue = mediaMatch.Groups[2].Value;
                result.DiscNumber = ConvertValueToInteger(rawValue);
            }
            else
            {
                MatchCollection brackets = Regex.Matches(cleanName, @"\([^)]+\)");
                for (int i = brackets.Count - 1; i >= 0; i--)
                {
                    Match fallbackMatch = FallbackMediaRegex.Match(brackets[i].Value);
                    if (fallbackMatch.Success)
                    {
                        result.DiscNumber = ConvertValueToInteger(fallbackMatch.Groups[1].Value);
                        break;
                    }
                }
            }

            // Track 4: Extract Regions & Version metadata blocks
            string rawEndTags = ExtractEndTagsFromFileName(fileName);
            if (!string.IsNullOrEmpty(rawEndTags))
            {
                MatchCollection tags = IndividualTagRegex.Matches(rawEndTags);
                List<string> foundRegions = new List<string>();
                List<string> versionTags = new List<string>();

                foreach (Match tagMatch in tags)
                {
                    string tagWithBrackets = tagMatch.Value;

                    // ALWAYS keep every tag in the Version property string
                    versionTags.Add(tagWithBrackets);

                    // Strip brackets to check if this tag contains a region
                    string innerContent = tagWithBrackets.Substring(1, tagWithBrackets.Length - 2).Trim();

                    // Check for multi-region strings split by commas (e.g., "Japan, North America")
                    string[] potentialSubRegions = innerContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    bool isRegionTag = false;
                    List<string> verifiedSubRegions = new List<string>();

                    foreach (var sub in potentialSubRegions)
                    {
                        string trimmedSub = sub.Trim();
                        if (KnownRegions.Contains(trimmedSub))
                        {
                            isRegionTag = true;
                            verifiedSubRegions.Add(trimmedSub);
                        }
                    }

                    if (isRegionTag)
                    {
                        string combinedRegionBlock = string.Join(", ", verifiedSubRegions);
                        if (!foundRegions.Contains(combinedRegionBlock))
                        {
                            foundRegions.Add(combinedRegionBlock);
                        }
                    }
                }

                // Format Region output using LaunchBox's preferred semicolon format
                if (foundRegions.Any())
                {
                    result.Region = string.Join("; ", foundRegions);
                }

                // Combine all tags back together as a displayable string variant collection
                if (versionTags.Any())
                {
                    result.Version = string.Join(" ", versionTags);
                }
            }

            return result;
        }

        private static int? ConvertValueToInteger(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue))
                return null;

            rawValue = rawValue.ToUpper();

            if (int.TryParse(rawValue, out int parsedInt))
            {
                return parsedInt;
            }

            if (rawValue.Contains("-"))
            {
                string firstPart = rawValue.Split('-')[0];
                if (int.TryParse(firstPart, out int parsedRangeStart))
                    return parsedRangeStart;
            }

            if (rawValue.Length == 1 && rawValue[0] >= 'A' && rawValue[0] <= 'Z')
            {
                return rawValue[0] - 'A' + 1;
            }

            return null;
        }
    }
}

