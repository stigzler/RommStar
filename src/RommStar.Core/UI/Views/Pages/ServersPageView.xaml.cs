using RommStar.Core.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for ServersPageView.xaml
    /// </summary>
    public partial class ServersPageView : Page
    {
        private ServersPageVM ViewModel;

        public ServersPageView(ServersPageVM viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void PasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            //HACK: To Correct for WPF squiffy/dogmatic PasswordBox behaviours (clears when navigate away - need to restore it when reloads page)
            PasswordBox passwordBox = (PasswordBox)sender;
            passwordBox.Password = ((ServerDisplayItemVM)passwordBox.DataContext).Server.ApiToken;
        }
    }
}