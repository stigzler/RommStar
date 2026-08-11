using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
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
        private IEnumerable<LaunchboxDbPlatform> _defaultPlatforms;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbEmulator> _defaultEmulators;

        [ObservableProperty]
        private IEnumerable<LaunchboxDbEmulatorPlatform> _defaultEmultorPlatforms;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private LaunchboxDbEmulator _selectedDefaultEmulator;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private LaunchboxDbPlatform _selectedDefaultPlatform;

        [ObservableProperty]
        private bool _emulatorNeedsPath = false;

        [ObservableProperty]
        private IEmulator? _userEmulator;

        [ObservableProperty]
        //[NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private IPlatform? _userPlatform;

        [ObservableProperty]
        private IEmulatorPlatform? _userEmulatorPlatform;

        [ObservableProperty]
        private string _infoMessage;

        [ObservableProperty]
        private InfoBarSeverity _infoSeverity;

        [ObservableProperty]
        private bool _infoBarVisible = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmulatorPlatformPropsSettable))]
        private string _exePath;

        [ObservableProperty]
        private bool? _m3uDiskLoadEnabled = false;

        [ObservableProperty]
        private bool? _autoExtract = false;

        public bool EmulatorPlatformPropsSettable => !(UserPlatform != null && UserEmulator != null && UserEmulatorPlatform != null);


        public AddNewPlatformUcVM()
        {

        }

        partial void OnSelectedDefaultEmulatorChanged(LaunchboxDbEmulator value)
        {
            if (value == null)
            {
                ResolveInfoBar();
                return;
            }

            UserEmulator = PluginHelper.DataManager.GetAllEmulators().Where(e => e.Title.Equals(SelectedDefaultEmulator.Name,
                    StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (UserEmulator == null)
            {
                ExePath = null;
                EmulatorNeedsPath = true;
                AutoExtract = null;
                M3uDiskLoadEnabled = null;
                ResolveInfoBar();
                return;
            }

            ExePath = UserEmulator.ApplicationPath;
            EmulatorNeedsPath = false;

            UserEmulatorPlatform = UserEmulator.GetAllEmulatorPlatforms().
                Where(ep => ep.EmulatorId == UserEmulator.Id && ep.Platform == UserPlatform?.Name && ep.IsDefault == true).FirstOrDefault();

            if (UserEmulatorPlatform != null)
            {
                M3uDiskLoadEnabled = UserEmulatorPlatform.M3uDiscLoadEnabled;
                AutoExtract = UserEmulatorPlatform.AutoExtract == true;
            }
            else
            {
                AutoExtract = UserEmulator.AutoExtract; // get default from parent emulator
                M3uDiskLoadEnabled = false;
            }

            ResolveInfoBar();
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

        partial void OnSelectedDefaultPlatformChanged(LaunchboxDbPlatform value)
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

        public AddNewPlatformUcVM(LaunchboxDataService launchboxDataService)
        {
            _launchboxDataService = launchboxDataService;
        }

        public async Task InitialiseAsync()
        {
            if (DefaultPlatforms != null && DefaultEmulators != null) return;

            DefaultPlatforms = await _launchboxDataService.GetDefaultDbPlatforms();
            DefaultEmulators = await _launchboxDataService.GetDefaultDbEmulators();
            DefaultEmultorPlatforms = await _launchboxDataService.GetDefaultDbEmulatorPlatforms();
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

        public void ClearData()
        {
            SelectedDefaultPlatform = null;
            SelectedDefaultEmulator = null;
            ExePath = null;
            M3uDiskLoadEnabled = null;
            AutoExtract = null;
            ResolveInfoBar();
        }

        private void UpdateInfoBar(string message, InfoBarSeverity infoBarSeverity)
        {
            InfoMessage = message;
            InfoSeverity = infoBarSeverity;

        }



    }
}
