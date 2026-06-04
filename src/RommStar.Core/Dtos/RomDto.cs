namespace RommStar.Core.Dtos
{
    /// <summary>
    /// Placeholder Data Transfer Object mapping Romm's API response layout.
    /// </summary>
    public class RomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RomUrl { get; set; } = string.Empty;
        public string BoxFrontUrl { get; set; } = string.Empty;
        public string Box3DUrl { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
    }
}
