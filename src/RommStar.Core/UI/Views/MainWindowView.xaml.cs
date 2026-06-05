using RommStar.Core.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        private JobsPageView JobsPageView;

        public MainWindowView(MainWindowVM mainWindowVM, HomePageVM homePageVM,
            SettingsPageVM settingsPageVM, JobsPageVM jobsPageVM)
        {
            InitializeComponent();
            ViewModel = mainWindowVM;
            DataContext = ViewModel;

            HomePageView = new HomePageView(homePageVM);
            SettingsPageView = new SettingsPageView(settingsPageVM);
            JobsPageView = new JobsPageView(jobsPageVM);
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
            else if (item == NavigationViewItem_Jobs)
            {
                page = JobsPageView;
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If application is shutting down, allow normal close.
            if (Application.Current?.Dispatcher?.HasShutdownStarted == true ||
                Application.Current?.Dispatcher?.HasShutdownFinished == true)
            {
                return;
            }

            // Cancel the close and hide the window so the singleton ViewModel and bindings remain alive.
            e.Cancel = true;
            this.Hide();
        }

        private void FontIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void DarkModeToggle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!NavigationView_Root.IsPaneOpen)
            {
                NavigationView_Root.IsPaneOpen = true;
            }
        }
    }
}