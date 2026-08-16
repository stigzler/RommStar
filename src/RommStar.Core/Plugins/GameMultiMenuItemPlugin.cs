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
            IGameMenuItem syncPlatform = new SyncPlatformGameMenuItem(selectedGames.LastOrDefault());

            IGameMenuItem openAdmin = new OpenAdminWindowMenuItem();

            IGameMenuItem uninstallGame = new UninstallGameMenuItem(selectedGames.LastOrDefault());

            IGameMenuItem fixParentItem = new FixParentMenuItem(selectedGames.LastOrDefault());

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
                //Children = new List<IGameMenuItem>() { syncPlatform,openAdmin    } // NB uninstallGame must be last else crashes subsequent menu items
            };

            List<IGameMenuItem> gameMenuItems = new List<IGameMenuItem>();

            gameMenuItems.Add(syncPlatform);
            gameMenuItems.Add(fixParentItem);

            if (selectedGames.LastOrDefault()?.Installed == true) gameMenuItems.Add(uninstallGame);

            gameMenuItems.Add(openAdmin);


            rommMenuItem.Children = gameMenuItems;

            return new List<IGameMenuItem>() { rommMenuItem };
        }
    }
}