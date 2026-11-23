using Accessibility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern;
using RommStar.Core.Services;

namespace RommStar.Core.UI.ViewModels.Windows
{
    public partial class MainWindowVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private UpdateInfo? updateInfo;

        public bool IsUpdateAvailable => UpdateInfo?.IsUpdateAvailable == true;

        [ObservableProperty]
        private bool isDarkMode = true;

        public MainWindowVM()
        {
            
        }

        public MainWindowVM(SettingsService settingsService)
        {
            _settingsService = settingsService;

            IsDarkMode = _settingsService.Settings.DarkModeEnabled;
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                UpdateInfo = await UpdateChecker.CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check for RommStar updates: {ex}");
            }
        }

        partial void OnUpdateInfoChanged(UpdateInfo? value)
        {
            OnPropertyChanged(nameof(IsUpdateAvailable));
        }

        partial void OnIsDarkModeChanged(bool value)
        {
            _settingsService.Settings.DarkModeEnabled = value;
            if (isDarkMode)
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            else
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

            _settingsService.Save();
        }
    }
}