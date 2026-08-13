using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Extensions
{
    public static class IPlatformExtensions
    {
        public static string ToCsv(this IPlatform platform)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"[{platform.Name}], ");
            sb.Append($"ScrapeAs: [{platform.ScrapeAs}], ");
            sb.Append($"Folder: [{platform.Folder}]");
            return sb.ToString();
        }
    }
}
