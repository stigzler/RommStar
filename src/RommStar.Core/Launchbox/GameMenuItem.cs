using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Launchbox
{
    internal class GameMenuItem : IGameMenuItem
    {
        private string _caption;
        private IEnumerable<IGameMenuItem> _children = new List<IGameMenuItem>();
        private bool _enabled;
        private Image _icon;
        public string Caption { get => _caption; set => _caption = value; }

        public IEnumerable<IGameMenuItem> Children { get => _children; set => _children = value.ToList(); }

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public Image Icon { get => _icon; set => _icon = value; }

        public void OnSelect(params IGame[] games)
        {
            //throw new NotImplementedException();
        }
    }
}