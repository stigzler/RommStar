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
    /// Interaction logic for AboutPageView.xaml
    /// </summary>
    public partial class AboutPageView : iNKORE.UI.WPF.Modern.Controls.Page
    {
        public AboutPageView()
        {
            InitializeComponent();

            VersionTBL.Text = $"Version: {typeof(PluginHost).Assembly.GetName().Version.ToString()}";


        }
    }
}
