
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Plugins
{
    internal class GameMenuItemPlugin : IGameMenuItemPlugin
    {
        public bool SupportsMultipleGames => false;

        public string Caption => "Uninstall Game";

        public Image IconImage => null;

        public bool ShowInLaunchBox => false;

        public bool ShowInBigBox => true;

        public bool GetIsValidForGame(IGame selectedGame)
        {
            return (selectedGame.Installed == true);
        }

        public bool GetIsValidForGames(IGame[] selectedGames)
        {
            return false;
        }

        public void OnSelected(IGame selectedGame)
        {
            if (selectedGame == null) return;
            PluginHost.Instance.ProcessUninstallRequest(selectedGame);
        }

        public void OnSelected(IGame[] selectedGames)
        {
            
        }
    }
}
