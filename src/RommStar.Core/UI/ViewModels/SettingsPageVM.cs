using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public class SettingsPageVM : BasePageVM
    {
        public SettingsPageVM(LoggingService loggingService) : base(loggingService)
        {
            PageTitle = "Settings";
            loggingService.Log("SettingsPageVM initialized.");
        }
    }
}