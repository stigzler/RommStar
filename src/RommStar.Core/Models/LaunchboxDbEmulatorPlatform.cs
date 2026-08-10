using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    public class LaunchboxDbEmulatorPlatform
    {
        public string Emultor { get; set; }

        public string Platform { get; set; }

        public string? CommandLine { get; set; }

        public string? ApplicationException { get; set; }

        public bool Recommended { get; set; } = false;

        public string? RequiredBiosFile { get; set; }

    }
}
