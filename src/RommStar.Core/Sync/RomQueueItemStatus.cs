using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public enum RomQueueItemStatus
    {
        Queued,
        Downloading,
        DownloadFailed,
        Downloaded,
        Unzipping,
        UpdatingLaunchbox,
        Complete,
        CompleteWithWarnings,
        Errored
    }
}
