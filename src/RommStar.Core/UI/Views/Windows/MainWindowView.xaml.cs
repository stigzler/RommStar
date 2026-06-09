using RommStar.Core.UI.ViewModels;
using RommStar.Core.UI.ViewModels.Pages;
using RommStar.Core.UI.ViewModels.Windows;
using RommStar.Core.UI.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RommStar.Core.UI.Views.Windows
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
        private ServersPageView ServersPageView;
        private PlatformsPageView PlatformsPageView;

        public MainWindowView(MainWindowVM mainWindowVM, HomePageVM homePageVM,
            SettingsPageVM settingsPageVM, JobsPageVM jobsPageVM, ServersPageVM serversPageVM,
            PlatformsPageVM platformsPageVM)
        {
            InitializeComponent();
            ViewModel = mainWindowVM;
            DataContext = ViewModel;

            HomePageView = new HomePageView(homePageVM);
            SettingsPageView = new SettingsPageView(settingsPageVM);
            JobsPageView = new JobsPageView(jobsPageVM);
            ServersPageView = new ServersPageView(serversPageVM);
            PlatformsPageView = new PlatformsPageView(platformsPageVM);
        }

        private void NavigationView_SelectionChanged(iNKORE.UI.WPF.Modern.Controls.NavigationView sender, iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            // --- ANY PAGE TRANSITION TASKS ---
            if (Frame_Main.Content is ServersPageView oldServersPage)
            {
                // Safely extract its ViewModel context and execute the data save
                if (oldServersPage.DataContext is ServersPageVM serversVm)
                {
                    serversVm.OnNavigatedAway();
                }
            }

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
            else if (item == NavigationViewItem_Servers)
            {
                page = ServersPageView;
            }
            else if (item == NavigationViewItem_Platforms)
            {
                page = PlatformsPageView;
            }

            if (page != null)
            {
                NavigationView_Root.Header = page.Title;
                Frame_Main.Navigate(page);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView_Root.SelectedItem = NavigationViewItem_Platforms;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.WindowSize = new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.Save();

            // ENSURE RELEVANT SETINGS SAVES
            // In case of launchbox shutdown etc.
            if (Frame_Main.Content is ServersPageView activeServersPage)
            {
                if (activeServersPage.DataContext is ServersPageVM serversVm)
                {
                    serversVm.OnNavigatedAway();
                }
            }

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