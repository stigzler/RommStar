using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Models;
using RommStar.Core.Primitives;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class ServersPageVM : ObservableObject
    {
        private readonly RommService _rommService;
        private readonly SettingsService _settingsService;

        // The UI binds strictly to this display wrapper collection
        public ObservableCollection<ServerDisplayItem> DisplayServers { get; } = new();

        public ServersPageVM()
        {
        }

        public ServersPageVM(RommService rommService, SettingsService settingsService)
        {
            _rommService = rommService;
            _settingsService = settingsService;

            // Map saved items to our interactive UI wrappers
            foreach (var server in _settingsService.Settings.RommServers)
            {
                var wrappedItem = new ServerDisplayItem(server);
                DisplayServers.Add(wrappedItem);

                // Fire-and-forget a live background health check on load
                _ = CheckServerHealthAsync(wrappedItem);
            }
        }

        // =========================================================================
        // LIVE HEALTH STATUS CHECKING
        // =========================================================================

        private async Task CheckServerHealthAsync(ServerDisplayItem item)
        {
            item.IsWorking = true;
            item.StatusColor = "#A0A0A0"; // Neutral processing tone
            item.ConnectionStatusText = "Connecting...";

            var result = await _rommService.TestConnectionAsync(item.Server);

            if (result.IsSuccess)
            {
                item.StatusColor = "#107C41"; // Microsoft Settings Active Green
                item.ConnectionStatusText = "Connected";
                item.HasError = false;
                item.ErrorMessage = string.Empty;
            }
            else
            {
                item.StatusColor = "#D83B01"; // Microsoft Settings Alert Red
                item.ConnectionStatusText = "Connection Failed";
                item.HasError = true;
                item.ErrorMessage = $"[{result.FailureReason}] {result.ExceptionMessage}";
            }

            item.IsWorking = false;
        }

        // =========================================================================
        // CRUD RELAY COMMANDS
        // =========================================================================

        [RelayCommand]
        public void AddNewServer()
        {
            var blankServer = new RommServer
            {
                ServerName = "New RomM Server",
                BaseUrl = "http://localhost:8080"
            };

            var wrapper = new ServerDisplayItem(blankServer);
            DisplayServers.Add(wrapper);

            // Instantly marks it as requiring input
            wrapper.HasError = true;
            wrapper.ErrorMessage = "Please edit and configure your connection credentials.";
        }

        [RelayCommand]
        public void DeleteServer(ServerDisplayItem item)
        {
            if (item != null)
            {
                DisplayServers.Remove(item);
            }
        }

        [RelayCommand]
        public async Task TestServerConnection(ServerDisplayItem item)
        {
            if (item != null)
            {
                await CheckServerHealthAsync(item);
            }
        }

        // =========================================================================
        // NAVIGATION AUTO-SAVE GATEWAY
        // =========================================================================

        /// <summary>
        /// Invoke this method from your Page's code-behind or shell navigation manager
        /// right before moving away from the Servers view panel.
        /// </summary>
        public void OnNavigatedAway()
        {
            // Re-compile the raw list back down to our clean model layout
            _settingsService.Settings.RommServers = DisplayServers
                .Select(wrapper => wrapper.Server)
                .ToList();

            // Commit transaction to file on disk once
            _settingsService.Save();
        }
    }
}