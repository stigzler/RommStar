using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Launchbox
{
    public class AdditionalApplicationDetails
    {
        public int? DiscNumber { get; set; }
        public bool IsSideA { get; set; } = false;
        public bool IsSideB { get; set; } = false;

        public string? Version { get; set; }

        public string? Region { get; set; }

        public override string ToString()
        {
            return $"Disk: {DiscNumber}\r\nSide A: {IsSideA}\r\nSide B: {IsSideB}\r\nVersion: {Version}\r\nRegion: {Region}";
        }
    }
}
