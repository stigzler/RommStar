using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Models;
using RommStar.Core.Primitives;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class ServersPageVM : ObservableObject
    {
        private readonly RommService _rommService;
        private CancellationTokenSource? _testCts;

        [ObservableProperty]
        private string _connectionStatusMessage = "Ready to test connection.";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
        private bool _isTestingConnection;

        [ObservableProperty]
        private bool _isSuccessState;

        public ServersPageVM()
        {
        }

        public ServersPageVM(RommService rommService)
        {
            _rommService = rommService;
        }

        /// <summary>
        /// Command called directly by the "Test Connection" button click.
        /// Binds nicely to an asynchronous task execution lifecycle.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanTestConnection))]
        private async Task TestConnectionAsync(RommServerConfig? selectedServer)
        {
            if (selectedServer == null)
            {
                ConnectionStatusMessage = "Please select or configure a valid RomM server card first.";
                IsSuccessState = false;
                return;
            }

            // 1. Setup UI visual execution states
            IsTestingConnection = true;
            IsSuccessState = false;
            ConnectionStatusMessage = $"Connecting to '{selectedServer.ServerName}'...";

            // Create a local cancellation source so the user can back out manually if desired
            _testCts = new CancellationTokenSource();

            try
            {
                // 2. Fire the stateless network verification request
                RommApiResponse result = await _rommService.TestConnectionAsync(selectedServer, _testCts.Token);

                // 3. Process the results cleanly
                if (result.IsSuccess)
                {
                    IsSuccessState = true;
                    ConnectionStatusMessage = $"Successfully verified connection to {selectedServer.ServerName}!";
                }
                else
                {
                    IsSuccessState = false;
                    ConnectionStatusMessage = MapFailureReasonToUserText(result.FailureReason, result.ExceptionMessage);
                }
            }
            finally
            {
                // 4. Reset operation states securely
                _testCts.Dispose();
                _testCts = null;
                IsTestingConnection = false;
            }
        }

        /// <summary>
        /// Command that can be optionally bound to a "Cancel Test" button if the 5s window is too long for the user.
        /// </summary>
        [RelayCommand]
        private void CancelTest()
        {
            _testCts?.Cancel();
            ConnectionStatusMessage = "Connection test was aborted by user.";
            IsTestingConnection = false;
        }

        private bool CanTestConnection() => !IsTestingConnection;

        /// <summary>
        /// Pure translation engine changing strict network errors into helpful troubleshooting instructions.
        /// </summary>
        private string MapFailureReasonToUserText(RommApiFailureReason reason, string? structuralDetails)
        {
            string baseFeedback = reason switch
            {
                RommApiFailureReason.InvalidConfiguration => "URL or API token fields cannot be blank.",
                RommApiFailureReason.Timeout => "Connection timed out. Ensure your RomM container is awake and accessible.",
                RommApiFailureReason.ServerNotFound => "Could not resolve server address. Check your domain name or IP configuration.",
                RommApiFailureReason.Unauthorized => "Access Denied! The RomM API token provided is invalid or expired.",
                RommApiFailureReason.Forbidden => "The API token is valid but lacks sufficient account privileges.",
                RommApiFailureReason.EndpointNotFound => "Connected to server, but the API path wasn't found. Double check your RomM version compatibility.",
                RommApiFailureReason.UnknownServerError => "RomM internal instance error (HTTP 500). Please review your docker/server logs.",
                _ => "An unexpected connection exception occurred."
            };

            // If an explicit network message was caught (like a socket exception string), append it for power users
            if (!string.IsNullOrWhiteSpace(structuralDetails))
            {
                return $"{baseFeedback} Details: ({structuralDetails})";
            }

            return baseFeedback;
        }
    }
}