namespace RommStar.Core.Models
{
    public class RommServer
    {
        [RommStar.Core.Primitives.Encrypted]
        public string ApiToken { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// The number of results to return per page for API queries (default 50)
        /// TODO: Consider making this user editable?
        /// </summary>
        public int PageLimit { get; set; } = 50;

    }
}