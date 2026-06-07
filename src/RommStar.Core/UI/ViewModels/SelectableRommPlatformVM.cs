using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Dtos;

namespace RommStar.Core.UI.ViewModels
{
    public partial class SelectableRommPlatformVM : ObservableObject
    {
        public RommPlatformDTO Dto { get; }

        [ObservableProperty]
        private bool _isSelected;

        public SelectableRommPlatformVM(RommPlatformDTO dto, bool isSelected = false)
        {
            Dto = dto;
            IsSelected = isSelected;
        }
    }
}