using iNKORE.UI.WPF.Modern;
using RommStar.Core.UI.ViewModels.Pages;
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

namespace RommStar.Core.UI.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPageView.xaml
    /// </summary>
    ///

    public partial class SettingsPageView : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private SettingsPageVM ViewModel;

        public SettingsPageView(SettingsPageVM viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ViewModel.OnPageVisibilityChanged((bool)e.NewValue);
        }

  
    }
}