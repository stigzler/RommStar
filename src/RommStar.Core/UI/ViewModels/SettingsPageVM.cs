using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class SettingsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        public SettingsPageVM(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }
    }
}