namespace RommStar.Core.Models
{
    /// <summary>
    /// Dictates which asset streams are pulled during a specific runtime operation.
    /// </summary>
    /// <summary>
    /// Dictates which asset streams are pulled during a specific runtime operation.
    /// </summary>
    public class MediaSelectionProfile
    {
        /// <summary>
        /// A collection containing the unique media types actively enabled for this profile.
        /// </summary>
        public HashSet<MediaType> EnabledTypes { get; set; } = new()
        {
            MediaType.BoxFront, MediaType.Box3D, MediaType.Logo, MediaType.Manual, MediaType.Music, MediaType.Video, MediaType.TitleScreen, 
            MediaType.FanArt, MediaType.PhysicalMedia
        };
    }
}
