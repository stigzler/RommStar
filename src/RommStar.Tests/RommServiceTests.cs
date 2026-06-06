using RommStar.Core.Models;
using RommStar.Core.Primitives;
using RommStar.Core.Services;
using RommStar.Tests.Handlers;
using System.Net;

namespace RommStar.Tests
{
    [Trait("Category", "Local")]
    public class RommServiceTests
    {
        [Fact(DisplayName = "RommAPI: Fail on Empty Token")]
        public async Task TestConnectionAsync_ShouldReturnInvalidConfiguration_WhenTokenIsEmpty()
        {
            // 1. ARRANGE
            var service = new RommService();
            var badConfig = new RommServer
            {
                ServerName = "Test Server",
                BaseUrl = "http://192.168.1.50:8080",
                ApiToken = "" // Deliberately blank to trip the validation
            };

            // 2. ACT
            RommApiResponse result = await service.TestConnectionAsync(badConfig);

            // 3. ASSERT
            Assert.False(result.IsSuccess);
            Assert.Equal(RommApiFailureReason.InvalidConfiguration, result.FailureReason);
            Assert.Contains("null or empty", result.ExceptionMessage);
        }

        [Fact(DisplayName = "RommAPI: Fail when Server is Offline")]
        public async Task TestConnectionAsync_ShouldReturnServerNotFound_WhenHostIsDown()
        {
            // 1. ARRANGE: Tell our fake network handler to throw a connection exception
            var fakeHandler = new TestHttpMessageHandler(request =>
            {
                var socketException = new System.Net.Sockets.SocketException(
                    (int)System.Net.Sockets.SocketError.ConnectionRefused);

                throw new HttpRequestException("No connection could be made because the target machine actively refused it.", socketException);
            });

            var mockedClient = new HttpClient(fakeHandler);
            var service = new RommService(mockedClient);

            var offlineConfig = new RommServer
            {
                BaseUrl = "http://192.168.1.99:8080", // A hypothetical dead address
                ApiToken = "any_token"
            };

            // 2. ACT: Run the connection test
            RommApiResponse result = await service.TestConnectionAsync(offlineConfig);

            // 3. ASSERT: Verify your backend plumbing caught it and mapped it correctly
            Assert.False(result.IsSuccess);
            Assert.Equal(RommApiFailureReason.ServerNotFound, result.FailureReason);
            Assert.Contains("actively refused it", result.ExceptionMessage);
        }

        [Fact(DisplayName = "RommAPI: Success on 200 OK")]
        public async Task TestConnectionAsync_ShouldReturnSuccess_WhenServerResponds200Ok()
        {
            // 1. ARRANGE: Set up a fake network rule returning 200 OK
            var fakeHandler = new TestHttpMessageHandler(request =>
                new HttpResponseMessage(HttpStatusCode.OK));

            var mockedClient = new HttpClient(fakeHandler);
            var service = new RommService(mockedClient); // Inject fake client

            var validConfig = new RommServer
            {
                ServerName = "Local",
                BaseUrl = "http://localhost",
                ApiToken = "valid_key"
            };

            // 2. ACT
            RommApiResponse result = await service.TestConnectionAsync(validConfig);

            // 3. ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(RommApiFailureReason.None, result.FailureReason);
        }

        [Fact(DisplayName = "RommAPI: Unauthorized on 401")]
        public async Task TestConnectionAsync_ShouldReturnUnauthorized_WhenServerResponds401()
        {
            // 1. ARRANGE: Set up a fake network rule returning 401 Unauthorized
            var fakeHandler = new TestHttpMessageHandler(request =>
                new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var mockedClient = new HttpClient(fakeHandler);
            var service = new RommService(mockedClient);

            var badTokenConfig = new RommServer
            {
                ServerName = "Local",
                BaseUrl = "http://localhost",
                ApiToken = "expired_key"
            };

            // 2. ACT
            RommApiResponse result = await service.TestConnectionAsync(badTokenConfig);

            // 3. ASSERT
            Assert.False(result.IsSuccess);
            Assert.Equal(RommApiFailureReason.Unauthorized, result.FailureReason);
            Assert.Contains("401", result.ExceptionMessage);
        }
    }
}