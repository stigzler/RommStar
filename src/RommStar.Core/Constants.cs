using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core
{
    internal static class Constants
    {
        internal static readonly string LaunchboxRootDir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName ?? AppContext.BaseDirectory;

        internal const string MediaPacksPlatformIconsRelPath = @"Images\Media Packs\Platform Icons";
    }
}