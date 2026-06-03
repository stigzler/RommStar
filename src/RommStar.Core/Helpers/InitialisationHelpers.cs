using RommStar.Core.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace RommStar.Core.Helpers
{
    internal static class InitialisationHelpers
    {
        internal static void CheckSettings()
        {
            UpgradeSettingsIfNeeded();
        }

        internal static void UiSetup()
        {
            // Set the accent color to a romm standard as per here: https://docs.romm.app/latest/Miscellaneous/Brand-Guidelines/
            ApplicationAccentColorManager.Apply(
                Color.FromArgb(0xFF, 0x55, 0x3e, 0x98),
                ApplicationTheme.Dark
            );
        }

        private static void UpgradeSettingsIfNeeded()
        {
            string configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            if (!File.Exists(configPath))
            {
                //Existing user config does not exist, so load settings from previous assembly
                Settings.Default.Upgrade();
                Settings.Default.Reload();
                Settings.Default.Save();
            }
        }
    }
}