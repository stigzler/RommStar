using RommStar.Core.Dtos;
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
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(2);

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

        public async Task<RommApiResponse<List<RommPlatformDTO>>> GetRommPlatformsAsync(RommServer server, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/platforms";

            var response = await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
            if (!response.IsSuccess)
            {
                return RommApiResponse<List<RommPlatformDTO>>.Fail(response.FailureReason, response.ExceptionMessage);
            }

            try
            {
                using var contentStream = await response.HttpResponse.Content.ReadAsStreamAsync();
                var platforms = await JsonSerializer.DeserializeAsync<List<RommPlatformDTO>>(contentStream, _jsonOptions);

                response.HttpResponse?.Dispose();
                return RommApiResponse<List<RommPlatformDTO>>.SuccessWithData(response.HttpResponse!, platforms ?? new List<RommPlatformDTO>());
            }
            catch (Exception ex)
            {
                response.HttpResponse?.Dispose();
                return RommApiResponse<List<RommPlatformDTO>>.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }

        // =========================================================================
        // CENTRALIZED PLUMBING (The DRY Exception & Timeout Gateway)
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