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
    /// Interaction logic for HomePageView.xaml
    /// </summary>
    public partial class HomePageView : Page
    {
        private HomePageVM ViewModel;

        public HomePageView(HomePageVM viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
    }
}