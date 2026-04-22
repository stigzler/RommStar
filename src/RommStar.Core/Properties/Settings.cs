using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Properties
{
    public static class Settings
    {
        private static UserSettings _default = SettingsManager.Load();
        public static UserSettings Default => _default;

        public static void Save() => SettingsManager.Save(_default);
    }
}