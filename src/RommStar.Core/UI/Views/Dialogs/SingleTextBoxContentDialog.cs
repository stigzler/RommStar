using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RommStar.Core.UI.Views.Dialogs
{
    /// <summary>
    /// Shonky implementation of this control in the absence of any kind of documentation!
    /// See PlatformsPageVM.RequestAddPlatformNameDialog and PlatformsPageView.xaml.cs for implementation
    /// </summary>
    public class SingleTextBoxContentDialog : ContentDialog
    {
        private TextBox _textBox = new TextBox();

        public string Text { get => _textBox.Text; }

        public SingleTextBoxContentDialog(string title)
        {
            PrimaryButtonText = "OK";
            SecondaryButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            Title = title;
            Content = _textBox;
        }
    }
}