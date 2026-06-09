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
        internal static void AddNewLaunchboxPlatform()
        {
            IPlatform newPLatform = PluginHelper.DataManager.AddNewPlatform("TestPLatform");
            newPLatform.ScrapeAs = "TestPLatScrapeAs";
        }
    }
}