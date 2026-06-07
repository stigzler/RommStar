using RommStar.Core.Models;
using RommStar.Core.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Properties
{
    public class PluginSettings
    {
        /// <summary>
        /// Store User set Romm Server details
        /// </summary>
        public List<RommServer> RommServers { get; set; } = new();

        public List<PlatformSyncSettings> PlatformSyncSettings { get; set; } = new();

        /// <summary>
        /// RommStar logging level
        /// </summary>
        public LoggingLevel LoggingLevel { get; set; } = LoggingLevel.Normal;

        public bool DarkModeEnabled { get; set; } = true;
    }
}