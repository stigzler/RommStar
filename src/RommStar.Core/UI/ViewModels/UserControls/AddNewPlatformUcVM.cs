using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using RommStar.Core.Dtos;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.UI.ViewModels.UserControls
{
    public partial class AddNewPlatformUcVM : ObservableObject
    {
        private readonly LaunchboxDataService _launchboxDataService;

        [ObservableProperty]
        private bool? _autoExtract = false;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbEmulatorDTO> _defaultEmulators;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbEmulatorPlatformDTO> _defaultEmultorPlatforms;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbPlatformDTO> _defaultPlatforms;
        [ObservableProperty]
        private bool _emulatorNeedsPath = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private string _exePath;

        [ObservableProperty]
        private bool _infoBarVisible = true;

        [ObservableProperty]
        private string _infoMessage;

        [ObservableProperty]
        private InfoBarSeverity _infoSeverity;

        [ObservableProperty]
        private bool? _m3uDiskLoadEnabled = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private LaunchboxDbEmulatorDTO _selectedDefaultEmulator;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private LaunchboxDbPlatformDTO _selectedDefaultPlatform;
        [ObservableProperty]
        private IEmulator? _userEmulator;

        [ObservableProperty]
        private IEmulatorPlatform? _userEmulatorPlatform;

        [ObservableProperty]
        //[NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private IPlatform? _userPlatform;
        public bool EmulatorPlatformPropsSettable => !(UserPlatform != null && UserEmulator != null && UserEmulatorPlatform != null);


        public AddNewPlatformUcVM()
        {

        }


        public AddNewPlatformUcVM(LaunchboxDataService launchboxDataService)
        {
            _launchboxDataService = launchboxDataService;
        }

        public void ClearData()
        {
            SelectedDefaultPlatform = null;
            SelectedDefaultEmulator = null;
            ExePath = null;
            M3uDiskLoadEnabled = null;
            AutoExtract = null;
            ResolveInfoBar();
        }

        public async Task InitialiseAsync()
        {
            if (DefaultPlatforms != null && DefaultEmulators != null) return;

            DefaultPlatforms = await _launchboxDataService.GetDefaultDbPlatforms();
            DefaultEmulators = await _launchboxDataService.GetDefaultDbEmulators();
            DefaultEmultorPlatforms = await _launchboxDataService.GetDefaultDbEmulatorPlatforms();
        }

        partial void OnSelectedDefaultEmulatorChanged(LaunchboxDbEmulatorDTO value)
        {
            if (value == null)
            {
                ResolveInfoBar();
                return;
            }

            UserEmulator = PluginHelper.DataManager.GetAllEmulators().Where(e => e.Title.Equals(SelectedDefaultEmulator.Name,
                    StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Set AutoExtract to the default for this platfrom.emu combo.
            var emulatorDTO = DefaultEmulators.FirstOrDefault(ep => ep.Name.Equals(SelectedDefaultEmulator.Name, StringComparison.OrdinalIgnoreCase));

            if (emulatorDTO != null)  AutoExtract = emulatorDTO.AutoExtract == true;
     
            if (UserEmulator == null)
            {
                ExePath = null;
                EmulatorNeedsPath = true;
                M3uDiskLoadEnabled = false;
                ResolveInfoBar();
                return;
            }

            ExePath = UserEmulator.ApplicationPath;
            EmulatorNeedsPath = false;

            if (emulatorDTO != null)
            {
                AutoExtract = emulatorDTO.AutoExtract == true;
            }

            // Now sets extract and m33u boxes to the actual values set in the emulator if it's already in the db
            UserEmulatorPlatform = UserEmulator.GetAllEmulatorPlatforms().
                Where(ep => ep.EmulatorId == UserEmulator.Id && ep.Platform == UserPlatform?.Name && ep.IsDefault == true).FirstOrDefault();

            if (UserEmulatorPlatform != null)
            {
                M3uDiskLoadEnabled = UserEmulatorPlatform.M3uDiscLoadEnabled;
                AutoExtract = UserEmulatorPlatform.AutoExtract == true;
            }
      

            ResolveInfoBar();
        }

        partial void OnSelectedDefaultPlatformChanged(LaunchboxDbPlatformDTO value)
        {
            if (SelectedDefaultPlatform == null) return;


            UserPlatform = PluginHelper.DataManager.GetAllPlatforms().Where(e => e.Name.Equals(SelectedDefaultPlatform.Name,
                                StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (UserPlatform != null)
            {
                var platfromEmulators = PluginHelper.DataManager.GetAllEmulators()
                    .Where(e => e.GetAllEmulatorPlatforms()
                        .Any(ep => ep.Platform.Equals(UserPlatform.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                SelectedDefaultEmulator = DefaultEmulators.Where(e => e.Name.Equals(platfromEmulators[0].Title, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                ResolveInfoBar();
            }
            else
            {
                SelectedDefaultEmulator = null;
                ExePath = null;
                ResolveInfoBar();
            }

            // do not need ResolveInfoBar here as OnSelectedDefaultEmulatorChanged ALWAYS fires after this due to data bindings.
        }

        [RelayCommand]
        private async Task OpenEmulatorDownloadPage()
        {
            if (SelectedDefaultEmulator != null)
            {
                string url = SelectedDefaultEmulator.URL;
                RommStar.Core.Helpers.ProcessHelper.OpenLinkInBrowser(url);
            }
        }
        private void ResolveInfoBar()
        {
            if (SelectedDefaultPlatform == null)
                UpdateInfoBar("Please select a Platform.", InfoBarSeverity.Warning);

            else if (UserPlatform != null)
                UpdateInfoBar("Platform already exists. Cannot be Added.", InfoBarSeverity.Error);

            else if (SelectedDefaultEmulator == null)
                UpdateInfoBar("Please choose an Emulator for this Platform.", InfoBarSeverity.Warning);

            else if (SelectedDefaultEmulator != null && string.IsNullOrEmpty(ExePath))
                UpdateInfoBar("Please choose an executable for this Emulator.", InfoBarSeverity.Warning);

            else
            {
                UpdateInfoBar("Valid Setup. Good to proceed.", InfoBarSeverity.Success);
                InfoBarVisible = true;
                return;
            }

            InfoBarVisible = true;

        }

        [RelayCommand]
        private async Task SetExecutablePath()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = "Please select the Emulator's executable",
                CheckPathExists = true,
                InitialDirectory = Path.Combine(Constants.LaunchboxRootDir, "Emulators")
            };

            var result = openFileDialog.ShowDialog();

            if (result != true) return;

            ExePath = openFileDialog.FileName;

            ResolveInfoBar();
        }
        private void UpdateInfoBar(string message, InfoBarSeverity infoBarSeverity)
        {
            InfoMessage = message;
            InfoSeverity = infoBarSeverity;

        }



    }
}
