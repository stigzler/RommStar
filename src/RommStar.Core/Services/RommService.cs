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
            string endpointUrl = $"{server.BaseUrl.TrimEnd('/')}/api/heartbeat";
            return await SendRequestAsync(HttpMethod.Get, endpointUrl, server, externalToken);
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