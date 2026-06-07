namespace RommStar.Core.Models
{
    public class RommServer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BaseUrl { get; set; } = string.Empty;

        [RommStar.Core.Primitives.Encrypted]
        public string ApiToken { get; set; } = string.Empty;

        public string ServerName { get; set; } = string.Empty;
    }
}