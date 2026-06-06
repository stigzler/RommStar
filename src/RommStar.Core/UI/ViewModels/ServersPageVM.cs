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

        public ObservableCollection<RommServer> Servers { get; }

        public ServersPageVM()
        {
        }

        public ServersPageVM(RommService rommService, SettingsService settingsService)
        {
            _rommService = rommService;
            _settingsService = settingsService;

            Servers = new ObservableCollection<RommServer>(_settingsService.Settings.RommServers);
        }
    }
}