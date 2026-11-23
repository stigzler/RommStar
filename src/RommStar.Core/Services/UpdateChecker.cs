using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RommStar.Core.Services;

public sealed record UpdateInfo(
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string ReleasePageUrl,
    string? ZipDownloadUrl,
    string? ReleaseNotes);

public static class UpdateChecker
{
    private const string Owner = "stigzler";
    private const string Repo = "RommStar";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Repo, "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        current = NormalizeVersion(current);

        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        var cleanTag = release.TagName.TrimStart('v', 'V');
        if (!Version.TryParse(cleanTag, out var latest))
            return null;

        latest = NormalizeVersion(latest);
        var zipAsset = release.Assets?.FirstOrDefault(asset =>
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return new UpdateInfo(
            IsUpdateAvailable: latest > current,
            CurrentVersion: current,
            LatestVersion: latest,
            ReleasePageUrl: release.HtmlUrl,
            ZipDownloadUrl: zipAsset?.BrowserDownloadUrl,
            ReleaseNotes: release.Body);
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static Version NormalizeVersion(Version version) =>
        new(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build));

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
