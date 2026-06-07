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
            PlatformPickerPopup.DataContext = ViewModel;
        }

        /// <summary>
        /// Clears stale selection state each time the popup opens so previously
        /// selected items from another platform don't visually carry over.
        /// </summary>
        private void PlatformPickerPopup_Opened(object sender, EventArgs e)
        {
            PlatformPickerListView.SelectedItems.Clear();
        }

        /// <summary>
        /// Fires per-click inside the multi-select platform picker flyout.
        /// Iterates AddedItems so every item ticked in a single interaction is captured.
        /// </summary>
        private void PlatformPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not PlatformsPageVM viewModel) return;

            foreach (RommPlatformDTO platform in e.AddedItems.OfType<RommPlatformDTO>())
            {
                viewModel.AddMappedPlatformToSelectedRow(platform);
            }
        }

        /// <summary>
        /// Fires when the ✕ close glyph on a capsule token is clicked.
        /// The Button's DataContext is the RommPlatformDTO set by the ItemTemplate.
        /// </summary>
        private void RemovePlatformButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is RommPlatformDTO targetPlatform)
            {
                if (DataContext is PlatformsPageVM viewModel)
                {
                    viewModel.RemoveMappedPlatformFromSelectedRow(targetPlatform);
                }
            }
        }
    }
}