using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{

    public class MediaDownloadItem
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string TargetLocalPath { get; set; } = string.Empty;
        public MediaType MediaType { get; set; }  // e.g., "BoxFront", "Video", "Logo"
    }
}
