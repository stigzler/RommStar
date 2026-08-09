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
        public string Caption => (bool)(selectedGame?.Installed) ? $"Uninstall Game: [{selectedGame.Title}]" : $"Install Game: [{selectedGame.Title}]";

        public IEnumerable<IGameMenuItem> Children => throw new NotImplementedException();

       // public bool Enabled => (bool)(selectedGame?.Installed) ? true: false;

        public bool Enabled => true;


        public Image Icon => (bool)(selectedGame?.Installed) ? Properties.Resources.box__minus : Properties.Resources.installing;

        private IGame selectedGame;

        public UninstallGameMenuItem(IGame game)
        {
            selectedGame = game;
        }

        public void OnSelect(params IGame[] games)
        {
             
        }
    }
}
