using CommunityToolkit.Mvvm.ComponentModel;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.DisplayModels
{
    public partial class InfoBarVM : ObservableObject
    {
        [ObservableProperty]
        private InfoBarSeverity
            _severity = InfoBarSeverity.Informational;

        [ObservableProperty]
        private string
            _title = "{default}";

        [ObservableProperty]
        private string
            _message = "{default}";

        [ObservableProperty]
        private bool
            _isOpen = false;

        [ObservableProperty]
        private bool
            _isClosable = true;

        public InfoBarVM(InfoBarSeverity severity, string title, string message)
        {
            Severity = severity;
            Title = title;
            Message = message;
        }

        public InfoBarVM(InfoBarSeverity severity, string title, string message, bool isOpen, bool isClosable)
          : this(severity, title, message)
        {
            IsOpen = isOpen;
            IsClosable = isClosable;
        }
    }
}