using RommStar.Core.Dtos;
using RommStar.Core.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RommStar.Core.UI.Views
{
    /// <summary>
    /// Interaction logic for PlatformsPageView.xaml
    /// </summary>
    public partial class PlatformsPageView : Page
    {
        private PlatformsPageVM ViewModel;

        public PlatformsPageView(PlatformsPageVM platformsPageVM)
        {
            InitializeComponent();
            ViewModel = platformsPageVM;
            DataContext = ViewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadPlatformsAndPersistedData();
        }
    }
}