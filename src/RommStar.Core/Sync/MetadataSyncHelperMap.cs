using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public class MetadataSyncHelperMap : IEquatable<MetadataSyncHelperMap>
    {
        /// <summary>
        /// The local game id. eg. 12d2010c-7e6d-4e12-a454-778886aff65b
        /// Should never be null
        /// </summary>
        public string LocalId { get; set; }

        /// <summary>
        /// The local database Id form the db3 file (NOT the online games database Id)
        /// eg. 4893 = BallBlazer
        /// </summary>

        public int? LbDatabaseId { get; set; }

        /// <summary>
        /// The Romm RomId (id local to romm server, not canon) for the rom (essentially game)
        /// </summary>
        public string? RommIds { get; set; }

        /// <summary>
        /// The Romm Server Id as set by RommStar
        /// </summary>
        public string? RommServerId { get; set; }

        /// <summary>
        /// Helper for debugging purposes
        /// </summary>
        public string GameName { get; set; }

        /// <summary>
        /// Used in adding/testing IGame custom fields
        /// </summary>
        public bool ProtectMetadata { get; set; } = false;

        public MetadataSyncHelperMap(string localId)
        {
            LocalId = localId;
        }

        public MetadataSyncHelperMap(string localId, int? databaseId)
        {
            LocalId = localId;
            LbDatabaseId = databaseId;
        }

        public bool Equals(MetadataSyncHelperMap other)
        {
            return LbDatabaseId == other.LbDatabaseId && LocalId == other.LocalId;
        }

        /// <summary>
        /// Checks if a specific RomM ID exists inside the comma-separated RommIds string.
        /// </summary>
        public bool ContainsId(int id)
        {
            if (string.IsNullOrEmpty(RommIds)) return false;

            string idStr = id.ToString();
            string[] splitIds = RommIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < splitIds.Length; i++)
            {
                if (splitIds[i].Trim() == idStr) return true;
            }
            return false;
        }
    }
}
