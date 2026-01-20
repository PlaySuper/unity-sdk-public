using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
// using Gpm.Communicator;
using Gpm.WebView;
using UnityEngine;
using PlaySuperUnity.FeatureFlags;

[assembly: InternalsVisibleTo("playsuper.unity.Runtime.Tests")]

namespace PlaySuperUnity
{
    public class PlaySuperUnitySDK : MonoBehaviour
    {
        private static PlaySuperUnitySDK _instance;
        private static string apiKey;
        private static string authToken;
        private static string baseUrl;
        private static ProfileData profile;
        private static bool isDev;
        private static bool enableAdvertisingId = false;
        private static bool hasTrackingPermission = false;

        // Remote flag system for server-side feature control
        private static string eventSingleUrlOverride;  // remote-provided single-event URL
        private static string eventBatchUrlOverride;   // remote-provided batch URL
        private static string psAnalyticsUrlOverride;
        private static bool remoteAdIdGate = true; // Gated by remote flags

        private static System.Threading.CancellationTokenSource flagsCts;
        private static IFeatureFlags featureFlags;
        private static DateTime lastFlagsFetchedAt = DateTime.MinValue;

        [System.Serializable]
        internal class PlayerIdentificationPayload
        {
            public string userId;
            public long timestamp;
            public string deviceId;
        }

        [System.Serializable]
        internal class SdkFlagsResponse
        {
            public string eventSingleUrl;
            public string eventBatchUrl;
            public bool enableAdId;
            public string psAnalyticsUrl;
            public int schemaVersion;
        }

        [System.Serializable]
        public class GrowthBookFeature
        {
            public string defaultValue;
            public object value;
        }

        [System.Serializable]
        public class GrowthBookResponse
        {
            public Dictionary<string, GrowthBookFeature> features;
        }

        // Private constructor to prevent instantiation from outside
        private PlaySuperUnitySDK() { }

        /// <summary>
        /// Initialize PlaySuper SDK
        /// </summary>
        /// <param name="_apiKey">Your API key</param>
        /// <param name="_isDev">Development mode</param>
        /// <param name="_enableAdvertisingId">Enable advertising ID collection. Default: FALSE for privacy compliance</param>
        public static void Initialize(string _apiKey, bool _isDev = false, bool _enableAdvertisingId = false)
        {
            // Clean up any existing instance first
            if (_instance != null)
            {
                Debug.LogWarning("[PlaySuper] SDK already initialized - disposing previous instance");
                Dispose();
            }

            Application.wantsToQuit += OnApplicationWantsToQuit;

            if (_instance == null)
            {
                // Initialize core SDK first
                string env = Environment.GetEnvironmentVariable("PROJECT_ENV") ?? "production";
                isDev = _isDev;
                baseUrl = (env == "development" || _isDev) ? Constants.devApiUrl : Constants.prodApiUrl;
                apiKey = _apiKey;
                enableAdvertisingId = _enableAdvertisingId;
                hasTrackingPermission = false; // Always start as false - game dev must explicitly enable

                // Load persisted auth token if available
                if (PlayerPrefs.HasKey("authToken"))
                {
                    authToken = PlayerPrefs.GetString("authToken");
                }

                // Create SDK GameObject
                GameObject sdkObject = new GameObject("PlaySuper");
                _instance = sdkObject.AddComponent<PlaySuperUnitySDK>();
                DontDestroyOnLoad(sdkObject);

                // Initialize MixPanel lifecycle manager AFTER SDK is ready
                MixPanelLifecycleManager.Initialize();

                LogPrivacySettings();
                Debug.Log("PlaySuperUnity initialized with API Key: " + apiKey);

                // Start flag fetching after core SDK is ready
                _ = FetchFlagsInitialAndSchedule();
            }

            // Handle previous session close event
            HandlePreviousSessionClose();

            // Send game open event
            MixPanelManager.SendEvent(Constants.MixpanelEvent.GAME_OPEN);
        }

