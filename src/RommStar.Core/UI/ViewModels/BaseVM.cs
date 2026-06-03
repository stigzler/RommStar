using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    internal partial class BaseVM : ObservableObject
    {
        private LoggingService? loggingService;

        public BaseVM(LoggingService loggingService)
        {
            this.loggingService = loggingService;
        }
    }
}