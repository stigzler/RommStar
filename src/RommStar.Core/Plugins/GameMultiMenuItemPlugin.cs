using RommStar.Core.Launchbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins
{
    internal class GameMultiMenuItemPlugin : IGameMultiMenuItemPlugin
    {
        public IEnumerable<IGameMenuItem> GetMenuItems(params IGame[] selectedGames)
        {
            GameMenuItem testSubItem1 = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "Test Sub Item 1",
                Enabled = true
            };
            GameMenuItem uninstallGame = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "Uninstall Game",
                Enabled = true
            };

            GameMenuItem rommMenuItem = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "RomM",
                Enabled = true,
                Children = new List<IGameMenuItem>() { uninstallGame }
            };

            return new List<IGameMenuItem>() { rommMenuItem };
        }
    }
}