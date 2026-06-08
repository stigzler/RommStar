using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.DataModels
{
    public partial class RommServerItemVM : ObservableObject
    {
        [ObservableProperty]
        private RommServer
            _rommServer;

        [ObservableProperty]
        private List<RommPlatformDTO>
            _serverPlatformDTOs = new List<RommPlatformDTO>();

        [ObservableProperty]
        private bool
            _isErrored = false;

        public RommServerItemVM()
        {
        }
    }
}