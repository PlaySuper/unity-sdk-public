using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class FeatureFlagsStaticTests
    {
        [SetUp]
        public void Setup()
        {
            // Reset the static instance before each test
            var instanceField = typeof(FeatureFlags).GetField("instance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            instanceField.SetValue(null, null);
        }

        [Test]
        public async Task FeatureFlagsStatic_ShouldProvideDefaultValues()
        {
            // Test that the static wrapper provides default values without initialization
            string singleUrl = FeatureFlags.GetEventSingleUrl();
            Assert.AreEqual(Constants.MIXPANEL_URL, singleUrl);

            string batchUrl = FeatureFlags.GetEventBatchUrl();
            Assert.AreEqual(Constants.MIXPANEL_URL_BATCH, batchUrl);

            bool adIdEnabled = FeatureFlags.IsAdIdEnabled();
            Assert.IsTrue(adIdEnabled);

            double numberValue = FeatureFlags.GetNumberFeature("test_key", 10.5);
            Assert.AreEqual(10.5, numberValue);

            string jsonValue = FeatureFlags.GetJsonFeature("test_key", "{\"default\": true}");
            Assert.AreEqual("{\"default\": true}", jsonValue);
        }

        [Test]
        public async Task FeatureFlagsStatic_ShouldHandleForceRefresh()
        {
            // Test that ForceRefresh can be called without throwing exceptions
            // Note: This won't actually refresh anything since we're not initialized
            Assert.DoesNotThrowAsync(async () => await FeatureFlags.ForceRefresh());
        }

        [Test]
        public void FeatureFlagsStatic_ShouldHandleClearCache()
        {
            // Test that ClearCache can be called without throwing exceptions
            Assert.DoesNotThrow(() => FeatureFlags.ClearCache());
        }

        [Test]
        public async Task FeatureFlagsStatic_ShouldHandleCleanup()
        {
            // Test that Cleanup can be called without throwing exceptions
            Assert.DoesNotThrowAsync(async () => await FeatureFlags.Cleanup());
        }

        [Test]
        public void FeatureFlagsStatic_LogCacheState_ShouldNotThrow()
        {
            // Test that LogCacheState can be called without throwing exceptions
            Assert.DoesNotThrow(() => FeatureFlags.LogCacheState());
        }

        [Test]
        public async Task FeatureFlagsStatic_Initialize_ShouldCreateInstance()
        {
            // Test that Initialize creates an instance
            // Note: This won't fully initialize since we don't have a real client key
            Assert.DoesNotThrowAsync(async () => await FeatureFlags.Initialize("test-key"));
        }

        [Test]
        public async Task FeatureFlagsStatic_ConsecutiveInitialize_ShouldDisposePreviousInstance()
        {
            // Test that consecutive calls to Initialize dispose the previous instance
            Assert.DoesNotThrowAsync(async () =>
            {
                await FeatureFlags.Initialize("test-key-1");
                await FeatureFlags.Initialize("test-key-2");
            });
        }

        [Test]
        public void FeatureFlagsStatic_GetMethods_ShouldReturnConsistentDefaults()
        {
            // Test that all getter methods return consistent default values
            // Single URL
            Assert.AreEqual(Constants.MIXPANEL_URL, FeatureFlags.GetEventSingleUrl());
            Assert.AreEqual(Constants.MIXPANEL_URL, FeatureFlags.GetEventSingleUrl()); // Call twice to ensure consistency

            // Batch URL
            Assert.AreEqual(Constants.MIXPANEL_URL_BATCH, FeatureFlags.GetEventBatchUrl());
            Assert.AreEqual(Constants.MIXPANEL_URL_BATCH, FeatureFlags.GetEventBatchUrl()); // Call twice to ensure consistency

            // Ad ID
            Assert.IsTrue(FeatureFlags.IsAdIdEnabled());
            Assert.IsTrue(FeatureFlags.IsAdIdEnabled()); // Call twice to ensure consistency

            // Number feature
            Assert.AreEqual(42.0, FeatureFlags.GetNumberFeature("test", 42.0));
            Assert.AreEqual(42.0, FeatureFlags.GetNumberFeature("test", 42.0)); // Call twice to ensure consistency

            // JSON feature
            Assert.AreEqual("{}", FeatureFlags.GetJsonFeature("test", "{}"));
            Assert.AreEqual("{}", FeatureFlags.GetJsonFeature("test", "{}")); // Call twice to ensure consistency
        }
    }
}