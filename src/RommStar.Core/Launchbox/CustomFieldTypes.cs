using RommStar.Core.CustomAttributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Launchbox
{
    internal enum CustomFieldTypes
    {
        [CustomName("Romm_RommId")]
        Romm_RomId,

        [CustomName("Romm_ServerId")]
        Romm_ServerId,

        [CustomName("Romm_ProtectMetadata")]
        Romm_ProtectMetadata,
    }
}
