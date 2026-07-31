using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Plugins
{
    internal class GameLaunchingPlugin : IGameLaunchingPlugin
    {
        public void OnAfterGameLaunched(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
           PluginHost.Instance.OnGameLaunchingEvent(Launchbox.GameLaunchingEvent.AfterLaunch, game, app, emulator);
        }

        public void OnBeforeGameLaunching(IGame? game, IAdditionalApplication? app, IEmulator? emulator)
        {
            PluginHost.Instance.OnGameLaunchingEvent(Launchbox.GameLaunchingEvent.BeforeLaunch, game,app,emulator);     
        }

        public void OnGameExited()
        {
            PluginHost.Instance.OnGameLaunchingEvent(Launchbox.GameLaunchingEvent.AfterExit);
        }
    }
}
