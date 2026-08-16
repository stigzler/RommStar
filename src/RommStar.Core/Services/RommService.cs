using RommStar.Core.Dtos.Romm;
using RommStar.Core.Extensions;
using RommStar.Core.Models;
using RommStar.Core.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace RommStar.Core.Services
{
    public class RommService
    {
        private readonly HttpClient _client;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

        LoggingService _loggingService;
        SettingsService _settingsService;

        // Cached once — System.Text.Json keys its internal type metadata cache by options
        // instance identity. Creating a new instance on every call busts that cache and
        // forces a full reflection scan + deserialiser JIT on each invocation.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public RommService()
        {
            
        }
        public RommService(LoggingService loggingService, SettingsService settingsService)
        {
            _client = new HttpClient();
            _loggingService = loggingService;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Used in Testing
        /// </summary>
        /// <param name="mockedClient"></param>
        public RommService(HttpClient mockedClient)
        {
            _client = mockedClient;
        }

        // =========================================================================
        // PUBLIC API METHODS
        // =========================================================================

        /// <summary>
        /// Validates connection and credentials against an isolated server snapshot.
        /// </summary>
        public async Task<RommApiResponse> TestConnectionAsync(RommServer server, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/users/me";

            _loggingService.Log($"Testing Romm API Connection via URL: [{endpointUrl.RedactSensitiveInfo(_settingsService.Settings.LoggingRedact)}]");
            
            var result = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);

            if (!result.IsSuccess)
            {
                _loggingService.Log($"WARNING: Could not connect to [{endpointUrl}]: {result.FailureToCSV()}");
            }
            else
            {
                _loggingService.Log($"Connection to Romm Server Successful via URL: [{endpointUrl.RedactSensitiveInfo(_settingsService.Settings.LoggingRedact)}]");
            }

            return result;
        }

        public async Task<RommApiResponse<List<PlatformDTO>>> GetRommPlatformsAsync(RommServer server, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/platforms";

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);

            if (!response.IsSuccess)
            {
                _loggingService.Log($"ERROR: API query unsuccessful: {response.FailureReason}, {response.ExceptionMessage}");
                return RommApiResponse<List<PlatformDTO>>.Fail(response.FailureReason, response.ExceptionMessage);
            }

            // Declare the string OUTSIDE the try block so the catch block can access it
            string rawContent = string.Empty;

            try
            {
                _loggingService.Log($"API query successful. Parsing data.");

                // Read the entire response into a string first
                rawContent = await response.HttpResponse.Content.ReadAsStringAsync();

                // Deserialize from the string instead of the stream
                var platforms = JsonSerializer.Deserialize<List<PlatformDTO>>(rawContent, _jsonOptions);

                response.HttpResponse?.Dispose();
                _loggingService.Log($"Parsing successful.");
                _loggingService.Log($"Content:\r\n{rawContent}", LoggingLevel.Verbose);

                return RommApiResponse<List<PlatformDTO>>.SuccessWithData(response.HttpResponse!, platforms ?? new List<PlatformDTO>());
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Parsing unsuccessful. Content from server: {rawContent}");

                response.HttpResponse?.Dispose();
                return RommApiResponse<List<PlatformDTO>>.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }

        public async Task DownloadRomsAsync(RommServer server, List<int> romIds, string filename = "rommDownload.zip",
            CancellationToken externalToken = default)
        {
            StringBuilder endpointUrl = new($"{server.BaseUrl.TrimEnd('/')}/api/roms/download?");

            endpointUrl.Append($"rom_ids={string.Join(',', romIds.Select(i => i.ToString()))}");
            endpointUrl.Append($"filename={filename}");

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl.ToString(), server, externalToken);



        }

        public async Task<RommApiResponse<RomCollectionDTO>> GetRomCollectionAsync(RommServer server, List<int> platformIds, int offset,
                                CancellationToken externalToken = default)
        {
            StringBuilder urlSB = new($"{server.BaseUrl.TrimEnd('/')}/api/roms?");
            foreach (var platformId in platformIds)
            {
                urlSB.Append($"platform_ids={platformId}&");
            }

            urlSB.Append($"limit={server.PageLimit}&offset={offset}"); // paging parameters

            string endpointUrl = urlSB.ToString();

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
            if (!response.IsSuccess)
            {
                return RommApiResponse<RomCollectionDTO>.Fail(response.FailureReason, response.ExceptionMessage);
            }

            try
            {
                using var contentStream = await response.HttpResponse.Content.ReadAsStreamAsync();
                var roms = await JsonSerializer.DeserializeAsync<RomCollectionDTO>(contentStream, _jsonOptions);
                response.HttpResponse?.Dispose();
                return RommApiResponse<RomCollectionDTO>.SuccessWithData(response.HttpResponse!, roms ?? new RomCollectionDTO());
            }
            catch (Exception ex)
            {
                response.HttpResponse?.Dispose();
                return RommApiResponse<RomCollectionDTO>.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }

        public async Task<RommApiResponse<RomDTO>> GetRomDetailsAsync(RommServer server, int romId, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/roms/{romId}";

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
            if (!response.IsSuccess)
            {
                return RommApiResponse<RomDTO>.Fail(response.FailureReason, response.ExceptionMessage);
            }

            try
            {
                using var contentStream = await response.HttpResponse.Content.ReadAsStreamAsync();
                var romDetail = await JsonSerializer.DeserializeAsync<RomDTO>(contentStream, _jsonOptions);
                response.HttpResponse?.Dispose();
                return RommApiResponse<RomDTO>.SuccessWithData(response.HttpResponse!, romDetail ?? new RomDTO());
            }
            catch (Exception ex)
            {
                response.HttpResponse?.Dispose();
                return RommApiResponse<RomDTO>.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }

        public async Task<string> DownloadRomsToDiskAsync(RommServer server, List<int> romIds, string targetFilePath, CancellationToken externalToken = default)
        {
            if (romIds == null || romIds.Count == 0) return "No ROM IDs provided.";

            string idsParam = string.Join(',', romIds);
            string filenameParam = Path.GetFileName(targetFilePath);
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/roms/download?rom_ids={idsParam}&filename={filenameParam}";

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromHours(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, externalToken);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpointUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.ApiToken);

                // TEST
                //if (endpointUrl.Contains("391")) throw new Exception("Test DownloadRomsToDiskAsync Exception on Atari 5200 id 391");

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return $"HTTP Error: {(int)response.StatusCode} {response.StatusCode}";
                }

                using (var sourceStream = await response.Content.ReadAsStreamAsync(linkedCts.Token))
                using (var targetStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await sourceStream.CopyToAsync(targetStream, linkedCts.Token);
                }

                return string.Empty; // Success
            }
            catch (Exception ex)
            {
                if (File.Exists(targetFilePath))
                {
                    try { File.Delete(targetFilePath); } catch { }
                }
                return $"Exception: {ex.Message}";
            }
        }

        // =========================================================================
        // CENTRALIZED PLUMBING (Timeout Gateway)
        // =========================================================================

        private async Task<RommApiResponse> SendRequestAsync(
            HttpMethod method,
            string url,
            RommServer server,
            CancellationToken externalToken)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.ApiToken))
            {
                return RommApiResponse.Fail(RommApiFailureReason.InvalidConfiguration, "Server configuration values cannot be null or empty.");
            }

            using var timeoutCts = new CancellationTokenSource(_defaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, externalToken);

            try
            {
                var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.ApiToken);

                // Test --------------------
                // Test end

                var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var reason = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => RommApiFailureReason.Unauthorized,
                        HttpStatusCode.Forbidden => RommApiFailureReason.Forbidden,
                        HttpStatusCode.NotFound => RommApiFailureReason.EndpointNotFound,
                        _ when (int)response.StatusCode >= 500 => RommApiFailureReason.UnknownServerError,
                        _ => RommApiFailureReason.UnexpectedError
                    };

                    string rawStatusCodeMessage = $"Server returned HTTP status code {(int)response.StatusCode} ({response.StatusCode}).";

                    //response.Dispose();
                    return RommApiResponse.Fail(reason, rawStatusCodeMessage, response);
                }

                return RommApiResponse.Success(response);
            }
            catch (OperationCanceledException ex)
            {
                // Triggered if the 5s safety timeout trips or user manually cancels
                bool isTimeout = timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested;
                var reason = isTimeout ? RommApiFailureReason.Timeout : RommApiFailureReason.UnexpectedError;

                return RommApiResponse.Fail(reason, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                // Differentiate complete server drop/DNS failure vs response pipeline anomalies
                if (ex.InnerException is System.Net.Sockets.SocketException ||
                    ex.Message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
                {
                    _loggingService.Log($"ERROR: HttpRequestException: {ex.StatusCode}: {ex.Message}. {ex.HttpRequestError}\r\n{ex.StackTrace} ", LoggingLevel.Debug);
                    return RommApiResponse.Fail(RommApiFailureReason.ServerNotFound, ex.Message);
                }

                return RommApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"ERROR: HttpRequestException: {ex.Message}\r\n{ex.StackTrace} ", LoggingLevel.Debug);
                return RommApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }
    }
}