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

    public partial class LaunchboxLocalDatabaseMapper
    {
        public LaunchboxLocalDatabaseMapper()
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


        [MapProperty(nameof(launchboxDbPlatformDTO.Name), nameof(iPlatform.Name))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Category), nameof(iPlatform.Category))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Cpu), nameof(iPlatform.Cpu))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Developer), nameof(iPlatform.Developer))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Display), nameof(iPlatform.Display))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Emulated), nameof(iPlatform.IsEmulated))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Graphics), nameof(iPlatform.Graphics))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Manufacturer), nameof(iPlatform.Manufacturer))]
        [MapProperty(nameof(launchboxDbPlatformDTO.MaxControllers), nameof(iPlatform.MaxControllers))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Media), nameof(iPlatform.Media))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Memory), nameof(iPlatform.Memory))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Notes), nameof(iPlatform.Notes))]
        [MapProperty(nameof(launchboxDbPlatformDTO.ReleaseDate), nameof(iPlatform.ReleaseDate))]
        [MapProperty(nameof(launchboxDbPlatformDTO.Sound), nameof(iPlatform.Sound))]
        public partial void PlatformDtoToIPlatform(LaunchboxDbPlatformDTO launchboxDbPlatformDTO, IPlatform iPlatform);


    }

}

