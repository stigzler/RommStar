using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Extensions
{
    public static  class IEmulatorExtension
    {
        public static string ToCsv(this IEmulator emulator)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"[{emulator.Title}], ");
            sb.Append($"Default Platform: [{emulator.DefaultPlatform}], ");

            sb.Append($"Auto extract: {emulator.AutoExtract}, ");
            sb.Append($"No quotes: {emulator.NoQuotes}, ");
            sb.Append($"No Space: {emulator.NoSpace}, ");
            sb.Append($"Bare filename: {emulator.FileNameWithoutExtensionAndPath}, ");
            sb.Append($"Command Line: [{emulator.CommandLine}], ");

            sb.Append($"Exe Path: [{emulator.ApplicationPath}]");

            return sb.ToString();

        }
    }
}
