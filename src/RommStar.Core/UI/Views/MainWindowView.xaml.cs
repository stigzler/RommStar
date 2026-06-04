using RommStar.Core.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RommStar.Core.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindowView.xaml
    /// </summary>
    public partial class MainWindowView : Window
    {
        private MainWindowVM ViewModel;

        private HomePageView HomePageView;
        private SettingsPageView SettingsPageView;

        public MainWindowView(MainWindowVM mainWindowVM, HomePageVM homePageVM, SettingsPageVM settingsPageVM)
        {
            InitializeComponent();
            ViewModel = mainWindowVM;
            DataContext = ViewModel;

            HomePageView = new HomePageView(homePageVM);
            SettingsPageView = new SettingsPageView(settingsPageVM);
        }

        private void NavigationView_SelectionChanged(iNKORE.UI.WPF.Modern.Controls.NavigationView sender, iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            var item = sender.SelectedItem;
            Page? page = null;

            if (item == NavigationViewItem_Home)
            {
                page = HomePageView;
            }
            else if (item == NavigationViewItem_Settings)
            {
                page = SettingsPageView;
            }

            if (page != null)
            {
                NavigationView_Root.Header = page.Title;
                Frame_Main.Navigate(page);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView_Root.SelectedItem = NavigationViewItem_Home;
        }
    }
}