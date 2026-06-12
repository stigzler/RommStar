using RommStar.Core.Dtos.Romm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    public class PlatformSyncSettings
    {
        /// <summary>
        /// Uses IPlatform.Name
        /// </summary>
        public string LaunchboxPlatformName { get; set; }

        public Guid RommServerId { get; set; }

        /// <summary>
        /// Bit hacky but meh
        /// </summary>
        public List<PlatformDTO> RommServerPlatforms { get; set; }

       public ExtendedSyncSettings ExtendedSyncSettings { get; set; } = new ExtendedSyncSettings();
    }
}