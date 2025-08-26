using System;
using System.Threading.Tasks;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Interface for feature flags service
    /// </summary>
    internal interface IFeatureFlags : IDisposable
    {
        /// <summary>
        /// Initialize the feature flags system
        /// </summary>
        /// <param name="clientKey">GrowthBook client key</param>
        Task Initialize(string clientKey);

        /// <summary>
        /// Get the event single URL from feature flags
        /// </summary>
        string GetEventSingleUrl();

        /// <summary>
        /// Get the event batch URL from feature flags
        /// </summary>
        string GetEventBatchUrl();

        /// <summary>
        /// Check if ad ID collection is enabled via feature flags
        /// </summary>
        bool IsAdIdEnabled();


        /// <summary>
        /// Get the PlaySuper Analytics URL
        /// </summary>
        string GetPSAnalyticsUrl();

        /// <summary>
        /// Get a numeric feature value
        /// </summary>
        double GetNumberFeature(string key, double defaultValue);

        /// <summary>
        /// Get a JSON feature value
        /// </summary>
        string GetJsonFeature(string key, string defaultValue);

        /// <summary>
        /// Force refresh feature flags from GrowthBook
        /// </summary>
        Task ForceRefresh();

        /// <summary>
        /// Clear the feature flags cache
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Log the current cache state and feature values
        /// </summary>
        void LogCacheState();

        /// <summary>
        /// Cleanup resources and stop background refresh
        /// </summary>
        Task Cleanup();
    }
}