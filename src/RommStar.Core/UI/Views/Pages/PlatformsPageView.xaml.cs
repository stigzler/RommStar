using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Dtos;
using RommStar.Core.UI.ViewModels;
using RommStar.Core.UI.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;

namespace RommStar.Core.UI.Views.Pages
{
    /// <summary>
    /// Interaction logic for PlatformsPageView.xaml
    /// </summary>
    public partial class PlatformsPageView : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private PlatformsPageVM ViewModel;

        public PlatformsPageView(PlatformsPageVM platformsPageVM)
        {
            InitializeComponent();
            ViewModel = platformsPageVM;
            DataContext = ViewModel;
        }

        private async void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            await ViewModel.OnPageVisibilityChanged((bool)e.NewValue);
        }

        private async void RommPlatformSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ViewModel.PlatformSearchText = RommPlatformSearch.Text;
        }

        private void RommPlatformSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (String.IsNullOrEmpty(RommPlatformSearch.Text)) ViewModel.PlatformSearchText = "";
        }
    }
}