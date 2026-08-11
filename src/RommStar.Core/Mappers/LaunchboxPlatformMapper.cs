using Riok.Mapperly.Abstractions;
using RommStar.Core.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Mappers
{
    [Mapper(PropertyNameMappingStrategy = PropertyNameMappingStrategy.CaseInsensitive, RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class LaunchboxPlatformMapper
    {
        public LaunchboxPlatformMapper()
        {
            
        }


    }
}
