using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    public class LaunchboxDbEmulator
    {
        public string Name { get; set; }
        public string? CommandLine { get; set; }

        public string? ApplicableFileExtensions { get; set; }

        public string? URL { get; set; }

        public string? BinaryFilename { get; set; }

        public bool NoQuotes { get; set; }

        public bool NoSpace { get; set; }

        public bool HideConsole { get; set; }

        public bool FileNameOnly { get; set; }

        public bool AutoExtract { get; set; }
    }
}
