using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RommStar.Tests
{
    [Trait("Category", "RommLiveServer")]
    public class RommServiceIntegrationTests
    {
        private readonly RommServer _liveConfig;
        private readonly bool _isConfigured;

        public RommServiceIntegrationTests()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secretsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(configPath);
                    var secrets = JsonSerializer.Deserialize<SecretConfig>(jsonText);

                    if (secrets != null && !string.IsNullOrEmpty(secrets.RommLiveBaseUrl) && !string.IsNullOrEmpty(secrets.RommLiveApiToken))
                    {
                        // Map the newly named keys smoothly to your production domain model
                        _liveConfig = new RommServer
                        {
                            BaseUrl = secrets.RommLiveBaseUrl,
                            ApiToken = secrets.RommLiveApiToken,
                            ServerName = "Live Docker Instance"
                        };

                        _isConfigured = true;
                    }
                }
                catch
                {
                    // Fail gracefully if JSON parsing breaks
                }
            }
        }

        [Fact(DisplayName = "Integration: Connect to Live Local RomM Server")]
        public async Task Test_LiveServerConnection()
        {
            SkipIfSecretsMissing();

            var service = new RommService();

            RommApiResponse result = await service.TestConnectionAsync(_liveConfig);

            Assert.True(result.IsSuccess);
        }

        [Fact(DisplayName = "Integration: Verify Something Else Later")]
        public async Task Test_AnotherFeature()
        {
            SkipIfSecretsMissing();

            var service = new RommService();

            // Ready for clean reuse down the line
        }

        private void SkipIfSecretsMissing()
        {
            if (!_isConfigured)
            {
                Assert.Fail("Integration test skipped: 'secretsettings.json' is missing or unconfigured.");
            }
        }
    }
}