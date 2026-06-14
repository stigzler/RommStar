using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Launchbox
{
    [Browsable(false)]
    internal class RomIdCustomField : ICustomField
    {
        private string _gameId;
        private string _name;
        private string _value;

        public string GameId { get => _gameId; set => _gameId = value; }
        public string Name { get => _name; set => _name=value; }
        public string Value { get => _value; set => _value=value; }
    }
}
