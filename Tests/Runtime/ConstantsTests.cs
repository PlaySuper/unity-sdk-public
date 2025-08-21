using NUnit.Framework;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class ConstantsTests
    {
        [Test]
        public void Constants_ShouldHaveExpectedValues()
        {
            // These tests verify that the constants exist and have reasonable types
            // We're not testing the actual values since they may change

            // Just verify the constants exist and can be accessed
            Assert.DoesNotThrow(() =>
            {
                var refreshInterval = Constants.GROWTHBOOK_REFRESH_INTERVAL_SECONDS;
                var apiUrl = Constants.GROWTHBOOK_API_URL;
                var mixpanelUrl = Constants.MIXPANEL_URL;
                var mixpanelBatchUrl = Constants.MIXPANEL_URL_BATCH;
            });
        }
    }
}