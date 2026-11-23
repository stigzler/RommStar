using RommStar.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class SyncPlatformBbGameMenuItem : IGameMenuItemPlugin
    {
        public bool SupportsMultipleGames => false;

        public string Caption => "Sync Platform with RomM";

        public Image IconImage => null;

        public bool ShowInLaunchBox => false;

        public bool ShowInBigBox => true;

        public bool GetIsValidForGame(IGame selectedGame)
        {
            if (selectedGame == null) return false;
            return PluginHost.Instance.LbPlatformHasRomMPlatformMapped(selectedGame.Platform);
        }

        public bool GetIsValidForGames(IGame[] selectedGames)
        {
            return false;
        }

        public async void OnSelected(IGame selectedGame)
        {
            string result = await PluginHost.Instance.SyncPlatform(selectedGame.Platform);
            PluginHelper.DataManager.Save();
            if (PluginHost.Instance.BigBoxSetToShowPopup())
            {
                BigBoxGlobalOverlayHelper.ShowMessage($"{result}");
            }
        }
        public void OnSelected(IGame[] selectedGames)
        {

        }
    }
}
