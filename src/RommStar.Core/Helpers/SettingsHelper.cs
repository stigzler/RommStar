using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RommStar.Core.Helpers
{
    internal static class SettingsHelper
    {
        public static void CheckForSettingsUpgrade()
        {
            if (Properties.Settings.Default.UpgradeRequired)
            {
                // Pulls settings from the previous version's user.config
                Properties.Settings.Default.Upgrade();

                // Set the flag to false so we don't upgrade again next launch
                Properties.Settings.Default.UpgradeRequired = false;

                // Save the updated flag and migrated settings
                Properties.Settings.Default.Save();
            }
        }
    }
}