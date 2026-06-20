using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Primitives;
using System;
using System.Collections.Generic;
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

        // Cached once — System.Text.Json keys its internal type metadata cache by options
        // instance identity. Creating a new instance on every call busts that cache and
        // forces a full reflection scan + deserialiser JIT on each invocation.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public RommService()
        {
            _client = new HttpClient();
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
            return await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
        }

        public async Task<RommApiResponse<List<PlatformDTO>>> GetRommPlatformsAsync(RommServer server, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/platforms";

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
            if (!response.IsSuccess)
            {
                return RommApiResponse<List<PlatformDTO>>.Fail(response.FailureReason, response.ExceptionMessage);
            }
            try
            {
                using var contentStream = await response.HttpResponse.Content.ReadAsStreamAsync();
                var platforms = await JsonSerializer.DeserializeAsync<List<PlatformDTO>>(contentStream, _jsonOptions);

                response.HttpResponse?.Dispose();
                return RommApiResponse<List<PlatformDTO>>.SuccessWithData(response.HttpResponse!, platforms ?? new List<PlatformDTO>());
            }
            catch (Exception ex)
            {
                response.HttpResponse?.Dispose();
                return RommApiResponse<List<PlatformDTO>>.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }

        }

        public async Task DownloadRoms(RommServer server, List<int> romIds, string filename = "rommDownload.zip",
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

                    response.Dispose();
                    return RommApiResponse.Fail(reason, rawStatusCodeMessage);
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
                    return RommApiResponse.Fail(RommApiFailureReason.ServerNotFound, ex.Message);
                }

                return RommApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
            catch (Exception ex)
            {
                return RommApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }
    }
}