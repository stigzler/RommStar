using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class OpenAdminWindowMenuItem : IGameMenuItem

    {
        public string Caption => "Open Admin Window";

        public IEnumerable<IGameMenuItem> Children => null;

        public bool Enabled => true;

        public Image Icon => Properties.Resources.gear__pencil;

        public void OnSelect(params IGame[] games)
        {
             _ = PluginHost.Instance.LaunchAdminWindow();
        }
    }
}
