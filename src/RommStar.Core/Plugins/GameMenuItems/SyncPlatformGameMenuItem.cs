using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins.GameMenuItems
{
    /// <summary>
    /// PluginHelper.StateManager.GetSelectedPlatform very sketchy and GetAllSelectedGames unreliable - right click a nightmare,
    /// so used hybrid system as IGameMultiMenuItemPlugin.SelectedGame WAS consistent.
    /// </summary>
    internal class SyncPlatformGameMenuItem : IGameMenuItem
    {

        public SyncPlatformGameMenuItem(IGame selectedGame)
        {
            _selectedGame = selectedGame;
        }

        private readonly IGame _selectedGame;

        public string Caption => DynamicCaption();
        public IEnumerable<IGameMenuItem> Children => null;

        public bool Enabled => IsValidForOperation();

        public Image Icon => Properties.Resources.sync;

        public void OnSelect(params IGame[] games)
        {
            // Enabled filter only allows this when only 1 game selected, therefore will awlays be games[0]
            PluginHost.Instance.SyncPlatform(_selectedGame.Platform);
        }

        private bool IsValidForOperation() {
            //if (PluginHelper.StateManager.GetAllSelectedGames()?.Length == 1 &&
            //    !string.IsNullOrEmpty( PluginHelper.StateManager.GetAllSelectedGames()?[0].Platform) 
            //    ) 
            //    return true;

            //return false;
            // return string.IsNullOrEmpty(_selectedGame?.Platform);

            if (PluginHelper.StateManager.GetAllSelectedGames()?.Length == 0) return false;
            if (!SelectedGamesSamePlatform()) return false;
            if (string.IsNullOrEmpty(_selectedGame?.Platform)) return false;

            return true;

        }

        private string DynamicCaption()
        {
            if (PluginHelper.StateManager.GetAllSelectedGames()?.Length == 0) return "Can't Sync: No Game Selected";
            if (!SelectedGamesSamePlatform()) return "Can't Sync: Platforms Mismatch";
            if (string.IsNullOrEmpty(_selectedGame?.Platform)) return "Can't Sync: No Game Platform";

            return $"Sync Platform: {_selectedGame.Platform}";
        }

        private bool SelectedGamesSamePlatform()
        {
            return PluginHelper.StateManager.GetAllSelectedGames()
                .Select(g => g.Platform)
                .Distinct()
                .Count() == 1;
        }

    }
}