        /// <summary>
        /// Enable advertising ID collection after user grants tracking permission
        /// Call this ONLY after the user has granted ATT permission (iOS) or appropriate consent
        /// </summary>
        /// <param name="callback">Optional callback when operation completes</param>
        public static void EnableAdvertisingIdCollection(Action<bool> callback = null)
        {
            enableAdvertisingId = true;
            hasTrackingPermission = true;

            LogPrivacySettings();

            Debug.Log("[PlaySuper] Advertising ID collection ENABLED by game developer");
            callback?.Invoke(true);
        }

        /// <summary>
        /// Disable advertising ID collection
        /// Call this when user revokes tracking permission or opts out
        /// </summary>
        public static void DisableAdvertisingIdCollection()
        {
            enableAdvertisingId = false;
            hasTrackingPermission = false;

            // Clear any cached advertising ID
            PlayerPrefs.DeleteKey("advertising_id");
            PlayerPrefs.DeleteKey("advertising_id_source");
            PlayerPrefs.DeleteKey("advertising_id_platform");
            PlayerPrefs.DeleteKey("advertising_id_timestamp");
            PlayerPrefs.Save();

            LogPrivacySettings();
            Debug.Log("[PlaySuper] Advertising ID collection DISABLED and cache cleared");
        }

        /// <summary>
        /// Check if advertising ID collection is currently enabled
        /// </summary>
        /// <returns>True if enabled and has tracking permission</returns>
        public static bool IsAdvertisingIdCollectionEnabled()
        {
            return enableAdvertisingId && hasTrackingPermission;
        }

        private static void HandlePreviousSessionClose()
        {
            if (
                PlayerPrefs.HasKey(Constants.lastCloseTimestampName)
                && PlayerPrefs.GetString(Constants.lastCloseDoneName) == "0"
            )
            {
                string timestamp = PlayerPrefs.GetString(Constants.lastCloseTimestampName);
                if (long.TryParse(timestamp, out long timestampLong))
                {
                    MixPanelManager.SendEvent(Constants.MixpanelEvent.GAME_CLOSE, timestampLong);
                    PlayerPrefs.SetString(Constants.lastCloseDoneName, "1");
                }
            }
        }

