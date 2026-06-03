using RommStar.Core.Services;
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
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace RommStar.Core.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindowView.xaml
    /// </summary>
    public partial class MainWindowView : FluentWindow
    {
        public MainWindowVM ViewModel { get; }

        public MainWindowView(MainWindowVM viewModel, INavigationService navigationService, INavigationViewPageProvider navigationViewPageProvider)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            // Attach the service to the NavigationView
            navigationService.SetNavigationControl(RootNavigationView);

            // You can also set the page service, which is required for some functionalities
            RootNavigationView.SetPageProviderService(navigationViewPageProvider);

            // Trigger initial navigation so the BreadcrumbBar gets populated
            Loaded += (_, _) => RootNavigationView.Navigate(typeof(DashboardPageView));
        }

        private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void FluentWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Properties.Settings.Default.WindowMainSize =
                new System.Drawing.Size((int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }
}