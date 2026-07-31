using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core
{
    internal static class Constants
    {
        internal static readonly string LaunchboxRootDir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName ?? AppContext.BaseDirectory;

        internal const string MediaPacksPlatformIconsRelPath = @"Images\Media Packs\Platform Icons";

        internal static readonly string PluginRootDir = Path.Combine(LaunchboxRootDir, @"Plugins\RommStar");

        internal static readonly string DummyEmulatorExe = Path.Combine(LaunchboxRootDir, @"Plugins\RommStar\RommStar.DummyEmulator.exe");

        /// <summary>
        /// This has to be relevant to launchbox root to ensure multi-version rom games show the 
        /// multi version badge 🤷
        /// </summary>
        internal static readonly string romPlaceholder = Path.Combine(@"Plugins\RommStar", "Game Installation Required");

        internal static readonly Dictionary<string, RatingDefinition> AgeRatingLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            // ESRB Standard
            { "ec",  new(RatingStandard.ESRB, "EC - Early Childhood") },
            { "e",   new(RatingStandard.ESRB, "E - Everyone") },
            { "e10", new(RatingStandard.ESRB, "E10+ - Everyone 10+") },
            { "t",   new(RatingStandard.ESRB, "T - Teen") },
            { "m",   new(RatingStandard.ESRB, "M - Mature") },
            { "ao",  new(RatingStandard.ESRB, "AO - Adults Only 18+") },

            // Future Proofing Examples (PEGI)
            { "pegi3",  new(RatingStandard.Pegi, "PEGI 3") },
            { "pegi7",  new(RatingStandard.Pegi, "PEGI 7") },
            { "pegi12", new(RatingStandard.Pegi, "PEGI 12") },
            { "pegi16", new(RatingStandard.Pegi, "PEGI 16") },
            { "pegi18", new(RatingStandard.Pegi, "PEGI 18") }
        };
    }
}