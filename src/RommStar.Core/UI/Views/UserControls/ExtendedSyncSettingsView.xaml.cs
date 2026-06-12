using RommStar.Core.Models;
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
    /// Interaction logic for ExtendedSyncSettingsView.xaml
    /// </summary>
    public partial class ExtendedSyncSettingsView : UserControl
    {


        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ExtendedSyncSettingsProperty =
            DependencyProperty.Register(nameof(ExtendedSyncSettings),
                typeof(ExtendedSyncSettings), 
                typeof(ExtendedSyncSettingsView),
                new PropertyMetadata(default(ExtendedSyncSettings)));
        public ExtendedSyncSettings ExtendedSyncSettings {
            get { return (ExtendedSyncSettings)GetValue(ExtendedSyncSettingsProperty); }
            set { SetValue(ExtendedSyncSettingsProperty, value); }
        }

        public ExtendedSyncSettingsView()
        {
            InitializeComponent();
        }
    }
}
