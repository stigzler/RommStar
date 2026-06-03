using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using RommStar.Core.UI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wpf.Ui.Controls;

namespace RommStar.Core.UI.ViewModels
{
    public partial class MainWindowVM : ObservableObject
    {
        private LoggingService? loggingService;

        [ObservableProperty]
        private ICollection<object> _menuItems = new ObservableCollection<object>();

        [ObservableProperty]
        private ICollection<object> _footerMenuItems = new ObservableCollection<object>();

        public MainWindowVM()
        {
            SetupMenuItems();
        }

        public MainWindowVM(LoggingService loggingService)
        {
            this.loggingService = loggingService;

            SetupMenuItems();

            loggingService.Log("MainWindowVM initialized.");
        }

        private void SetupMenuItems()
        {
            MenuItems = new ObservableCollection<object>
                {
                    new NavigationViewItem("Home", SymbolRegular.Home24, typeof(DashboardPageView))
                    { TargetPageTag = "Dashboard" },
                };
            FooterMenuItems = new ObservableCollection<object>
                {
                    new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(SettingsPageView))
                    { TargetPageTag = "Settings" },
                };
        }
    }
}