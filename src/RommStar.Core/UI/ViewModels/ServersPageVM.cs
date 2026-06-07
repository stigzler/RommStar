using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System.Collections.ObjectModel;

namespace RommStar.Core.UI.ViewModels
{
    public partial class ServersPageVM : ObservableObject
    {
        private readonly RommService _rommService;
        private readonly SettingsService _settingsService;

        // The UI binds strictly to this display wrapper collection
        public ObservableCollection<ServerDisplayItemVM> DisplayServers { get; } = new();

        public ServersPageVM()
        {
            _rommService = new RommService();
            _settingsService = new SettingsService(new CryptoService());
            DisplayServers.Add(new ServerDisplayItemVM(new RommServer()));
        }

        public ServersPageVM(RommService rommService, SettingsService settingsService)
        {
            _rommService = rommService;
            _settingsService = settingsService;

            // Map saved items to our interactive UI wrappers
            foreach (var server in _settingsService.Settings.RommServers)
            {
                var wrappedItem = new ServerDisplayItemVM(server);
                DisplayServers.Add(wrappedItem);

                // Fire-and-forget a live background health check on load
                _ = CheckServerHealthAsync(wrappedItem);
            }
        }

        // =========================================================================
        // LIVE HEALTH STATUS CHECKING
        // =========================================================================

        private async Task CheckServerHealthAsync(ServerDisplayItemVM item, bool isManualTest = false)
        {
            // Reset layout flags before running the connection test
            item.IsMessageDismissed = false;
            item.HasError = false;
            item.HasSuccessMessage = false;

            item.IsWorking = true;
            item.StatusColor = "#A0A0A0";
            item.ConnectionStatusText = "Connecting...";

            // Clear out any old success banner history before running a new test
            item.HasSuccessMessage = false;
            item.SuccessMessage = string.Empty;

            var result = await _rommService.TestConnectionAsync(item.Server);

            if (result.IsSuccess)
            {
                item.StatusColor = "#107C41"; // Microsoft Settings Active Green
                item.ConnectionStatusText = "Connected";
                item.HasError = false;
                item.ErrorMessage = string.Empty;

                // ONLY trip this visibility banner if triggered interactively
                if (isManualTest)
                {
                    item.HasSuccessMessage = true;
                    item.SuccessMessage = $"Successfully authenticated with {item.Server.ServerName}! Ready to sync.";
                }
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
                BaseUrl = "http://localhost:8080",
                ApiToken = "{TokenHere}"
            };

            var wrapper = new ServerDisplayItemVM(blankServer);
            DisplayServers.Add(wrapper);

            // Instantly marks it as requiring input
            wrapper.HasError = true;
            wrapper.ErrorMessage = "Please edit and configure your connection credentials.";
        }

        [RelayCommand]
        public async Task DeleteServer(ServerDisplayItemVM item)
        {
            if (item == null) return;

            ContentDialog dialog = new ContentDialog();
            dialog.Title = "Are you sure?";
            dialog.Content = $"This will permanently delete the server \"{item.Server.ServerName}\" from your settings. Are you sure?";
            dialog.PrimaryButtonText = "Yes";
            dialog.SecondaryButtonText = "No";

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Secondary) return;

            DisplayServers.Remove(item);
        }

        [RelayCommand]
        public async Task TestServerConnection(ServerDisplayItemVM item)
        {
            if (item != null)
            {
                // Set flag to true because the user directly clicked the button!
                await CheckServerHealthAsync(item, isManualTest: true);
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