using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class BasePageVM : ObservableObject
    {
        private LoggingService? loggingService;

        [ObservableProperty]
        private string? pageTitle;

        public BasePageVM(LoggingService loggingService)
        {
            this.loggingService = loggingService;
        }
    }
}