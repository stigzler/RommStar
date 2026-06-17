using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public enum MetadataSyncAction
    {
        None, 
        Insert, 
        Update, 
        DeleteAndInsert
    }
}
