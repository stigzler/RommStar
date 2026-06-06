using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Common;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class ServerDisplayItem : ObservableObject
    {
        // The core underlying domain data
        public RommServer Server { get; }

        [ObservableProperty] private string _statusColor = "#808080"; // Gray default (Unchecked)
        [ObservableProperty] private string _connectionStatusText = "Unchecked";
        [ObservableProperty] private bool _isWorking;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private bool _hasSuccessMessage;
        [ObservableProperty] private string _successMessage = string.Empty;
        [ObservableProperty] private bool _isMessageDismissed;
        [ObservableProperty] private PasswordRevealMode _apiKeyVisibility = PasswordRevealMode.Hidden;

        [RelayCommand]
        private void DismissMessage()
        {
            IsMessageDismissed = true;
        }

        [RelayCommand]
        public void ToggleApiKeyVisibility()
        {
            if (ApiKeyVisibility == PasswordRevealMode.Hidden)
            {
                ApiKeyVisibility = PasswordRevealMode.Visible;
            }
            else
            {
                ApiKeyVisibility = PasswordRevealMode.Hidden;
            }
        }

        public ServerDisplayItem(RommServer server)
        {
            Server = server;
        }
    }
}