        public static PlaySuperUnitySDK Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("PlaySuperUnitySDK is not initialized.");
                }
                return _instance;
            }
        }

        static bool OnApplicationWantsToQuit()
        {
            Debug.Log("[PlaySuper] Application wants to quit - saving final state");

            // Save timestamp for next session
            PlayerPrefs.SetString(
                Constants.lastCloseTimestampName,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            );
            PlayerPrefs.SetString(Constants.lastCloseDoneName, "0");

            // Dispose resources to save any pending data
            MixPanelEventQueue.Dispose();
            MixPanelManager.Dispose();

            return true; // Allow quit
        }

        public async Task DistributeCoins(string coinId, int amount)
        {
            if (authToken == null)
            {
                TransactionsManager.AddTransaction(coinId, amount);
                Debug.Log("Transaction stored locally (no auth token)");
                return;
            }

            try
            {
                var client = new HttpClient();
                var jsonPayload = $@"{{""amount"": {amount}}}";
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var url = $"{baseUrl}/coins/{coinId}/distribute";

                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*")
                );
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("Authorization", $"Bearer {authToken}");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.Log("Coins distributed successfully: " + responseContent);
                }
                else
                {
                    Debug.LogError($"Error from DistributeCoins: {response.StatusCode}");
                    // IMPORTANT: Store locally on server error
                    TransactionsManager.AddTransaction(coinId, amount);
                    Debug.Log("Transaction stored locally due to server error");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Network error in DistributeCoins: {ex.Message}");
                // IMPORTANT: Store locally on network error
                TransactionsManager.AddTransaction(coinId, amount);
                Debug.Log("Transaction stored locally due to network error");
            }
        }

        private async Task SendPlayerIdentificationRequest()
        {
            if (string.IsNullOrEmpty(authToken) || profile == null)
            {
                Debug.LogWarning("[PlaySuper] Cannot send player identification request - missing auth token or profile");
                return;
            }

            try
            {
                var client = new HttpClient();


                // Build the payload with player and game information
                var payload = new PlayerIdentificationPayload
                {
                    userId = profile.id,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    deviceId = MixPanelManager.DeviceId
                };

                var jsonPayload = JsonUtility.ToJson(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var url = $"{GetResolvedPSAnalyticsUrl()}/events/identify-user";

                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.Log("[PlaySuper] Player identification request sent successfully: " + responseContent);
                }
                else
                {
                    Debug.LogWarning($"[PlaySuper] Player identification request failed: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper] Error sending player identification request: {ex.Message}");
            }
        }

        public async void OpenStore()
        {
            OpenStore(null, null, null);
        }

        public async void OpenStore(string token, string url = null, string utmContent = null)
        {
            MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_OPEN);
            Debug.Log("OpenStore: with token " + token);

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    Debug.Log("OpenStore: Processing provided token");
                    await ProcessTokenForUpfrontAuth(token);

                    Debug.Log("OpenStore: Token processed, showing authenticated store");
                    // Show store FIRST
                    WebView.ShowUrlPopupPositionSize(isDev, url, utmContent);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"OpenStore: Token validation failed - {ex.Message}");
                    // error handling
                }
            }
            else
            {
                Debug.Log("OpenStore: No token provided, showing store for callback");
                WebView.ShowUrlPopupPositionSize(isDev, url, utmContent);
            }
        }

        public static bool ValidateToken(string token)
        {
            // Delegate to TokenUtils
            return TokenUtils.ValidateToken(token);
        }

        // Common token processing logic
        private async Task ProcessTokenCommon(string token)
        {
            if (IsLoggedIn())
                return;

            authToken = token;
            profile = await ProfileManager.GetProfileData();

            await MixPanelManager.SendEvent(Constants.MixpanelEvent.PLAYER_IDENTIFY);

            // Send player identification POST request
            await SendPlayerIdentificationRequest();

            // Process pending transactions
            if (TransactionsManager.HasTransactions())
            {
                List<Transaction> transactions = TransactionsManager.GetTransactions();
                Dictionary<string, int> coinMap = new Dictionary<string, int>();
                foreach (Transaction t in transactions)
                {
                    if (coinMap.ContainsKey(t.coinId))
                    {
                        coinMap[t.coinId] += t.amount;
                    }
                    else
                    {
                        coinMap.Add(t.coinId, t.amount);
                    }
                }
                foreach (KeyValuePair<string, int> kvp in coinMap)
                {
                    Debug.Log("Distributing coins: " + kvp.Value + " of " + kvp.Key);
                    await DistributeCoins(kvp.Key, kvp.Value);
                }
                TransactionsManager.ClearTransactions();
            }
        }

        private async Task ProcessTokenForUpfrontAuth(string token)
        {
            await ProcessTokenCommon(token);
            // NO webview reload - store opens authenticated
        }

        internal async void OnTokenReceive(string _token)
        {
            await ProcessTokenCommon(_token);
            // Reload webview for callback scenarios
            GpmWebView.ExecuteJavaScript("window.location.reload()");
        }

        public async Task<List<CoinBalance>> GetBalance()
        {
            if (authToken == null)
            {
                var client = new HttpClient();

                CoinResponse coinData = null;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                HttpResponseMessage response = await client.GetAsync($"{baseUrl}/coins");

                if (response.IsSuccessStatusCode)
                {
                    string coinJson = await response.Content.ReadAsStringAsync();
                    coinData = JsonUtility.FromJson<CoinResponse>(coinJson);
                    List<Transaction> transactionList = GetLocalTransactions();
                    List<CoinBalance> balances = new List<CoinBalance>();
                    foreach (Coin c in coinData.data)
                    {
                        CoinBalance cb = new CoinBalance(c.id, c.name, c.url, 0);
                        balances.Add(cb);
                    }
                    foreach (Transaction t in transactionList)
                    {
                        foreach (CoinBalance cb in balances)
                        {
                            if (cb.id == t.coinId)
                            {
                                cb.amount += t.amount;
                            }
                        }
                    }

                    return balances;
                }
                else
                {
                    Debug.LogError($"Error in fetching coins for game: {response.StatusCode}");
                    return null;
                }
            }
            else
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

                // Get balance from PlaySuper API
                HttpResponseMessage response = await client.GetAsync($"{baseUrl}/player/funds");

                FundResponse fundsData = null;
                List<CoinBalance> balances = new List<CoinBalance>();
                if (response.IsSuccessStatusCode)
                {
                    string fundsJson = await response.Content.ReadAsStringAsync();
                    fundsData = JsonUtility.FromJson<FundResponse>(fundsJson);
                    if (fundsData.data != null)
                    {
                        foreach (PlayerCoin pc in fundsData.data)
                        {
                            CoinBalance cb = new CoinBalance(
                                pc.coinId,
                                pc.coin.name,
                                pc.coin.pictureUrl,
                                pc.balance
                            );
                            balances.Add(cb);
                        }
                    }

                    // Add balance from local transactions
                    List<Transaction> transactionList = GetLocalTransactions();
                    foreach (Transaction t in transactionList)
                    {
                        for (int i = 0; i < balances.Count; i++)
                        {
                            if (t.coinId == balances[i].id)
                            {
                                balances[i].amount += t.amount;
                            }
                        }
                    }

                    return balances;
                }
                else
                {
                    Debug.LogError($"Error from GetBalance: {response}");
                    return null;
                }
            }
        }

        public static bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(authToken) && profile != null;
        }

        internal static string GetApiKey()
        {
            return apiKey;
        }

        internal static string GetAuthToken()
        {
            return authToken;
        }

        public static void SetAuthToken(string token)
        {
            authToken = token;
            PlayerPrefs.SetString("authToken", token);
            PlayerPrefs.Save();
        }

        internal static ProfileData GetProfileData()
        {
            return profile;
        }

        internal static List<Transaction> GetLocalTransactions()
        {
            string json = PlayerPrefs.GetString("transactions");
            TransactionListWrapper wrapper = JsonUtility.FromJson<TransactionListWrapper>(json);
            return wrapper.transactions;
        }

        internal static string GetBaseUrl()
        {
            return baseUrl;
        }

        public static AdvertisingIdResult GetAdvertisingId()
        {
            if (!enableAdvertisingId)
            {
                return new AdvertisingIdResult("", "disabled", GetCurrentPlatform());
            }

            try
            {
                return GetAdsId.GetAdvertisingId();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to get advertising ID: {ex.Message}");
                return new AdvertisingIdResult("", "error", GetCurrentPlatform());
            }
        }

        // Keep this for backward compatibility if needed
        public static string GetAndroidAdvertiserId()
        {
            var result = GetAdvertisingId();
            return result.id;
        }

        internal static bool IsAdvertisingIdEnabled()
        {
            return enableAdvertisingId;
        }

        internal static bool HasTrackingPermission()
        {
            // Simply return our internal state - game dev controls this
            return hasTrackingPermission;
        }

        /// <summary>
        /// Check if we should collect advertising ID based on all privacy requirements
        /// </summary>
        /// <returns>True if advertising ID collection is allowed</returns>
        public static bool ShouldAllowAdvertisingIdCollection()
        {
            // Primary setting check
            if (!enableAdvertisingId)
            {
                Debug.Log("[PlaySuper] Advertising ID disabled by configuration");
                return false;
            }

            // Remote flag gate check - use the actual flag value we fetched
            if (!remoteAdIdGate)
            {
                Debug.Log("[PlaySuper] Advertising ID disabled by remote flag (sdk_enable_ad_id: false)");

                return false;
            }

            // Check if game developer has granted permission
            if (!hasTrackingPermission)
            {
                Debug.Log("[PlaySuper] Advertising ID disabled - tracking permission not granted by game developer");
                return false;
            }

            Debug.Log("[PlaySuper] Advertising ID collection ALLOWED - all checks passed");

            return true;
        }

        void OnDestroy()
        {
            Debug.Log("[PlaySuper] SDK destroying - cleaning up resources");

            // Dispose MixPanel resources
            MixPanelEventQueue.Dispose();

            // Unsubscribe from application events
            Application.wantsToQuit -= OnApplicationWantsToQuit;

            // Clear static references to help GC
            _instance = null;
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Debug.Log("[PlaySuper] App pausing - saving state");
                MixPanelEventQueue.Dispose();
            }
        }

        // Add explicit disposal method for manual cleanup
        public static void Dispose()
        {
            if (_instance != null)
            {
                try
                {
                    flagsCts?.Cancel();
                    featureFlags?.Dispose();
                }
                catch { }
                DestroyImmediate(_instance.gameObject);
            }
        }

        // Add these public static methods for GetAdsId to use
        public static bool IsAdvertisingIdEnabledStatic()
        {
            return enableAdvertisingId;
        }

        public static bool HasTrackingPermissionStatic()
        {
            return hasTrackingPermission;
        }

        private static void LogPrivacySettings()
        {
            if (enableAdvertisingId && hasTrackingPermission)
            {
                Debug.Log("[PlaySuper] Privacy: Advertising ID collection ENABLED with tracking permission");
            }
            else if (enableAdvertisingId && !hasTrackingPermission)
            {
                Debug.LogWarning("[PlaySuper] Privacy: Advertising ID collection ENABLED but tracking permission NOT granted");
            }
            else
            {
                Debug.Log("[PlaySuper] Privacy: Advertising ID collection DISABLED");
            }
        }

        private static string GetCurrentPlatform()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return "ios";
