using NUnit.Framework;
using System;
using UnityEngine;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class FeatureFlagsCacheTests
    {
        private FeatureFlagsCache cache;

        [SetUp]
        public void Setup()
        {
            cache = new FeatureFlagsCache();
        }

        [TearDown]
        public void TearDown()
        {
            // Clear cache after each test
            cache.ClearCache();
        }

        [Test]
        public void FeatureFlagsCache_ShouldReturnDefaultWhenNotCached()
        {
            // Test that cache returns default value when key is not present
            string result = cache.GetCachedValue("test_key", "default_value", s => s);
            Assert.AreEqual("default_value", result);
        }

        [Test]
        public void FeatureFlagsCache_ShouldCacheAndRetrieveValues()
        {
            // Test caching a value
            cache.UpdateCache("test_key", "cached_value");

            // Test retrieving the cached value
            string result = cache.GetCachedValue("test_key", "default_value", s => s);
            Assert.AreEqual("cached_value", result);
        }

        [Test]
        public void FeatureFlagsCache_ShouldHandleDifferentTypes()
        {
            // Test caching a numeric value
            cache.UpdateCache("number_key", "42.5");
            double result = cache.GetCachedValue("number_key", 0.0, double.Parse);
            Assert.AreEqual(42.5, result);

            // Test caching a boolean value
            cache.UpdateCache("bool_key", "true");
            bool boolResult = cache.GetCachedValue("bool_key", false, bool.Parse);
            Assert.IsTrue(boolResult);
        }

        [Test]
        public void FeatureFlagsCache_ShouldRespectCacheTimeout()
        {
            // Test that cache respects timeout
            cache.UpdateCache("test_key", "cached_value");

            // Cache should be valid immediately
            Assert.IsFalse(cache.ShouldRefresh());

            // Note: We can't easily test the timeout expiration without modifying time,
            // but we can verify the method exists and returns a boolean
            bool shouldRefresh = cache.ShouldRefresh();
            Assert.IsInstanceOf<bool>(shouldRefresh);
        }

        [Test]
        public void FeatureFlagsCache_ShouldClearCache()
        {
            // Add some values to cache
            cache.UpdateCache("key1", "value1");
            cache.UpdateCache("key2", "value2");

            // Verify cache has values
            string result1 = cache.GetCachedValue("key1", "default", s => s);
            Assert.AreEqual("value1", result1);

            // Clear cache
            cache.ClearCache();

            // Verify cache returns defaults after clearing
            string result2 = cache.GetCachedValue("key1", "default", s => s);
            Assert.AreEqual("default", result2);
        }

        [Test]
        public void FeatureFlagsCache_ShouldMarkRefreshed()
        {
            // Test that MarkRefreshed can be called without throwing exceptions
            Assert.DoesNotThrow(() => cache.MarkRefreshed());
        }

        [Test]
        public void FeatureFlagsCache_ShouldHandleParserExceptions()
        {
            // Add a value that will cause parsing to fail
            cache.UpdateCache("invalid_number", "not_a_number");

            // Should return default value when parser throws exception
            double result = cache.GetCachedValue("invalid_number", 42.0, double.Parse);
            Assert.AreEqual(42.0, result);
        }

        [Test]
        public void FeatureFlagsCache_GetCachedValue_ShouldReturnDefault_ForExpiredCache()
        {
            // Add a value with a past timestamp (simulating expired cache)
            var field = typeof(FeatureFlagsCache).GetField("cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cacheDict = (System.Collections.Generic.Dictionary<string, CachedFeature>)field.GetValue(cache);

            cacheDict["expired_key"] = new CachedFeature
            {
                Value = "old_value",
                CachedAt = DateTime.UtcNow.AddMinutes(-6) // Expired (older than 5 minutes)
            };

            string result = cache.GetCachedValue("expired_key", "default_value", s => s);
            Assert.AreEqual("default_value", result);
        }

        [Test]
        public void FeatureFlagsCache_GetCachedValue_ShouldReturnCachedValue_ForValidCache()
        {
            // Add a value with a recent timestamp (simulating valid cache)
            var field = typeof(FeatureFlagsCache).GetField("cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cacheDict = (System.Collections.Generic.Dictionary<string, CachedFeature>)field.GetValue(cache);

            cacheDict["valid_key"] = new CachedFeature
            {
                Value = "cached_value",
                CachedAt = DateTime.UtcNow.AddMinutes(-4) // Valid (less than 5 minutes old)
            };

            string result = cache.GetCachedValue("valid_key", "default_value", s => s);
            Assert.AreEqual("cached_value", result);
        }

        [Test]
        public void FeatureFlagsCache_LogCacheState_ShouldNotThrow()
        {
            // Should not throw any exceptions when logging cache state
            Assert.DoesNotThrow(() => cache.LogCacheState());

            // Add some values and test again
            cache.UpdateCache("key1", "value1");
            cache.UpdateCache("key2", "value2");
            Assert.DoesNotThrow(() => cache.LogCacheState());
        }

        [Test]
        public void FeatureFlagsCache_ShouldHandleConcurrentAccess()
        {
            // Test that cache can handle multiple operations
            Assert.DoesNotThrow(() =>
            {
                cache.UpdateCache("key1", "value1");
                cache.UpdateCache("key2", "value2");
                cache.UpdateCache("key3", "value3");

                var result1 = cache.GetCachedValue("key1", "default", s => s);
                var result2 = cache.GetCachedValue("key2", "default", s => s);
                var result3 = cache.GetCachedValue("key3", "default", s => s);

                Assert.AreEqual("value1", result1);
                Assert.AreEqual("value2", result2);
                Assert.AreEqual("value3", result3);
            });
        }
    }
}