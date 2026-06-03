using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    internal class DashboardPageVM : BasePageVM
    {
        public DashboardPageVM(LoggingService loggingService) : base(loggingService)
        {
            PageTitle = "Dashboard";
            loggingService.Log("DashboardPageVM initialized.");
        }
    }
}