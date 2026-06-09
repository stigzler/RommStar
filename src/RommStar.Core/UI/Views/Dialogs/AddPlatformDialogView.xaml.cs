using iNKORE.UI.WPF.Modern.Controls;
using System.Windows;
using System.Windows.Media;

namespace RommStar.Core.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for AddPlatformDialogView.xaml
    /// </summary>
    public partial class AddPlatformDialogView : ContentDialog
    {
        public AddPlatformDialogView()
        {
            InitializeComponent();
        }

        // Add this override to protect the plugin from host layout clashes
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Safely locate the visual root of the iNKORE dialog template
            if (VisualTreeHelper.GetChildrenCount(this) > 0)
            {
                if (VisualTreeHelper.GetChild(this, 0) is FrameworkElement templateRoot)
                {
                    // Clear the visual state groups to disable the conflicting ColumnDefinition animations
                    var visualStateGroups = VisualStateManager.GetVisualStateGroups(templateRoot);
                    visualStateGroups?.Clear();
                }
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
        }
    }
}