using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RommStar.Core.UI.Views.Dialogs
{
    internal class ConfrimYesCancelContextDialog : ContentDialog
    {
        private TextBlock _textBlock = new TextBlock();

        public ConfrimYesCancelContextDialog(string title, string message)
        {
            PrimaryButtonText = "Yes";
            SecondaryButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            Title = title;
            _textBlock.Text = message;
            Content = _textBlock;
        }
    }
}