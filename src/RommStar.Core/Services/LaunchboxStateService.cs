using RommStar.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Services
{
    /// <summary>
    /// Covers things like game launching/selection and LB/BB Views tasks
    /// </summary>
    public class LaunchboxStateService
    {
        LaunchboxDataService _launchboxDataService;

        public LaunchboxStateService(LaunchboxDataService launchboxDataService)
        {
            _launchboxDataService = launchboxDataService;
        }

        string _lastEmulatorApplicationPath;
        IEmulator _lastGameLaunchEmulator;

        internal void DoShutdownOperations()
        {
            // Ensure that any manipulation of the last launch Emulator's application path
            // as part of the Game Install strategy is restored 
            RestoreGameLaunchEmulatorExe();
        }

        internal async Task DownloadRoms()
        {

        }
            

        internal async Task OnGameSelectionChanged()
        {
            var selectedGames = PluginHelper.StateManager.GetAllSelectedGames();
            if (selectedGames != null && selectedGames.Count() > 0)
            {
                await LaunchboxViewsHelper.UpdatePlayButtonUi(selectedGames[0]);
            }
        }

        internal void RestoreGameLaunchEmulatorExe()
        {
            if (_lastGameLaunchEmulator != null && _lastEmulatorApplicationPath != Constants.KillGameLaunchExe)
            {
                _lastGameLaunchEmulator.ApplicationPath = _lastEmulatorApplicationPath;
                PluginHelper.DataManager.Save();
            }
        }

        internal async Task OnBeforeLaunch(IGame game, IEmulator emulator, IAdditionalApplication additionalApplication)
        {
            // 
            if (game == null && additionalApplication == null) return;

            // Check that game's emulator has not been set to the KillGameLaunchExe as a 
            // result of game Installation logic failing
            if (emulator != null)
            {
                if (emulator.ApplicationPath == Constants.KillGameLaunchExe)
                {
                    if (PluginHelper.StateManager.IsBigBox == false)
                    {
                        // Show in Launchbox
                        MessageBox.Show($"It appears that this game's emulator has been set to an operational file used by RommStar. " +
                            $"You will need to re-instate the correct Application Path for this emulator: {emulator.Title}",
                            "RommStar Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    _lastEmulatorApplicationPath = emulator.ApplicationPath; // order important here - beware  emulator.ApplicationPath = Constants.KillGameLaunchExe;
                    _lastGameLaunchEmulator = emulator;

                }
            }

            var apps = game.GetAllAdditionalApplications();

            

            // Check if Rom Installation required
            // This covers both main roms and sibling roms/additional applications
            if (game?.Installed == false && game.Status != "Installing")
            {
                // Update any additional apps to also read updating
                foreach (var app in apps)
                {
                    app.Status = "Installing";
                    app.ApplicationPath = Constants.KillGameLaunchExe;
                }

                game.Status = "Installing";
      
                
                // TODO: Do install stuff

                // Now set the emulator to an essentially empty exe to fake game launch
                // (No game launch cancel facility in LB sadly)
                if (emulator != null || apps.Count() > 0) emulator.ApplicationPath = Constants.KillGameLaunchExe;

                //PluginHelper.DataManager.Save();
                //PluginHelper.LaunchBoxMainViewModel.RefreshData();
                await LaunchboxViewsHelper.UpdatePlayButtonUi(game);
            }
            else if (additionalApplication != null && additionalApplication.Status == "Installing")
            {
                emulator.ApplicationPath = Constants.KillGameLaunchExe;
            }

        }
    }
}
