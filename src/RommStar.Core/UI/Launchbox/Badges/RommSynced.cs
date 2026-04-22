using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.UI.Launchbox.Badges
{
    internal class RommSynced : IGameBadge
    {
        public string Name => "Romm Synced";

        public string UniqueId => "rommstarSynced";

        public Image DefaultIcon => null;

        public int Index { get; set; } = 0;

        public bool GetAppliesToGame(IGame game)
        {
            return true;
        }
    }
}