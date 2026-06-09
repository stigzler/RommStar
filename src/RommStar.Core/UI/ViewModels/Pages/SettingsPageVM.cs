using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.Pages
{
    public partial class SettingsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private bool _isDarkTheme;

        public SettingsPageVM() : this(new SettingsService(new CryptoService()))
        {
        }

        public SettingsPageVM(SettingsService settingsService)
        {
            _settingsService = settingsService;

            LoadSettings();
            this.PropertyChanged += OnSettingsPropertyChanged;
        }

        private void OnSettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsDarkTheme))
            {
                _settingsService.Settings.DarkModeEnabled = IsDarkTheme;
            }
            _settingsService.Save();
        }

        private void LoadSettings()
        {
            IsDarkTheme = _settingsService.Settings.DarkModeEnabled;
        }
    }
}