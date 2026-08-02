using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public record MetadataSyncState(
        bool? HasMathcingLaunchboxId,
        bool? HasMatchingRommId,
        bool? HasMatchingServerId,
        bool? IsMultiFile = false
        );
}
