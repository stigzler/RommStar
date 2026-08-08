using RommStar.Core.Launchbox;
using RommStar.Core.Plugins.GameMenuItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins
{
    internal class GameMultiMenuItemPlugin : IGameMultiMenuItemPlugin
    {
        public IEnumerable<IGameMenuItem> GetMenuItems(params IGame[] selectedGames)
        {
            IGameMenuItem syncPlatform = new SyncPlatformGameMenuItem(selectedGames.FirstOrDefault());

            IGameMenuItem openAdmin = new OpenAdminWindowMenuItem();

            GameMenuItem uninstallGame = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "Uninstall Game",
                Enabled = true
            };

            // hacky but no other way
            GameMenuItem separator = new GameMenuItem()
            {
                Icon = null,
                Caption = "------------",
                Enabled = false
            };

            GameMenuItem rommMenuItem = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "RomM",
                Enabled = true,
                Children = new List<IGameMenuItem>() { syncPlatform, uninstallGame, openAdmin }
            };





            return new List<IGameMenuItem>() { rommMenuItem };
        }
    }
}