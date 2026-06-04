namespace RommStar.Core.Models
{
    /// <summary>
    /// Dictates which asset streams are pulled during a specific runtime operation.
    /// </summary>
    public class MediaSelectionProfile
    {
        public bool BoxFront { get; set; }
        public bool Box3D { get; set; }
        public bool Screenshots { get; set; }
        public bool Manuals { get; set; }
        public bool Videos { get; set; }
        public bool Music { get; set; }
    }
}
