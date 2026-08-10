using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.UI.ViewModels.UserControls
{
    public partial class AddNewPlatformUcVM: ObservableObject
    {
        private readonly LaunchboxDataService _launchboxDataService;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbPlatform> _defaultPlatforms;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbEmulator> _defaultEmulators;

        [ObservableProperty]
        private LaunchboxDbEmulator _selectedDefaultEmulator;

        [ObservableProperty]
        private bool _emulatorNeedsPath = false;

        [ObservableProperty]
        private IEmulator _userEmulator;


        public AddNewPlatformUcVM()
        {
                
        }

        partial void OnSelectedDefaultEmulatorChanged(LaunchboxDbEmulator value)
        {
            UserEmulator = PluginHelper.DataManager.GetAllEmulators().Where(e => e.Title.Equals(SelectedDefaultEmulator.Name, 
                StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (UserEmulator == null)
            {
                EmulatorNeedsPath = true;
                return;
            }

            EmulatorNeedsPath = false;
        }

        public AddNewPlatformUcVM(LaunchboxDataService launchboxDataService)
        {
            _launchboxDataService = launchboxDataService;
        }

        public async Task InitialiseAsync()
        {
            if (DefaultPlatforms != null && DefaultEmulators != null) return;

            DefaultPlatforms = await _launchboxDataService.GetDefaultDbPlatforms();
            DefaultEmulators = await _launchboxDataService.GetDefaultDbEmulators(); 
        }

  

    }
}
