using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class UninstallGameMenuItem : IGameMenuItem
    {

        public string Caption => $"Uninstall Game: [{selectedGame.Title}]";

        public IEnumerable<IGameMenuItem> Children => null;

       // public bool Enabled => (bool)(selectedGame?.Installed) ? true: false;

        public bool Enabled => selectedGame.Installed == true;

        public Image Icon => Properties.Resources.uninstall;

        private IGame selectedGame;

        public UninstallGameMenuItem(IGame game)
        {
            selectedGame = game;            
        }

        public void OnSelect(params IGame[] games)
        {
            if (selectedGame == null) return;
            PluginHost.Instance.ProcessUninstallRequest(selectedGame);
            //if (Caption == _installGameText) PluginHost.Instance.ProcessInstallUninstallRequest(games, install: true);
            //else if (Caption == _unInstallGameText) PluginHost.Instance.ProcessInstallUninstallRequest(games, install: false);
        }
    }
}
