using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.Messages
{
    internal class RomSeverListChangedMessage : ValueChangedMessage<bool>
    {
        public RomSeverListChangedMessage(bool value) : base(value)
        {
        }
    }
}
