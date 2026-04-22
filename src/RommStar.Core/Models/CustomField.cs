using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommBox.Core.Models.Launchbox
{
    internal class CustomField : ICustomField
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public CustomField(string gameId, string name, string value)
        {
            GameId = gameId;
            Name = name;
            Value = value;
        }

        public CustomField()
        {
        }
    }
}