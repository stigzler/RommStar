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
        private readonly string _installGameText = "Install Game/s";
        private readonly string _unInstallGameText = "Uninstall Game/s";

        public string Caption => (bool)(selectedGame?.Installed) ? $"{_unInstallGameText}" : $"{_installGameText}";

        public IEnumerable<IGameMenuItem> Children => null;

       // public bool Enabled => (bool)(selectedGame?.Installed) ? true: false;

        public bool Enabled => true;

        public Image Icon => (bool)(selectedGame?.Installed) ? Properties.Resources.uninstall : Properties.Resources.install;

        private IGame selectedGame;

        public UninstallGameMenuItem(IGame game)
        {
            selectedGame = game;
        }

        public void OnSelect(params IGame[] games)
        {
            if (Caption == _installGameText) PluginHost.Instance.ProcessInstallUninstallRequest(games, install: true);
            else if (Caption == _unInstallGameText) PluginHost.Instance.ProcessInstallUninstallRequest(games, install: false);
        }
    }
}
