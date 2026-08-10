using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.UI.ViewModels.Pages;
using System.Threading.Tasks;
using System.Windows;

namespace RommStar.Core.UI.Views.Pages
{
    public partial class RomQueuePageView : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private RomQueuePageVM ViewModel;

        public RomQueuePageView(RomQueuePageVM viewModel)
        {
            InitializeComponent();

            ViewModel = viewModel;
            DataContext = ViewModel;

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RomQueuePageVM oldVm)
            {
                oldVm.RequestConfirmationDialog -= ShowConfirmationDialogAsync;
            }
            if (e.NewValue is RomQueuePageVM newVm)
            {
                newVm.RequestConfirmationDialog += ShowConfirmationDialogAsync;
            }
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "Yes, Clear It",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}