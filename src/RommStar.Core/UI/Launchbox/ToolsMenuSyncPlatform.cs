using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.UI.Launchbox
{
    internal class ToolsMenuSyncPlatform : ISystemMenuItemPlugin
    {
        public string Caption => "Rommstar Sync Platform";

        public Image IconImage => Properties.Resources.rommIcon64px;

        public bool ShowInLaunchBox => true;

        public bool ShowInBigBox => true;

        public bool AllowInBigBoxWhenLocked => false;

        public void OnSelected()
        {
            PluginHost.Instance.ToolsMenuItemSelected(ToolMenuItem.SyncPlatform);
        }
    }
}