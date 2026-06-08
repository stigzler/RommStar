using CommunityToolkit.Mvvm.ComponentModel;
using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.UI.ViewModels.DisplayModels;
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
        private InfoBar
            _infoBar;

        public RommServerItemVM(RommServer rommServer)
        {
            RommServer = rommServer;
        }

        public RommServerItemVM(RommServer rommServer, List<RommPlatformDTO> serverPlatformDTOs, InfoBar infoBar)
        {
            RommServer = rommServer;
            ServerPlatformDTOs = serverPlatformDTOs;
            InfoBar = infoBar;
        }

        public override string ToString()
        {
            return RommServer.ServerName;
        }
    }
}