#elif UNITY_ANDROID && !UNITY_EDITOR
            return "android";
#elif UNITY_EDITOR
            return "editor";
#else
            return "unknown";
#endif
        }

        internal static string GetResolvedEventBatchUrl()
        {
            return featureFlags?.GetEventBatchUrl() ?? Constants.MIXPANEL_URL_BATCH;
        }

        internal static string GetResolvedEventSingleUrl()
        {
            return featureFlags?.GetEventSingleUrl() ?? Constants.MIXPANEL_URL;
        }

        internal static string GetResolvedPSAnalyticsUrl()
        {
            return featureFlags?.GetPSAnalyticsUrl() ?? Constants.PS_ANALYTICS_URL;
        }

        private static bool IsValidHttpsUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("https://")) return false;
            try { var _ = new Uri(url); return true; } catch { return false; }
        }

        private static void ApplyFlags(SdkFlagsResponse flags)
        {
            if (flags == null) return;

            string prevBatch = eventBatchUrlOverride;
            string prevSingle = eventSingleUrlOverride;
            string prevPsAnalytics = psAnalyticsUrlOverride;
            bool prevRemoteAdIdGate = remoteAdIdGate;

            // Only accept valid https URLs
            eventSingleUrlOverride = IsValidHttpsUrl(flags.eventSingleUrl) ? flags.eventSingleUrl : null;
            eventBatchUrlOverride = IsValidHttpsUrl(flags.eventBatchUrl) ? flags.eventBatchUrl : null;
            psAnalyticsUrlOverride = IsValidHttpsUrl(flags.psAnalyticsUrl) ? flags.psAnalyticsUrl : null;

            remoteAdIdGate = flags.enableAdId; // This should work
            lastFlagsFetchedAt = DateTime.UtcNow;

            // Log changes with more detail
            if (prevBatch != eventBatchUrlOverride || prevSingle != eventSingleUrlOverride || prevRemoteAdIdGate != remoteAdIdGate || prevPsAnalytics != psAnalyticsUrlOverride)
            {
                Debug.Log($"[PlaySuper][Flags] Updated: batchUrl={eventBatchUrlOverride ?? "default"}, singleUrl={eventSingleUrlOverride ?? "default"}, psAnalyticsUrl={psAnalyticsUrlOverride ?? "default"}, enableAdId={remoteAdIdGate} (was {prevRemoteAdIdGate})");
            }

            // Always log the current flag state
            Debug.Log($"[PlaySuper][Flags] Current state: remoteAdIdGate={remoteAdIdGate}, localEnable={enableAdvertisingId}, trackingPermission={hasTrackingPermission}");
        }

        private static async Task FetchFlagsInitialAndSchedule()
        {
            var clientKey = "sdk-7lLklUP0lUDKF2Q8";
            Debug.Log("[PlaySuper][Flags] Starting flag initialization...");

            featureFlags = new FeatureFlags.FeatureFlagsService();
            await featureFlags.Initialize(clientKey);

            Debug.Log("[PlaySuper][Flags] Flag service initialized, getting flag values...");

            // Use the service instead of CDN fetch
            var eventSingleUrl = featureFlags.GetEventSingleUrl();
            var eventBatchUrl = featureFlags.GetEventBatchUrl();
            var enableAdId = featureFlags.IsAdIdEnabled();
            var psAnalyticsUrl = featureFlags.GetPSAnalyticsUrl();

            // Apply the flags
            var flags = new SdkFlagsResponse
            {
                eventSingleUrl = eventSingleUrl,
                eventBatchUrl = eventBatchUrl,
                enableAdId = enableAdId,
                psAnalyticsUrl = psAnalyticsUrl,
                schemaVersion = 1
            };

            ApplyFlags(flags);
            Debug.Log("[PlaySuper][Flags] Initial flag fetch completed");
        }
    }

    internal class MixPanelManager
    {
        private static string _deviceId;
        private static string _advertisingId;
        private static string _advertisingIdSource;

        internal static string DeviceId
        {
            get
            {
                if (!string.IsNullOrEmpty(_deviceId))
                    return _deviceId;
                if (PlayerPrefs.HasKey(Constants.deviceIdName))
                {
                    _deviceId = PlayerPrefs.GetString(Constants.deviceIdName);
                }
                if (string.IsNullOrEmpty(_deviceId))
                {
                    DeviceId = Guid.NewGuid().ToString();
                }
                return _deviceId;
            }
            set
            {
                _deviceId = value;
                PlayerPrefs.SetString(Constants.deviceIdName, value);
            }
        }
        internal static string AdvertisingId
        {
            get
            {
                // Check if advertising ID collection is allowed (includes remote flag)
                if (!PlaySuperUnitySDK.ShouldAllowAdvertisingIdCollection())
                {
                    return "";
                }

                if (!string.IsNullOrEmpty(_advertisingId))
                    return _advertisingId;

                // Get fresh advertising ID with source
                var result = PlaySuperUnitySDK.GetAdvertisingId();
                _advertisingId = result.id;
                _advertisingIdSource = result.source;

                return _advertisingId ?? "";
            }
        }

        internal static string AdvertisingIdSource
        {
            get
            {
                if (string.IsNullOrEmpty(_advertisingIdSource))
                {
                    // Try to get from cache
                    if (PlayerPrefs.HasKey("advertising_id_source"))
                    {
                        _advertisingIdSource = PlayerPrefs.GetString("advertising_id_source");
                    }
                    else
                    {
                        // Trigger a fresh fetch
                        var _ = AdvertisingId; // This will populate both ID and source
                    }
                }
                return _advertisingIdSource ?? "";
            }
        }

        internal static string AdvertisingIdPlatform
        {
            get
            {
                // Get from cache or fresh
                if (PlayerPrefs.HasKey("advertising_id_platform"))
                {
                    return PlayerPrefs.GetString("advertising_id_platform");
                }

                // Get fresh platform info
                var result = PlaySuperUnitySDK.GetAdvertisingId();
                return result.platform;
            }
        }

        private static string userId
        {
            get
            {
                if (PlaySuperUnitySDK.IsLoggedIn())
                {
                    return PlaySuperUnitySDK.GetProfileData().id;
                }
                else
                    return null;
            }
        }

        private static GameData gameData;

        internal static async Task SendEvent(string eventName, long timestamp = 0)
        {
            try
            {
                // Capture actual event time immediately
                long actualEventTime =
                    timestamp != 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (gameData == null)
                {
                    gameData = await GameManager.GetGameData();
                }

                // Get IP address (with fallback)
                string ipAddress = await NetworkUtils.GetPublicIPAddress();

                // Build properties list
                var properties = new List<string>
                {
                    $@"""$device_id"": ""{DeviceId}""",
                    $@"""time"": {actualEventTime}",
                    $@"""$insert_id"": ""{Guid.NewGuid()}""",
                    $@"""$ip"": ""{ipAddress}""",
                    $@"""gameId"": ""{gameData.id}""",
                    $@"""gameName"": ""{gameData.name}""",
                    $@"""studioId"": ""{gameData.studioId}""",
                    $@"""studioOrganizationId"": ""{gameData.studio.organizationId}""",
                    $@"""studioName"": ""{gameData.studio.organization.name}""",
                    $@"""studioHandle"": ""{gameData.studio.organization.handle}""",
                };

                // Before adding advertising ID to properties
                if (PlaySuperUnitySDK.IsAdvertisingIdEnabled())
                {
                    string adId = AdvertisingId;
                    if (!string.IsNullOrEmpty(adId))
                    {
                        properties.Add($@"""advertising_id"": ""{adId}""");

                        // Include the source and platform only if ad ID is enabled
                        string adSource = AdvertisingIdSource;
                        if (!string.IsNullOrEmpty(adSource))
                        {
                            properties.Add($@"""advertising_id_source"": ""{adSource}""");
                        }

                        string adPlatform = AdvertisingIdPlatform;
                        if (!string.IsNullOrEmpty(adPlatform))
                        {
                            properties.Add($@"""advertising_id_platform"": ""{adPlatform}""");
                        }
                    }
                }

                // Add user ID only if available
                if (!string.IsNullOrEmpty(userId))
                {
                    properties.Add($@"""$user_id"": ""{userId}""");
                }

                // Create clean payload
                var mixPanelPayload =
                    $@"{{
    ""event_name"": ""{eventName}"",
    ""properties"": {{
        {string.Join(",\n        ", properties)}
    }}
}}";

                // Always queue - let the lifecycle manager handle processing
                MixPanelEventQueue.EnqueueEvent(eventName, actualEventTime, mixPanelPayload);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in SendEvent: {e.Message}");
                // Create minimal fallback payload
                // CreateFallbackEvent(eventName, timestamp);
            }
        }

        private static void CreateFallbackEvent(string eventName, long timestamp)
        {
            long fallbackTime =
                timestamp != 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fallbackPayload =
                $@"{{
    ""event_name"": ""{eventName}"",
    ""properties"": {{
        ""time"": {fallbackTime},
        ""$device_id"": ""{DeviceId}"",
        ""$insert_id"": ""{Guid.NewGuid()}""
    }}
}}";
            MixPanelEventQueue.EnqueueEvent(eventName, fallbackTime, fallbackPayload);
        }

        internal static void Dispose()
        {
            // Clear cached data
            gameData = null;
            _deviceId = null;
            _advertisingId = null;

            Debug.Log("[MixPanel] Manager disposed");
        }
    }
}
