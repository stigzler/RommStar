using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.Views.UserControls
{
    public partial class MediaSelectionItemViewModel : ObservableObject
    {
        public MediaType Type { get; }
        public string DisplayName { get; }

        [ObservableProperty]
        private bool _isSelected;

        public MediaSelectionItemViewModel(MediaType type, bool isInitialValueSelected)
        {
            Type = type;
            IsSelected = isInitialValueSelected;

            // Turns "BoxFront" into "Box Front" or "PhysicalMedia" into "Physical Media" for the CheckBox UI
            DisplayName = System.Text.RegularExpressions.Regex.Replace(type.ToString(), "([a-z])([A-Z])", "$1 $2");
        }
    }
}
