using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Badges
{
    internal class RommSynced : IGameBadge
    {
        public string Name => "Synced with Romm";

        public string UniqueId => "rommstarSynced";

        public Image DefaultIcon => Properties.Resources.rommIcon64px;

        public int Index { get; set; } = 0;

        public bool GetAppliesToGame(IGame game)
        {
            // IGame default state for Installed is null, Romm Import process will set to either true or false
            // so we can use this to determine if the game has been processed by Romm and if has -
            // show romm icon in badge array.

            return game.GetAllCustomFields().Any(cf => cf.Name == "Romm_RomIds");
            //return game.Installed != null;
        }
    }
}