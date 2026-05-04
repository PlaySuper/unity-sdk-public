using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Main orchestrator for feature flags service
    /// </summary>
    internal class FeatureFlagsService : IFeatureFlags
    {
        private bool disposed = false;
        private string clientKey;
        private bool isInitialized;
        private GameData gameData;
        private CancellationTokenSource refreshCancellation;

        // Component dependencies
        private GrowthBookApiClient apiClient;
        private GrowthBookResponseParser parser;
        private FeatureRuleEvaluator evaluator;
        private FeatureFlagsCache cache;

        // Stored feature definitions
        private ParsedFeatures parsedFeatures;

        // Feature flag keys
        private const string EVENT_SINGLE_URL_KEY = "sdk_event_single_url";
        private const string EVENT_BATCH_URL_KEY = "sdk_event_batch_url";
        private const string ENABLE_AD_ID_KEY = "sdk_enable_ad_id";
        private const string PS_ANALYTICS_URL_KEY = "sdk_ps_analytics_url";

        public FeatureFlagsService()
        {
            cache = new FeatureFlagsCache();
        }

        /// <summary>
        /// Initialize the feature flags system
        /// </summary>
        /// <param name="clientKey">GrowthBook client key</param>
        public async Task Initialize(string clientKey)
        {
            // Cancel any existing refresh task
            if (refreshCancellation != null)
            {
                await Cleanup();
            }

            this.clientKey = clientKey;
            isInitialized = true;

            // Initialize components
            apiClient = new GrowthBookApiClient(clientKey);
            parser = new GrowthBookResponseParser();

            // Get game data for targeting
            gameData = await GameManager.GetGameData();
            if (gameData != null)
            {
                evaluator = new FeatureRuleEvaluator(gameData);
                Debug.Log($"[PlaySuper][FeatureFlags] Initialized with targeting data: gameId={gameData.id}, studioId={gameData.studioId}");
            }

            // Start new refresh task
            refreshCancellation = new CancellationTokenSource();

            // Start initial fetch and wait for it to complete
            await RefreshFeaturesFromApi();

            // Start background refresh on main thread (must not use Task.Run —
            // the loop calls UnityWebRequest which requires the main thread)
            _ = BackgroundRefreshLoop(refreshCancellation.Token);
        }

        /// <summary>
        /// Get the event single URL from feature flags
        /// </summary>
        public string GetEventSingleUrl()
        {
            return GetStringFeature(EVENT_SINGLE_URL_KEY, Constants.PS_ANALYTICS_EVENT_URL);
        }

        /// <summary>
        /// Get the event batch URL from feature flags
        /// </summary>
        public string GetEventBatchUrl()
        {
            return GetStringFeature(EVENT_BATCH_URL_KEY, Constants.PS_ANALYTICS_BATCH_URL);
        }

        /// <summary>
        /// Check if ad ID collection is enabled via feature flags
        /// </summary>
        public bool IsAdIdEnabled()
        {
            return GetBoolFeature(ENABLE_AD_ID_KEY, true);
        }

        /// <summary>
        /// Get the PlaySuper Analytics URL for production
        /// </summary>
        public string GetPSAnalyticsUrl()
        {
            return GetStringFeature(PS_ANALYTICS_URL_KEY, Constants.PS_ANALYTICS_URL);
        }

        /// <summary>
        /// Get a numeric feature value
        /// </summary>
        public double GetNumberFeature(string key, double defaultValue)
        {
            // Safe parser that uses TryParse to avoid exceptions from malformed remote values
            Func<string, double> safeParser = value =>
                double.TryParse(value, out double result) ? result : defaultValue;

            // Try cache first
            var cachedValue = cache.GetCachedValue(key, defaultValue, safeParser);
            if (cachedValue != defaultValue)
            {
                return cachedValue;
            }

            if (!isInitialized || evaluator == null || parser == null || apiClient == null)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] GetNumberFeature({key}): Using default, initialized={isInitialized}");
                cache.UpdateCache(key, defaultValue.ToString());
                return defaultValue;
            }

            // Get feature definition and evaluate
            var featureDefinition = GetFeatureDefinition(key);
            if (featureDefinition != null)
            {
                var evaluatedValue = evaluator.EvaluateFeature(key, defaultValue, featureDefinition, safeParser);
                cache.UpdateCache(key, evaluatedValue.ToString());
                return evaluatedValue;
            }

            cache.UpdateCache(key, defaultValue.ToString());
            return defaultValue;
        }

        /// <summary>
        /// Get a JSON feature value
        /// </summary>
        public string GetJsonFeature(string key, string defaultValue)
        {
            var value = GetStringFeature(key, defaultValue);

            // Validate JSON before returning
            if (!JsonValidator.IsValidJson(value))
            {
                Debug.LogWarning($"[PlaySuper][FeatureFlags] Invalid JSON for {key}, returning default");
                return defaultValue;
            }

            return value;
        }

        /// <summary>
        /// Force refresh feature flags from GrowthBook
        /// </summary>
        public async Task ForceRefresh()
        {
            ClearCache();
            Debug.Log("[PlaySuper][FeatureFlags] Cache cleared, fetching fresh values...");
            await RefreshFeaturesFromApi();
        }

        /// <summary>
        /// Clear the feature flags cache
        /// </summary>
        public void ClearCache()
        {
            cache.ClearCache();
        }

        /// <summary>
        /// Log the current cache state and feature values
        /// </summary>
        public void LogCacheState()
        {
            cache.LogCacheState();

            if (isInitialized)
            {
                Debug.Log("[PlaySuper][FeatureFlags] Current Values:");
                Debug.Log($"[PlaySuper][FeatureFlags] - {EVENT_SINGLE_URL_KEY}: {GetEventSingleUrl()}");
                Debug.Log($"[PlaySuper][FeatureFlags] - {EVENT_BATCH_URL_KEY}: {GetEventBatchUrl()}");
                Debug.Log($"[PlaySuper][FeatureFlags] - {ENABLE_AD_ID_KEY}: {IsAdIdEnabled()}");
                Debug.Log($"[PlaySuper][FeatureFlags] - {PS_ANALYTICS_URL_KEY}: {GetPSAnalyticsUrl()}");
            }
        }

        /// <summary>
        /// Cleanup resources and stop background refresh
        /// </summary>
        public async Task Cleanup()
        {
            if (refreshCancellation != null)
            {
                refreshCancellation.Cancel();
                refreshCancellation.Dispose();
                refreshCancellation = null;
                Debug.Log("[PlaySuper][FeatureFlags] Cleanup completed, background refresh stopped");
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (refreshCancellation != null)
                    {
                        refreshCancellation.Cancel();
                        refreshCancellation.Dispose();
                        refreshCancellation = null;
                    }
                    isInitialized = false;
                    gameData = null;
                }

                disposed = true;
            }
        }

        ~FeatureFlagsService()
        {
            Dispose(false);
        }

        #region Private Methods

        /// <summary>
        /// Get a string feature value
        /// </summary>
        private string GetStringFeature(string key, string defaultValue)
        {
            // Try cache first
            var cachedValue = cache.GetCachedValue(key, defaultValue, value => value);
            if (cachedValue != defaultValue)
            {
                return cachedValue;
            }

            if (!isInitialized || evaluator == null || parser == null || apiClient == null)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] GetStringFeature({key}): Using default, initialized={isInitialized}");
                cache.UpdateCache(key, defaultValue);
                return defaultValue;
            }

            // Get feature definition and evaluate
            var featureDefinition = GetFeatureDefinition(key);
            if (featureDefinition != null)
            {
                var evaluatedValue = evaluator.EvaluateFeature(key, defaultValue, featureDefinition, value => value);
                cache.UpdateCache(key, evaluatedValue);
                return evaluatedValue;
            }

            cache.UpdateCache(key, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// Get a boolean feature value
        /// </summary>
        private bool GetBoolFeature(string key, bool defaultValue)
        {
            // Safe parser that uses TryParse to avoid exceptions from malformed remote values
            Func<string, bool> safeParser = value =>
                bool.TryParse(value, out bool result) ? result : defaultValue;

            // Try cache first
            var cachedValue = cache.GetCachedValue(key, defaultValue, safeParser);
            if (cachedValue != defaultValue)
            {
                return cachedValue;
            }

            if (!isInitialized || evaluator == null)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] GetBoolFeature({key}): Using default, initialized={isInitialized}");
                cache.UpdateCache(key, defaultValue.ToString().ToLowerInvariant());
                return defaultValue;
            }

            // Get feature definition and evaluate
            var featureDefinition = GetFeatureDefinition(key);
            if (featureDefinition != null)
            {
                var evaluatedValue = evaluator.EvaluateFeature(key, defaultValue, featureDefinition, safeParser);
                cache.UpdateCache(key, evaluatedValue.ToString().ToLowerInvariant());
                return evaluatedValue;
            }

            cache.UpdateCache(key, defaultValue.ToString().ToLowerInvariant());
            return defaultValue;
        }

        /// <summary>
        /// Get feature definition by key
        /// </summary>
        private FeatureDefinition GetFeatureDefinition(string key)
        {
            if (parsedFeatures?.Features != null && parsedFeatures.Features.ContainsKey(key))
            {
                return parsedFeatures.Features[key];
            }
            return null;
        }

        /// <summary>
        /// Refresh features from the API
        /// </summary>
        private async Task RefreshFeaturesFromApi()
        {
            if (!isInitialized || apiClient == null || !apiClient.IsNetworkAvailable())
            {
                return;
            }

            var rawResponse = await apiClient.FetchRawResponse();
            if (string.IsNullOrEmpty(rawResponse))
            {
                return;
            }

            var parsedFeatures = parser.ParseResponse(rawResponse);
            if (parsedFeatures == null)
            {
                return;
            }

            // Store the parsed features
            this.parsedFeatures = parsedFeatures;
            cache.MarkRefreshed();

            Debug.Log($"[PlaySuper][FeatureFlags] Successfully refreshed {parsedFeatures.Features.Count} features");
        }

        /// <summary>
        /// Background refresh loop — waits using Task.Delay to avoid per-frame CPU overhead.
        /// The await continuation runs on Unity's main thread via SynchronizationContext.
        /// </summary>
        private async Task BackgroundRefreshLoop(CancellationToken cancellationToken)
        {
            const int CHECK_INTERVAL_MS = 1000; // Check every 1 second instead of every frame

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Wait for refresh interval using 1-second delays (not per-frame yields)
                    int waitSeconds = Constants.GROWTHBOOK_REFRESH_INTERVAL_SECONDS;
                    while (waitSeconds > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(CHECK_INTERVAL_MS, cancellationToken);
                        waitSeconds--;
                    }

                    if (!cancellationToken.IsCancellationRequested && cache.ShouldRefresh())
                    {
                        await RefreshFeaturesFromApi();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper][FeatureFlags] Background refresh error: {ex.Message}");
            }
            finally
            {
                Debug.Log("[PlaySuper][FeatureFlags] Background refresh task completed");
            }
        }

        #endregion
    }
}