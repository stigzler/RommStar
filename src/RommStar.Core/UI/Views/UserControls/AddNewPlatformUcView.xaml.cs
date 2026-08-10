using RommStar.Core.UI.ViewModels.UserControls;
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

namespace RommStar.Core.UI.Views.UserControls
{
    /// <summary>
    /// Interaction logic for AddNewPlatformUcView.xaml
    /// </summary>
    public partial class AddNewPlatformUcView : UserControl
    {
        public AddNewPlatformUcVM ViewModel;

        public AddNewPlatformUcView(AddNewPlatformUcVM addNewPlatformUcVM)
        {
            InitializeComponent();

            ViewModel = addNewPlatformUcVM;

            DataContext = ViewModel;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddNewPlatformUcVM vm)
            {
                try
                {
                    await vm.InitialiseAsync();
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}
