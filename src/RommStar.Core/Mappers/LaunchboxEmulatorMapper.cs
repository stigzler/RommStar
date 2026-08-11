using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Riok.Mapperly.Abstractions;
using RommStar.Core.Dtos;
using Unbroken.LaunchBox.Plugins.Data;


namespace RommStar.Core.Mappers
{
    [Mapper(PropertyNameMappingStrategy = PropertyNameMappingStrategy.CaseInsensitive,   RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public partial class LaunchboxEmulatorMapper
    {
        public LaunchboxEmulatorMapper()
        {
            
        }

        [MapProperty(nameof(launchboxDbEmulatorDTO.Name), nameof(iEmulator.Title))]
        [MapProperty(nameof(launchboxDbEmulatorDTO.AutoExtract), nameof(iEmulator.AutoExtract))]
        [MapProperty(nameof(launchboxDbEmulatorDTO.CommandLine), nameof(iEmulator.CommandLine))]
        [MapProperty(nameof(launchboxDbEmulatorDTO.FileNameOnly), nameof(iEmulator.FileNameWithoutExtensionAndPath))] // not sure about this one
        [MapProperty(nameof(launchboxDbEmulatorDTO.HideConsole), nameof(iEmulator.HideConsole))]
        [MapProperty(nameof(launchboxDbEmulatorDTO.NoQuotes), nameof(iEmulator.NoQuotes))]
        [MapProperty(nameof(launchboxDbEmulatorDTO.NoSpace), nameof(iEmulator.NoSpace))]
        public partial void EmulatorDtoToIEmulator(LaunchboxDbEmulatorDTO launchboxDbEmulatorDTO, IEmulator iEmulator);




    }
}
