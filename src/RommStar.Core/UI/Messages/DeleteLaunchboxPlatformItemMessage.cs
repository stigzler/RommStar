using CommunityToolkit.Mvvm.Messaging.Messages;
using RommStar.Core.UI.ViewModels.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.Messages
{
    internal class DeleteLaunchboxPlatformItemMessage : ValueChangedMessage<ViewModels.DataModels.LaunchboxPlatformItemVM>
    {
        public DeleteLaunchboxPlatformItemMessage(LaunchboxPlatformItemVM value) : base(value)
        {
        }
    }
}