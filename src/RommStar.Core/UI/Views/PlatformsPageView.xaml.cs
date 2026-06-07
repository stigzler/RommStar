using RommStar.Core.Dtos;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        /// <summary>
        /// Captures selection events inside the dynamic DropDownButton's Flyout menu frame,
        /// appending items straight to the VM token list before resetting selection states cleanly.
        /// </summary>
        private void AddPlatformListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is RommPlatformDTO chosenPlatform)
            {
                if (DataContext is PlatformsPageVM viewModel)
                {
                    viewModel.AddMappedPlatformToSelectedRow(chosenPlatform);
                }

                // Instantly clear selection state. This forces placeholder compliance and eliminates flickering visual bugs
                listView.SelectedItem = null;
            }
        }

        /// <summary>
        /// Triggers when the '✕' close glyph on a capsule token is clicked by the user.
        /// </summary>
        private void RemovePlatformButton_Click(object sender, System.Windows.RoutedEventArgs e)
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