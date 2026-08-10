using RommStar.Core.UI.ViewModels.Pages;

namespace RommStar.Core.UI.Views.Pages
{
    /// <summary>
    /// Interaction logic for JobsPageView.xaml
    /// </summary>
    public partial class JobsPageView : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private JobsPageVM ViewModel;

        public JobsPageView(JobsPageVM viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
    }
}