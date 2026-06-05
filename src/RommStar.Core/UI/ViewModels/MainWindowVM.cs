using Accessibility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class MainWindowVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private bool isDarkMode = true;

        public MainWindowVM(SettingsService settingsService)
        {
            _settingsService = settingsService;

            isDarkMode = _settingsService.Settings.DarkModeEnabled;
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