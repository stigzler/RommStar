using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Dtos;
using RommStar.Core.UI.ViewModels;
using RommStar.Core.UI.ViewModels.Pages;
using RommStar.Core.UI.Views.Dialogs;
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

        private void PlatformsPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PlatformsPageVM vm)
            {
                // Hook the event handler to intercept dialog open requests
                vm.RequestAddPlatformNameDialog += OnRequestAddPlatformNameAsync;
                vm.RequestConfirmationDialog += OnRequestConfirmationDialogAsync;
            }
        }

        private async Task<bool> OnRequestConfirmationDialogAsync(string title, string message)
        {
            var dialog = new ConfrimYesCancelContextDialog(title, message);

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task<string> OnRequestAddPlatformNameAsync()
        {
            // Instantiate your custom ContentDialog object

            var dialog = new SingleTextBoxContentDialog("Name for your new platform");

            // iNKORE's built-in engine overlays this on top of the Window automatically
            ContentDialogResult result = await dialog.ShowAsync();

            // If the user clicked the Primary Action ("Yes" / "Save")
            if (result == ContentDialogResult.Primary)
            {
                // Return the text typed into the custom dialog's TextBox
                return dialog.Text;
            }

            // Return empty if they closed or canceled
            return string.Empty;
        }
    }
}