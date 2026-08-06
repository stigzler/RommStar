using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Badges
{
    internal class InstallingBadge : IGameBadge
    {
        public string Name => "Installing from RomM";

        public string UniqueId => "installingFromRomm";

        public Image DefaultIcon => Properties.Resources.installing;

        public int Index { get; set; } = 0;

        public bool GetAppliesToGame(IGame game)
        {
            return game.Status == "Installing";
        }
    }
}