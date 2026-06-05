using RommStar.Core.Models;
using RommStar.Core.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Services
{
    public class RommService
    {
        private readonly HttpClient _client;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

        public RommService()
        {
            _client = new HttpClient();
        }

        // =========================================================================
        // PUBLIC API METHODS
        // =========================================================================

        /// <summary>
        /// Validates connection and credentials against an isolated server snapshot.
        /// </summary>
        public async Task<ApiResponse> TestConnectionAsync(RommServerConfig server, CancellationToken externalToken = default)
        {
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/v1/users/me";

            return await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
        }

        // =========================================================================
        // CENTRALIZED PLUMBING (The DRY Exception & Timeout Gateway)
        // =========================================================================

        private async Task<ApiResponse> SendRequestAsync(
            HttpMethod method,
            string url,
            RommServerConfig server,
            CancellationToken externalToken)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.ApiToken))
            {
                return ApiResponse.Fail(RommApiFailureReason.InvalidConfiguration, "Server configuration values cannot be null or empty.");
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
                    return ApiResponse.Fail(reason, rawStatusCodeMessage);
                }

                return ApiResponse.Success(response);
            }
            catch (OperationCanceledException ex)
            {
                // Triggered if the 5s safety timeout trips or user manually cancels
                bool isTimeout = timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested;
                var reason = isTimeout ? RommApiFailureReason.Timeout : RommApiFailureReason.UnexpectedError;

                return ApiResponse.Fail(reason, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                // Differentiate complete server drop/DNS failure vs response pipeline anomalies
                if (ex.InnerException is System.Net.Sockets.SocketException ||
                    ex.Message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse.Fail(RommApiFailureReason.ServerNotFound, ex.Message);
                }

                return ApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(RommApiFailureReason.UnexpectedError, ex.Message);
            }
        }
    }
}