using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Plugins
{
    internal class SystemEventsPlugin : ISystemEventsPlugin
    {
        public void OnEventRaised(string eventType)
        {
            PluginHost.Instance.LaunchboxEventReceived(eventType);
        }
    }
}