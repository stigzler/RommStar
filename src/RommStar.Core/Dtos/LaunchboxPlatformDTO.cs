using RommStar.Core.UI.ViewModels.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.Dtos
{
    public class LaunchboxPlatformDTO
    {
        public string Name { get; set; }
        public string NestedName { get; set; }
        public string SortTitle { get; set; }
        public string SortTitleOrTitle { get; set; }
        public string ScrapeAs { get; set; }

        public LaunchboxPlatformItemVM ToLaunchboxPlatformItem()
        {
            return new LaunchboxPlatformItemVM() { LaunchboxPlatformName = Name };
        }
    }
}