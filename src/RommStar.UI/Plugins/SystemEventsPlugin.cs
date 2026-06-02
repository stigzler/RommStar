using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.UI.Plugins
{
    internal class SystemEventsPlugin : ISystemEventsPlugin
    {
        public void OnEventRaised(string eventType)
        {
            switch (eventType)
            {
                case "PluginInitialized":
                    Debug.WriteLine("Plugin Initialised");
                    break;

                default:
                    break;
            }
        }
    }
}