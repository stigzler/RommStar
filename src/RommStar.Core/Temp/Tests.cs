using RommStar.Core.Models;
using RommStar.Core.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

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

        internal static void AddNewLaunchboxPlatform()
        {
            IPlatform newPLatform = PluginHelper.DataManager.AddNewPlatform("TestPLatform");
            newPLatform.ScrapeAs = "TestPLatScrapeAs";
        }
    }
}