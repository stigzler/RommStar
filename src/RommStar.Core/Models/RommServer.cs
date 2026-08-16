namespace RommStar.Core.Models
{
    public class RommServer
    {
        [Primitives.Encrypted]
        public string ApiToken { get; set; } = string.Empty;
        private string _baseUrl = string.Empty;
        public string BaseUrl {
            get => _baseUrl;
            // The ?.Trim() safely removes leading and trailing spaces as the user types. TrimEnd any illegal final chars
            set => _baseUrl = value?.Trim().TrimEnd('/', '\\', '#', '?', '.', ',', ';') ?? string.Empty;
        }

        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// The number of results to return per page for API queries (default 50)
        /// TODO: Consider making this user editable?
        /// </summary>
        public int PageLimit { get; set; } = 50;

    }
}