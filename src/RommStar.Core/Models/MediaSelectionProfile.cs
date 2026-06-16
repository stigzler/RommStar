namespace RommStar.Core.Models
{
    /// <summary>
    /// Dictates which asset streams are pulled during a specific runtime operation.
    /// </summary>
    public class MediaSelectionProfile
    {
        public bool Bezel { get; set; }
        public bool Box3D { get; set; }
        public bool BoxBack { get; set; }
        public bool BoxFront { get; set; } = true;
        public bool FanArt { get; set; }
        public bool Logo { get; set; }
        public bool Manual { get; set; }
        public bool Marquee { get; set; }
        public bool MixImage { get; set; }
        public bool Music { get; set; }
        public bool PhysicalMedia { get; set; }
        public bool Screenshot { get; set; }
        public bool TitleScreen { get; set; }       
        public bool Video { get; set; }
    }
}
