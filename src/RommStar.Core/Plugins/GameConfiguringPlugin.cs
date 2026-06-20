using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins
{
    internal class GameConfiguringPlugin : IGameConfiguringPlugin
    {
        public void OnAfterGameConfigurationOpens(IGame game)
        {
            //throw new NotImplementedException();
        }

        public void OnBeforeGameConfigurationOpens(IGame game)
        {
            Debug.WriteLine("OnBeforeGameConfigurationOpens");

        }

        public void OnGameConfigurationExited(IGame game)
        {// throw new NotImplementedException();
        }
    }
}
