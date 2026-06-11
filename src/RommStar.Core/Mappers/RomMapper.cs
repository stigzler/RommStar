using Riok.Mapperly.Abstractions;
using RommStar.Core.Dtos.Romm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Mappers
{
    [Mapper]
    public partial class RomMapper
    {
        [MapProperty(nameof(romDto.Name), nameof(iGame.Title))]
        [MapProperty(nameof(romDto.Summary), nameof(iGame.Notes))]

        public partial void RommRomDtoToIGame(RomDTO romDto, IGame iGame);

        private IGame game = PluginHelper.DataManager.AddNewGame("1942");
    }
}