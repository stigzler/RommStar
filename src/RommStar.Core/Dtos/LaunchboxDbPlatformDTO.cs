using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Dtos
{
    public class LaunchboxDbPlatformDTO
    {
        public int PlatformKey { get; set; }

        public string Name { get; set; }

        public bool Emulated { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public string? Developer { get; set; }

        public string? Manufacturer { get; set; }

        public string? Cpu { get; set; }

        public string? Memory { get; set; }

        public string? Graphics { get; set; }

        public string? Sound { get; set; }

        public string? Display { get; set; }

        public string? Media { get; set; }

        public string? MaxControllers { get; set; }

        public string? Notes { get; set; }

        public string? Category { get; set; }

        public bool UseMameFiles { get; set; } = false;

        public override string ToString()
        {
            return Name;
        }

    }
}
