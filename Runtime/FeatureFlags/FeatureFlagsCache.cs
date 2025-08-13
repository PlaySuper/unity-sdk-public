using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Responsible for caching feature flags and managing refresh intervals
    /// </summary>
    internal class FeatureFlagsCache
    {
        private Dictionary<string, CachedFeature> cache = new Dictionary<string, CachedFeature>();
        private DateTime lastRefreshTime = DateTime.MinValue;
        private static readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(Constants.GROWTHBOOK_REFRESH_INTERVAL_SECONDS);

        /// <summary>
        /// Get a cached feature value if it exists and is still valid
        /// </summary>
        /// <typeparam name="T">Type of the feature value</typeparam>
        /// <param name="key">Feature key</param>
        /// <param name="defaultValue">Default value if not found or expired</param>
        /// <returns>Cached value or default</returns>
        public T GetCachedValue<T>(string key, T defaultValue, Func<string, T> parser)
        {
            if (cache.TryGetValue(key, out var cachedFeature) && cachedFeature.IsValid)
            {
                try
                {
                    return parser(cachedFeature.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlaySuper][FeatureFlags] Failed to parse cached value for {key}: {ex.Message}");
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Update the cache with a new feature value
        /// </summary>
        /// <param name="key">Feature key</param>
        /// <param name="value">Feature value as string</param>
        public void UpdateCache(string key, string value)
        {
            cache[key] = new CachedFeature
            {
                Value = value,
                CachedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Check if cache should be refreshed based on time interval
        /// </summary>
        /// <returns>True if cache should be refreshed</returns>
        public bool ShouldRefresh()
        {
            return DateTime.UtcNow - lastRefreshTime > refreshInterval;
        }

        /// <summary>
        /// Mark the cache as refreshed
        /// </summary>
        public void MarkRefreshed()
        {
            lastRefreshTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Clear all cached features
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
            Debug.Log("[PlaySuper][FeatureFlags] Cache cleared manually");
        }

        /// <summary>
        /// Log the current cache state
        /// </summary>
        public void LogCacheState()
        {
            Debug.Log($"[PlaySuper][FeatureFlags] Cache State:");
            Debug.Log($"[PlaySuper][FeatureFlags] - Cache count: {cache.Count}");
            Debug.Log($"[PlaySuper][FeatureFlags] - Last refresh: {lastRefreshTime}");

            foreach (var entry in cache)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] - {entry.Key}: {entry.Value.Value} (valid: {entry.Value.IsValid})");
            }
        }
    }
}