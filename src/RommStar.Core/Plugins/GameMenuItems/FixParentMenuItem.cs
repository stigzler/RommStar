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
    internal class FixParentMenuItem : IGameMenuItem
    {
        public string Caption =>$"Fix [{_selectedGame?.Title}]";

        FixToInstalledMenuItem fixToInstalled;
        FixToNotInstalledMenuItem fixToNotInstalled;
        FixAutomaticallyMenuItem fixAutomatically;

        public IEnumerable<IGameMenuItem> Children => new IGameMenuItem[] { fixToInstalled, fixToNotInstalled, fixAutomatically };

        public bool Enabled => true;

        public Image Icon => Properties.Resources.wand;

        private IGame? _selectedGame;

        public FixParentMenuItem(IGame selectedGame)
        {
            _selectedGame = selectedGame;
            fixToInstalled = new FixToInstalledMenuItem(_selectedGame);
            fixToNotInstalled = new FixToNotInstalledMenuItem(_selectedGame);
            fixAutomatically = new FixAutomaticallyMenuItem(_selectedGame);
        }

        public void OnSelect(params IGame[] games)
        {
            _selectedGame = games.LastOrDefault();
        }
    }
}
