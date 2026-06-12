using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Launchbox
{
    public class GameIdMap : IEquatable<GameIdMap>
    {
        public string LocalId { get; set; } // should never be null
        public int? DatabaseId { get; set; }
        public int RommId { get; set; }

        public GameIdMap(string localId)
        {
            LocalId = localId;
        }

        public GameIdMap(string localId, int? databaseId)
        {
            LocalId = localId;
            DatabaseId = databaseId;
        }

        public bool Equals(GameIdMap other)
        {
            return DatabaseId == other.DatabaseId && LocalId == other.LocalId;
        }
    }
}
