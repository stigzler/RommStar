using RommStar.Core.Models;
using RommStar.Core.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Temp
{
    internal class Tests
    {
        internal static void PopulateTestSettings(PluginSettings settings)
        {
            settings.RommServers.Clear();
            settings.RommServers.AddRange(
                new RommServer
                {
                    ServerName = "stig.life",
                    BaseUrl = "https://roms.stif.life",
                    ApiToken = "rmm_43249141784587d5d34c2956f03960ff80af9f1d21233695e27ed8d7dc1bc897"
                }
                );
        }
    }
}