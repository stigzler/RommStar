using RommStar.Core.Launchbox;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Plugins
{
    internal class SystemMenuItemPlugin : ISystemMenuItemPlugin
    {
        public string Caption => "Rommstar Settings";

        public Image IconImage => Properties.Resources.rommIcon64px;

        public bool ShowInLaunchBox => true;

        public bool ShowInBigBox => false;

        public bool AllowInBigBoxWhenLocked => false;

        public void OnSelected()
        {
            PluginHost.Instance.LaunchboxMenuItemSelected(LaunchboxMenuItem.ToolsMenuRommStar);
        }
    }
}