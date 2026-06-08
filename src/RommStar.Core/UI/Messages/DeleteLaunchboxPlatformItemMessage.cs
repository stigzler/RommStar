using CommunityToolkit.Mvvm.Messaging.Messages;
using RommStar.Core.UI.ViewModels.DataItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.Messages
{
    internal class DeleteLaunchboxPlatformItemMessage : ValueChangedMessage<ViewModels.DataItems.LaunchboxPlatformItemVM>
    {
        public DeleteLaunchboxPlatformItemMessage(LaunchboxPlatformItemVM value) : base(value)
        {
        }
    }
}