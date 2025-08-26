using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Static wrapper for feature flags service for backward compatibility
    /// </summary>
    internal static class FeatureFlags
    {
        private static IFeatureFlags instance;

        /// <summary>
        /// Initialize the feature flags system
        /// </summary>
        /// <param name="clientKey">GrowthBook client key</param>
        public static async Task Initialize(string clientKey)
        {
            if (instance != null)
            {
                instance.Dispose();
            }

            instance = new FeatureFlagsService();
            await instance.Initialize(clientKey);
        }

        /// <summary>
        /// Get the event single URL from feature flags
        /// </summary>
        public static string GetEventSingleUrl()
        {
            return instance?.GetEventSingleUrl() ?? Constants.MIXPANEL_URL;
        }

        /// <summary>
        /// Get the event batch URL from feature flags
        /// </summary>
        public static string GetEventBatchUrl()
        {
            return instance?.GetEventBatchUrl() ?? Constants.MIXPANEL_URL_BATCH;
        }

        /// <summary>
        /// Check if ad ID collection is enabled via feature flags
        /// </summary>
        public static bool IsAdIdEnabled()
        {
            return instance?.IsAdIdEnabled() ?? true;
        }

        /// <summary>
        /// Get the PlaySuper Analytics URL
        /// </summary>
        public static string GetPSAnalyticsUrl()
        {
            return instance?.GetPSAnalyticsUrl() ?? Constants.PS_ANALYTICS_URL;
        }

        /// <summary>
        /// Get a numeric feature value
        /// </summary>
        public static double GetNumberFeature(string key, double defaultValue)
        {
            return instance?.GetNumberFeature(key, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// Get a JSON feature value
        /// </summary>
        public static string GetJsonFeature(string key, string defaultValue)
        {
            return instance?.GetJsonFeature(key, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// Force refresh feature flags from GrowthBook
        /// </summary>
        public static async Task ForceRefresh()
        {
            if (instance != null)
            {
                await instance.ForceRefresh();
            }
        }

        /// <summary>
        /// Clear the feature flags cache
        /// </summary>
        public static void ClearCache()
        {
            instance?.ClearCache();
        }

        /// <summary>
        /// Log the current cache state and feature values
        /// </summary>
        public static void LogCacheState()
        {
            instance?.LogCacheState();
        }

        /// <summary>
        /// Cleanup resources and stop background refresh
        /// </summary>
        public static async Task Cleanup()
        {
            if (instance != null)
            {
                await instance.Cleanup();
            }
        }
    }
}