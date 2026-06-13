using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    internal class MetadataSyncDecisionEngine
    {
        public static MetadataSyncAction DetermineAction(MetadataSyncState s, bool OverwriteMetadata,
             bool DeleteOldServerRoms) => s switch
        {
            // F, F, T -> Insert
            {  HasMathcingLaunchboxId: false, HasMatchingRommId: false, HasMatchingServerId: true } 
                => MetadataSyncAction.Insert,

            // F, F, F -> Insert
            { HasMathcingLaunchboxId: false, HasMatchingRommId: false, HasMatchingServerId: false } 
                => MetadataSyncAction.Insert,

            // F, T, T -> ?Update (Upsert if Overwrite && !Protect)
            { HasMathcingLaunchboxId: false, HasMatchingRommId: true, HasMatchingServerId: true }
                => (OverwriteMetadata) ? MetadataSyncAction.Update : MetadataSyncAction.None,

            // F, T, F -> ?Delete + Insert (If DeleteOldServerRoms)
            { HasMathcingLaunchboxId: false, HasMatchingRommId: true, HasMatchingServerId: false }
                => DeleteOldServerRoms ? MetadataSyncAction.DeleteAndInsert : MetadataSyncAction.None,

            // T, F, F -> ?Update (Upsert if Overwrite && !Protect)
            { HasMathcingLaunchboxId: true, HasMatchingRommId: false, HasMatchingServerId: false }
                => (OverwriteMetadata) ? MetadataSyncAction.Update : MetadataSyncAction.None,

            // T, F, T -> Update (Unconditional)
            { HasMathcingLaunchboxId: true, HasMatchingRommId: false, HasMatchingServerId: true } => MetadataSyncAction.Update,

            // T, T, T -> ?Update (Upsert if Overwrite && !Protect)
            { HasMathcingLaunchboxId: true, HasMatchingRommId: true, HasMatchingServerId: true }
                => (OverwriteMetadata) ? MetadataSyncAction.Update : MetadataSyncAction.None,

            // T, T, F -> ?Delete + Insert
            { HasMathcingLaunchboxId: true, HasMatchingRommId: true, HasMatchingServerId: false }
                => DeleteOldServerRoms ? MetadataSyncAction.DeleteAndInsert : MetadataSyncAction.None,

            // Handle Null cases (representing final two rows)
            { HasMathcingLaunchboxId: true, HasMatchingRommId: null, HasMatchingServerId: null }
                => (OverwriteMetadata) ? MetadataSyncAction.Update : MetadataSyncAction.None,

            { HasMathcingLaunchboxId: false, HasMatchingRommId: null, HasMatchingServerId: null }
                => MetadataSyncAction.Insert,

            _ => MetadataSyncAction.None
        };
    }
}
