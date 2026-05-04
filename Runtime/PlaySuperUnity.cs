using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
// using Gpm.Communicator;
using Gpm.WebView;
using UnityEngine;
using UnityEngine.Networking;
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

        /// <summary>
        /// Event fired when the store WebView is closed (by user or programmatically)
        /// </summary>
        public static event Action OnStoreClosed;

        /// <summary>
        /// Event fired when the store WebView is opened
        /// </summary>
        public static event Action OnStoreOpened;

        /// <summary>
        /// Internal method to invoke OnStoreClosed event from WebView
        /// Also fetches any new SDK transactions that occurred during the store session
        /// </summary>
        internal static void NotifyStoreClosed()
        {
            OnStoreClosed?.Invoke();

            // Fetch SDK transactions after store closes (purchases may have happened)
            if (IsLoggedIn() && SdkTransactionSyncManager.HasVisitedStore())
            {
                Debug.Log("[PlaySuper] Store closed - fetching SDK transactions");
                _ = FetchSdkTransactionsAfterAuth();
            }
            else
            {
                Debug.Log($"[PlaySuper] Store closed - skipping transaction fetch (loggedIn={IsLoggedIn()}, visitedStore={SdkTransactionSyncManager.HasVisitedStore()})");
            }
        }

        /// <summary>
        /// Internal method to invoke OnStoreOpened event from WebView
        /// </summary>
        internal static void NotifyStoreOpened()
        {
            OnStoreOpened?.Invoke();
        }

        [System.Serializable]
        internal class PlayerIdentificationPayload
        {
            public string userId;
            public long timestamp;
            public string deviceId;
        }

        [System.Serializable]
        internal class CreatePlayerPayload
        {
            public string uuid;
        }

        [System.Serializable]
        public class CreatePlayerData
        {
            public string message;
            public string playerId;
        }

        [System.Serializable]
        public class CreatePlayerResponse
        {
            public CreatePlayerData data;
            public int statusCode;
            public string message;
            public string requestId;
            public string timestamp;
        }

        [System.Serializable]
        public class FederatedLoginResponse
        {
            public string message;
            public string access_token;
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
                bool hadExistingToken = false;
                if (PlayerPrefs.HasKey("authToken"))
                {
                    authToken = PlayerPrefs.GetString("authToken");
                    hadExistingToken = !string.IsNullOrEmpty(authToken);
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

                // If user was previously authenticated, fetch SDK transactions
                if (hadExistingToken)
                {
                    _ = FetchSdkTransactionsAfterAuth();
                }
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
            PlayerPrefsSaveManager.ScheduleSave();

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

        #region User Properties

        /// <summary>
        /// Set persistent user properties that will be sent with every event.
        /// Use this for attributes that describe the user (VIP level, cohort, A/B test group, etc.)
        /// Properties are merged with existing ones - call ClearUserProperties() to reset.
        /// </summary>
        /// <param name="properties">Dictionary of property names and values (string, int, float, bool supported)</param>
        /// <example>
        /// PlaySuperUnitySDK.SetUserProperties(new Dictionary&lt;string, object&gt;
        /// {
        ///     { "vip_level", 3 },
        ///     { "lifetime_purchases", 5 },
        ///     { "ab_test_group", "variant_b" }
        /// });
        /// </example>
        public static void SetUserProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                Debug.LogWarning("[PlaySuper] SetUserProperties called with null or empty properties");
                return;
            }

            MixPanelManager.SetUserProperties(properties);
            Debug.Log($"[PlaySuper] User properties set: {properties.Count} properties");
        }

        /// <summary>
        /// Clear all user properties. Call this on logout or when user context changes.
        /// </summary>
        public static void ClearUserProperties()
        {
            MixPanelManager.ClearUserProperties();
            Debug.Log("[PlaySuper] User properties cleared");
        }

        /// <summary>
        /// Get current user properties (for debugging)
        /// </summary>
        /// <returns>Copy of current user properties dictionary</returns>
        public static Dictionary<string, object> GetUserProperties()
        {
            return MixPanelManager.GetUserProperties();
        }

        #endregion

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
            PlayerPrefsSaveManager.ForceSaveImmediate(); // Flush all pending saves before quit

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
                var jsonPayload = $@"{{""amount"": {amount}}}";
                var url = $"{baseUrl}/coins/{coinId}/distribute";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                using (var webRequest = new UnityWebRequest(url, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("Coins distributed successfully: " + webRequest.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogError($"Error from DistributeCoins: {webRequest.responseCode}");
                        // IMPORTANT: Store locally on server error
                        TransactionsManager.AddTransaction(coinId, amount);
                        Debug.Log("Transaction stored locally due to server error");
                    }
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

        public async Task DeductCoins(string coinId, int amount)
        {
            if (authToken == null)
            {
                TransactionsManager.AddTransaction(coinId, amount, "deduct");
                Debug.Log("Deduction stored locally (no auth token)");
                return;
            }

            try
            {
                var jsonPayload = $@"{{""amount"": {amount}}}";
                var url = $"{baseUrl}/coins/{coinId}/deduct";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                using (var webRequest = new UnityWebRequest(url, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("Coins deducted successfully: " + webRequest.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogError($"Error from DeductCoins: {webRequest.responseCode}");
                        // IMPORTANT: Store locally on server error
                        TransactionsManager.AddTransaction(coinId, amount, "deduct");
                        Debug.Log("Deduction stored locally due to server error");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Network error in DeductCoins: {ex.Message}");
                // IMPORTANT: Store locally on network error
                TransactionsManager.AddTransaction(coinId, amount, "deduct");
                Debug.Log("Deduction stored locally due to network error");
            }
        }

        public async Task<CreatePlayerResponse> CreatePlayerWithUuid(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                Debug.LogError("[PlaySuper] CreatePlayerWithUuid requires a valid uuid");
                return null;
            }

            try
            {
                var payload = new CreatePlayerPayload { uuid = uuid };
                var jsonPayload = JsonUtility.ToJson(payload);
                var url = $"{baseUrl}/player/create-with-uuid";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                using (var webRequest = new UnityWebRequest(url, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    string responseContent = webRequest.downloadHandler.text;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var createResponse = JsonUtility.FromJson<CreatePlayerResponse>(responseContent);
                        if (createResponse?.data != null)
                        {
                            Debug.Log("[PlaySuper] Player created successfully: " + createResponse.data.playerId);
                        }
                        else
                        {
                            Debug.LogWarning("[PlaySuper] Player created but response missing data: " + responseContent);
                        }
                        return createResponse;
                    }

                    Debug.LogError($"Error from CreatePlayerWithUuid: {webRequest.responseCode} - {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Network error in CreatePlayerWithUuid: {ex.Message}");
                return null;
            }
        }

        public async Task<FederatedLoginResponse> LoginFederatedByStudio(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                Debug.LogError("[PlaySuper] LoginFederatedByStudio requires a valid uuid");
                return null;
            }

            try
            {
                var payload = new CreatePlayerPayload { uuid = uuid };
                var jsonPayload = JsonUtility.ToJson(payload);
                var url = $"{baseUrl}/player/login/federatedByStudio";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                using (var webRequest = new UnityWebRequest(url, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    string responseContent = webRequest.downloadHandler.text;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var loginResponse = JsonUtility.FromJson<FederatedLoginResponse>(responseContent);
                        if (!string.IsNullOrEmpty(loginResponse?.access_token))
                        {
                            authToken = loginResponse.access_token;
                            PlayerPrefs.SetString("authToken", authToken);
                            PlayerPrefsSaveManager.ScheduleSave();
                            profile = await ProfileManager.GetProfileData();
                            Debug.Log("[PlaySuper] Federated login succeeded");
                        }
                        else
                        {
                            Debug.LogWarning("[PlaySuper] Federated login response missing access_token");
                        }
                        return loginResponse;
                    }

                    Debug.LogError($"Error from LoginFederatedByStudio: {webRequest.responseCode} - {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Network error in LoginFederatedByStudio: {ex.Message}");
                return null;
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
                // Build the payload with player and game information
                var payload = new PlayerIdentificationPayload
                {
                    userId = profile.id,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    deviceId = MixPanelManager.DeviceId
                };

                var jsonPayload = JsonUtility.ToJson(payload);
                var url = $"{GetResolvedPSAnalyticsUrl()}/events/identify-user";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                using (var webRequest = new UnityWebRequest(url, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[PlaySuper] Player identification request sent successfully: " + webRequest.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper] Player identification request failed: {webRequest.responseCode} - {webRequest.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper] Error sending player identification request: {ex.Message}");
            }
        }

        public void OpenStore()
        {
            OpenStore(null, null);
        }

        public void OpenStore(string url = null, string utmContent = null)
        {
            MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_OPEN);

            if (!IsLoggedIn())
            {
                Debug.LogWarning("[PlaySuper] Opening store without valid auth token - user may need to login");
            }
            else
            {
                // Mark store as visited on the server and locally (fire and forget)
                _ = MarkStoreVisitedAsync();
            }

            Debug.Log("[PlaySuper] OpenStore: opening store");
            WebView.ShowUrlFullScreen(isDev, url, utmContent);
        }

        /// <summary>
        /// Mark the store as visited on the server (enables SDK transaction syncing)
        /// Called automatically when OpenStore() is invoked while logged in.
        /// </summary>
        private static async Task MarkStoreVisitedAsync()
        {
            // Check local cache first - skip API call if already visited
            if (SdkTransactionSyncManager.HasVisitedStore())
            {
                Debug.Log("[PlaySuper] Store already marked as visited (local cache)");
                return;
            }

            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[PlaySuper] Cannot mark store visited - user not authenticated");
                return;
            }

            try
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes("{}");
                using (var webRequest = new UnityWebRequest($"{baseUrl}/player/mark-store-visited", "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseJson = webRequest.downloadHandler.text;
                        MarkStoreVisitedResponse wrapper = JsonUtility.FromJson<MarkStoreVisitedResponse>(responseJson);

                        if (wrapper?.data != null && wrapper.data.success)
                        {
                            // Update local cache
                            SdkTransactionSyncManager.SetHasVisitedStore(true);
                            Debug.Log($"[PlaySuper] Store marked as visited (alreadyVisited: {wrapper.data.alreadyVisited})");

                            // If this is the first visit, fetch transactions in background
                            if (!wrapper.data.alreadyVisited)
                            {
                                _ = FetchSdkTransactionsAfterAuth();
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper] Failed to mark store visited: {webRequest.responseCode}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Error marking store visited: {e.Message}");
                // Non-blocking - don't fail store open due to this
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

            if (profile == null)
            {
                Debug.LogWarning("[PlaySuper] Failed to fetch profile after login - some features may be unavailable");
            }

            await MixPanelManager.SendEvent(Constants.MixpanelEvent.PLAYER_IDENTIFY);

            // Send player identification POST request (guards against null profile internally)
            await SendPlayerIdentificationRequest();

            // Process pending transactions
            if (TransactionsManager.HasTransactions())
            {
                List<Transaction> transactions = TransactionsManager.GetTransactions();
                Dictionary<string, int> distributeCoinMap = new Dictionary<string, int>();
                Dictionary<string, int> deductCoinMap = new Dictionary<string, int>();
                foreach (Transaction t in transactions)
                {
                    var targetMap = t.type == "deduct" ? deductCoinMap : distributeCoinMap;
                    if (targetMap.ContainsKey(t.coinId))
                    {
                        targetMap[t.coinId] += t.amount;
                    }
                    else
                    {
                        targetMap.Add(t.coinId, t.amount);
                    }
                }
                foreach (KeyValuePair<string, int> kvp in distributeCoinMap)
                {
                    Debug.Log("Distributing coins: " + kvp.Value + " of " + kvp.Key);
                    await DistributeCoins(kvp.Key, kvp.Value);
                }
                foreach (KeyValuePair<string, int> kvp in deductCoinMap)
                {
                    Debug.Log("Deducting coins: " + kvp.Value + " of " + kvp.Key);
                    await DeductCoins(kvp.Key, kvp.Value);
                }
                TransactionsManager.ClearTransactions();
            }

            // Fetch SDK transactions (purchase debits, refund credits) after authentication
            // This allows games to sync transaction state on login
            _ = FetchSdkTransactionsAfterAuth();
        }

        /// <summary>
        /// Internal method to fetch SDK transactions after authentication.
        /// Runs in background and fires OnSdkTransactionsReceived event if transactions exist.
        /// </summary>
        private static async Task FetchSdkTransactionsAfterAuth()
        {
            try
            {
                var transactions = await FetchSdkTransactions();
                if (transactions != null && transactions.Count > 0)
                {
                    Debug.Log($"[PlaySuper] {transactions.Count} SDK transactions available for processing after auth");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to fetch SDK transactions after auth: {e.Message}");
                // Non-blocking - don't fail auth flow due to transaction sync issues
            }
        }

        private async Task ProcessTokenForUpfrontAuth(string token)
        {
            await ProcessTokenCommon(token);
            // NO webview reload - store opens authenticated
        }

        internal async void OnTokenReceive(string _token)
        {
            try
            {
                await ProcessTokenCommon(_token);
                // Reload webview for callback scenarios
                GpmWebView.ExecuteJavaScript("window.location.reload()");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper] Error processing token: {ex.Message}");
            }
        }

        public async Task<List<CoinBalance>> GetBalance()
        {
            if (authToken == null)
            {
                using (var webRequest = UnityWebRequest.Get($"{baseUrl}/coins"))
                {
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string coinJson = webRequest.downloadHandler.text;
                        CoinResponse coinData = JsonUtility.FromJson<CoinResponse>(coinJson);
                        if (coinData?.data == null)
                        {
                            Debug.LogError("[PlaySuper] Failed to parse coin data");
                            return null;
                        }
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
                        Debug.LogError($"Error in fetching coins for game: {webRequest.responseCode}");
                        return null;
                    }
                }
            }
            else
            {
                using (var webRequest = UnityWebRequest.Get($"{baseUrl}/player/funds"))
                {
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    List<CoinBalance> balances = new List<CoinBalance>();
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string fundsJson = webRequest.downloadHandler.text;
                        FundResponse fundsData = JsonUtility.FromJson<FundResponse>(fundsJson);
                        if (fundsData?.data != null)
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
                        Debug.LogError($"Error from GetBalance: {webRequest.responseCode}");
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Fetch a curated list by type and name
        /// </summary>
        /// <param name="type">Type of curated list (Products or Rewards)</param>
        /// <param name="listName">Name of the curated list (e.g., "homepage_featured", "daily_deals")</param>
        /// <param name="coinId">Coin ID for pricing calculations</param>
        /// <param name="version">Optional API version for rewards (e.g., "2.0.0" for enhanced response)</param>
        /// <returns>CuratedListResponse containing products or rewards based on type</returns>
        public async Task<CuratedListResponse> GetCuratedList(
            CuratedListType type,
            string listName,
            string coinId,
            string version = null)
        {
            return await CuratedListService.GetCuratedListAsync(type, listName, coinId, apiKey, baseUrl, version);
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

        public static async void SetAuthToken(string token)
        {
            try
            {
                authToken = token;
                PlayerPrefs.SetString("authToken", token);
                PlayerPrefsSaveManager.ScheduleSave();
                // Fetch and set the profile for this token
                profile = await ProfileManager.GetProfileData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper] Error setting auth token: {ex.Message}");
            }
        }

        internal static ProfileData GetProfileData()
        {
            return profile;
        }

        /// <summary>
        /// Update player profile fields (firstName, lastName, gender, dateOfBirth, email, phoneNumber).
        /// All parameters are optional - only provided fields will be updated.
        /// </summary>
        /// <param name="firstName">Player's first name</param>
        /// <param name="lastName">Player's last name</param>
        /// <param name="gender">Gender: "MALE", "FEMALE", or "OTHER"</param>
        /// <param name="dateOfBirth">Date of birth in ISO 8601 format (e.g., "1990-01-01")</param>
        /// <param name="email">Email address</param>
        /// <param name="phoneNumber">Phone number in E.164 format (e.g., "+1234567890")</param>
        /// <returns>Updated PlayerProfileData on success, null on failure</returns>
        public static async Task<PlayerProfileData> UpdatePlayerProfile(
            string firstName = null,
            string lastName = null,
            string gender = null,
            string dateOfBirth = null,
            string email = null,
            string phoneNumber = null)
        {
            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[PlaySuper] Cannot update profile - user not authenticated");
                return null;
            }

            // Validate gender enum if provided
            if (!string.IsNullOrEmpty(gender) && gender != "MALE" && gender != "FEMALE" && gender != "OTHER")
            {
                Debug.LogError($"[PlaySuper] Invalid gender value: {gender}. Must be MALE, FEMALE, or OTHER");
                return null;
            }

            try
            {
                // Build JSON manually to only include non-null fields
                // JsonUtility serializes null strings as empty strings, which fail API validation
                var jsonParts = new List<string>();
                if (!string.IsNullOrEmpty(firstName))
                    jsonParts.Add($"\"firstName\":\"{firstName}\"");
                if (!string.IsNullOrEmpty(lastName))
                    jsonParts.Add($"\"lastName\":\"{lastName}\"");
                if (!string.IsNullOrEmpty(gender))
                    jsonParts.Add($"\"gender\":\"{gender}\"");
                if (!string.IsNullOrEmpty(dateOfBirth))
                    jsonParts.Add($"\"dateOfBirth\":\"{dateOfBirth}\"");
                if (!string.IsNullOrEmpty(email))
                    jsonParts.Add($"\"email\":\"{email}\"");
                if (!string.IsNullOrEmpty(phoneNumber))
                    jsonParts.Add($"\"phoneNumber\":\"{phoneNumber}\"");

                string jsonBody = "{" + string.Join(",", jsonParts) + "}";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                using (var webRequest = new UnityWebRequest($"{baseUrl}/player/gcommerce/profile", "PATCH"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseJson = webRequest.downloadHandler.text;
                        PlayerProfileResponse wrapper = JsonUtility.FromJson<PlayerProfileResponse>(responseJson);

                        if (wrapper?.data != null)
                        {
                            Debug.Log($"[PlaySuper] Player profile updated successfully");
                            return wrapper.data;
                        }
                        else
                        {
                            Debug.LogError("[PlaySuper] Invalid response format from profile update");
                            return null;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PlaySuper] Error updating profile: {webRequest.responseCode} - {webRequest.downloadHandler.text}");
                        return null;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlaySuper] Exception updating profile: {e.Message}");
                return null;
            }
        }

        internal static List<Transaction> GetLocalTransactions()
        {
            string json = PlayerPrefs.GetString("transactions", "");
            if (string.IsNullOrEmpty(json))
            {
                return new List<Transaction>();
            }
            TransactionListWrapper wrapper = JsonUtility.FromJson<TransactionListWrapper>(json);
            return wrapper?.transactions ?? new List<Transaction>();
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

            // Stop transaction polling and reset static flag
            WebViewManager.StopTransactionPolling();

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
                PlayerPrefsSaveManager.ForceSaveImmediate(); // Flush any pending debounced saves
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
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlaySuper] Error during dispose: {ex.Message}");
                }
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
            return featureFlags?.GetEventBatchUrl() ?? Constants.PS_ANALYTICS_BATCH_URL;
        }

        internal static string GetResolvedEventSingleUrl()
        {
            return featureFlags?.GetEventSingleUrl() ?? Constants.PS_ANALYTICS_EVENT_URL;
        }

        internal static string GetResolvedPSAnalyticsUrl()
        {
            return featureFlags?.GetPSAnalyticsUrl() ?? Constants.PS_ANALYTICS_URL;
        }

        private static bool IsValidHttpsUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("https://")) return false;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
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
            try
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
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper][Flags] Failed to initialize feature flags: {ex.Message}");
            }
        }

        #region SDK Transaction Sync

        /// <summary>
        /// Event fired when new SDK transactions are available for the game to process.
        /// The game should handle these transactions (e.g., update local currency, show notifications)
        /// and then call CommitSdkTransactions() to mark them as processed.
        /// </summary>
        public static event Action<List<SdkTransaction>> OnSdkTransactionsReceived;

        /// <summary>
        /// Handles a realtime transaction notification from the WebView.
        /// Fetches the actual transaction data from the server and fires OnSdkTransactionsReceived.
        /// </summary>
        internal static void HandleRealtimeTransaction()
        {
            Debug.Log("[PlaySuper] Realtime transaction notification received from store");
            _ = FetchSdkTransactionsAfterAuth();
        }

        /// <summary>
        /// Fetch SDK transactions (purchase debits and refund credits) from the server.
        /// Requires authenticated user and that the player has visited the store.
        /// Automatically stores transactions locally and fires OnSdkTransactionsReceived.
        /// </summary>
        /// <returns>List of transactions that need processing, or null on error/not eligible</returns>
        public static async Task<List<SdkTransaction>> FetchSdkTransactions()
        {
            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[PlaySuper] Cannot fetch SDK transactions - user not authenticated");
                return null;
            }

            // Check local cache - skip API call if player hasn't visited store
            if (!SdkTransactionSyncManager.HasVisitedStore())
            {
                Debug.Log("[PlaySuper] Skipping SDK transaction fetch - player hasn't visited store yet");
                return new List<SdkTransaction>();
            }

            try
            {
                using (var webRequest = UnityWebRequest.Get($"{baseUrl}/player/sdk-transactions"))
                {
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string json = webRequest.downloadHandler.text;
                        SdkTransactionsResponse wrapper = JsonUtility.FromJson<SdkTransactionsResponse>(json);
                        var data = wrapper?.data;

                        // Update local store visit flag based on server state
                        if (data != null)
                        {
                            SdkTransactionSyncManager.SetHasVisitedStore(data.hasVisitedStore);
                        }

                        if (data != null && data.transactions != null && data.transactions.Count > 0)
                        {
                            // Store transactions locally for persistence
                            SdkTransactionSyncManager.AddPendingTransactions(data.transactions);

                            Debug.Log($"[PlaySuper] Fetched {data.transactions.Count} SDK transactions");

                            // Fire event for game to handle
                            OnSdkTransactionsReceived?.Invoke(data.transactions);

                            // Track analytics event for transactions fetched
                            _ = TrackTransactionsFetched(data.transactions);

                            return data.transactions;
                        }
                        else
                        {
                            Debug.Log("[PlaySuper] No new SDK transactions to sync");
                            return new List<SdkTransaction>();
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PlaySuper] Error fetching SDK transactions: {webRequest.responseCode}");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Exception fetching SDK transactions: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get pending SDK transactions from local storage.
        /// Use this to retrieve transactions that were fetched but not yet committed.
        /// </summary>
        /// <returns>List of pending transactions</returns>
        public static List<SdkTransaction> GetPendingSdkTransactions()
        {
            return SdkTransactionSyncManager.GetPendingTransactions();
        }

        /// <summary>
        /// Check if there are pending SDK transactions to process.
        /// </summary>
        /// <returns>True if there are unprocessed transactions</returns>
        public static bool HasPendingSdkTransactions()
        {
            return SdkTransactionSyncManager.HasPendingTransactions();
        }

        /// <summary>
        /// Check if the player has visited the PlaySuper store (from local cache).
        /// SDK transaction syncing is only enabled for players who have opened the store at least once.
        /// </summary>
        /// <returns>True if player has visited the store</returns>
        public static bool HasVisitedStore()
        {
            return SdkTransactionSyncManager.HasVisitedStore();
        }

        /// <summary>
        /// Commit SDK transactions after the game has processed them.
        /// This marks transactions as synced on the server and removes them from local storage.
        /// Call this after your game has successfully handled the transactions (e.g., updated local currency).
        /// </summary>
        /// <param name="lastProcessedTransactionId">The ID of the last transaction that was processed</param>
        /// <returns>True if commit was successful</returns>
        public static async Task<bool> CommitSdkTransactions(string lastProcessedTransactionId)
        {
            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[PlaySuper] Cannot commit SDK transactions - user not authenticated");
                return false;
            }

            if (string.IsNullOrEmpty(lastProcessedTransactionId))
            {
                Debug.LogWarning("[PlaySuper] Cannot commit SDK transactions - no transaction ID provided");
                return false;
            }

            try
            {
                CommitSdkSyncRequest requestBody = new CommitSdkSyncRequest(lastProcessedTransactionId);
                string jsonBody = JsonUtility.ToJson(requestBody);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                using (var webRequest = new UnityWebRequest($"{baseUrl}/player/sdk-transactions/commit", "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseJson = webRequest.downloadHandler.text;
                        CommitSdkSyncResponse wrapper = JsonUtility.FromJson<CommitSdkSyncResponse>(responseJson);
                        var data = wrapper?.data;

                        if (data != null && data.success)
                        {
                            // Update local storage - remove processed transactions
                            SdkTransactionSyncManager.RemoveProcessedTransactions(lastProcessedTransactionId);
                            SdkTransactionSyncManager.SetLastSyncedCheckpoint(data.newCheckpoint);

                            // Track success using legacy checkpoint method
                            _ = TrackLegacyCommitSuccess(lastProcessedTransactionId);

                            Debug.Log($"[PlaySuper] Successfully committed SDK transactions up to {lastProcessedTransactionId}");
                            return true;
                        }
                        else
                        {
                            Debug.LogError("[PlaySuper] Server returned unsuccessful commit response");
                            _ = TrackTransactionsCommitFailed(new List<string> { lastProcessedTransactionId }, "unsuccessful_response", 0, "checkpoint");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PlaySuper] Error committing SDK transactions: {webRequest.responseCode} - {webRequest.downloadHandler.text}");
                        _ = TrackTransactionsCommitFailed(new List<string> { lastProcessedTransactionId }, webRequest.downloadHandler.text, (int)webRequest.responseCode, "checkpoint");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Exception committing SDK transactions: {e.Message}");
                _ = TrackTransactionsCommitFailed(new List<string> { lastProcessedTransactionId }, e.Message, 0, "checkpoint");
                return false;
            }
        }

        /// <summary>
        /// Convenience method to commit all pending transactions.
        /// Gets the last transaction from pending list and commits up to that point.
        /// </summary>
        /// <returns>True if commit was successful, false if failed or no pending transactions</returns>
        public static async Task<bool> CommitAllPendingSdkTransactions()
        {
            List<SdkTransaction> pending = SdkTransactionSyncManager.GetPendingTransactions();
            if (pending == null || pending.Count == 0)
            {
                Debug.Log("[PlaySuper] No pending SDK transactions to commit");
                return true; // Nothing to commit is considered success
            }

            // Get the last transaction ID
            string lastTransactionId = pending[pending.Count - 1].id;
            return await CommitSdkTransactions(lastTransactionId);
        }

        /// <summary>
        /// Commit specific transactions by their IDs.
        /// This is the recommended method for committing transactions as it provides per-transaction granularity.
        /// Call this after successfully processing each transaction in-game.
        /// </summary>
        /// <param name="transactionIds">List of transaction IDs to mark as committed</param>
        /// <returns>Result containing breakdown of committed, already committed, not found, and failed IDs</returns>
        /// <example>
        /// var result = await PlaySuperUnitySDK.CommitSdkTransactionsByIds(new List&lt;string&gt; { "txn_123", "txn_456" });
        /// if (result.Success) {
        ///     Debug.Log($"Committed {result.Committed.Count} transactions");
        /// }
        /// </example>
        public static async Task<CommitByIdsResult> CommitSdkTransactionsByIds(List<string> transactionIds)
        {
            var emptyResult = new CommitByIdsResult { Success = false };

            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[PlaySuper] Cannot commit SDK transactions - user not authenticated");
                return emptyResult;
            }

            if (transactionIds == null || transactionIds.Count == 0)
            {
                Debug.LogWarning("[PlaySuper] Cannot commit SDK transactions - no transaction IDs provided");
                return emptyResult;
            }

            try
            {
                CommitByIdsRequest requestBody = new CommitByIdsRequest(transactionIds);
                string jsonBody = JsonUtility.ToJson(requestBody);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

                using (var webRequest = new UnityWebRequest($"{baseUrl}/player/sdk-transactions/commit-by-ids", "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);
                    webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseJson = webRequest.downloadHandler.text;
                        CommitByIdsResponse wrapper = JsonUtility.FromJson<CommitByIdsResponse>(responseJson);
                        var data = wrapper?.data;

                        if (data != null)
                        {
                            var result = CommitByIdsResult.FromData(data);

                            // Remove successfully committed transactions from local storage
                            List<string> allCommitted = new List<string>();
                            allCommitted.AddRange(result.Committed);
                            allCommitted.AddRange(result.AlreadyCommitted);

                            if (allCommitted.Count > 0)
                            {
                                SdkTransactionSyncManager.RemoveTransactionsByIds(allCommitted);
                            }

                            // Track analytics event for successful commit
                            _ = TrackTransactionsCommitted(result, transactionIds.Count, "by_ids");

                            Debug.Log($"[PlaySuper] CommitByIds: {result.Committed.Count} committed, {result.AlreadyCommitted.Count} already committed, {result.NotFound.Count} not found, {result.Failed.Count} failed");
                            return result;
                        }
                        else
                        {
                            Debug.LogError("[PlaySuper] Server returned null data in commit-by-ids response");
                            // Track failure
                            _ = TrackTransactionsCommitFailed(transactionIds, "null_response", 0, "by_ids");
                            return emptyResult;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PlaySuper] Error committing SDK transactions by IDs: {webRequest.responseCode} - {webRequest.downloadHandler.text}");
                        // Track failure
                        _ = TrackTransactionsCommitFailed(transactionIds, webRequest.downloadHandler.text, (int)webRequest.responseCode, "by_ids");
                        return emptyResult;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Exception committing SDK transactions by IDs: {e.Message}");
                // Track failure
                _ = TrackTransactionsCommitFailed(transactionIds, e.Message, 0, "by_ids");
                return emptyResult;
            }
        }

        /// <summary>
        /// Clear all SDK transaction sync state. Call this on logout.
        /// </summary>
        public static void ClearSdkTransactionSyncState()
        {
            SdkTransactionSyncManager.ClearAll();
            Debug.Log("[PlaySuper] SDK transaction sync state cleared");
        }

        #region Transaction Analytics Tracking

        /// <summary>
        /// Track when transactions are fetched from server
        /// </summary>
        private static async Task TrackTransactionsFetched(List<SdkTransaction> transactions)
        {
            if (transactions == null || transactions.Count == 0) return;

            try
            {
                // Calculate totals by type
                float totalDebit = 0;
                float totalCredit = 0;
                string coinId = null;
                string coinName = null;

                foreach (var txn in transactions)
                {
                    if (txn.source == "PURCHASE_DEBIT")
                    {
                        totalDebit += txn.amount;
                    }
                    else if (txn.source == "REFUND_CREDIT")
                    {
                        totalCredit += txn.amount;
                    }

                    // Capture coin info from first transaction
                    if (coinId == null && !string.IsNullOrEmpty(txn.coinId))
                    {
                        coinId = txn.coinId;
                        coinName = txn.coinName;
                    }
                }

                var transactionIds = new List<string>();
                foreach (var txn in transactions)
                {
                    transactionIds.Add(txn.id);
                }

                var properties = new Dictionary<string, object>
                {
                    { "transaction_count", transactions.Count },
                    { "transaction_ids", transactionIds },
                    { "total_debit_amount", totalDebit },
                    { "total_credit_amount", totalCredit },
                    { "coinId", coinId },
                    { "coinName", coinName }
                };

                await MixPanelManager.SendEvent(Constants.MixpanelEvent.TRANSACTIONS_FETCHED, 0, properties);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to track transactions_fetched event: {e.Message}");
            }
        }

        /// <summary>
        /// Track when transactions are successfully committed
        /// </summary>
        private static async Task TrackTransactionsCommitted(CommitByIdsResult result, int requestedCount, string commitMethod)
        {
            try
            {
                var properties = new Dictionary<string, object>
                {
                    { "requested_count", requestedCount },
                    { "committed_count", result.Committed.Count },
                    { "already_committed_count", result.AlreadyCommitted.Count },
                    { "not_found_count", result.NotFound.Count },
                    { "failed_count", result.Failed.Count },
                    { "commit_method", commitMethod },
                    { "success", result.Success },
                    { "transaction_ids", result.Committed }
                };

                await MixPanelManager.SendEvent(Constants.MixpanelEvent.TRANSACTIONS_COMMITTED, 0, properties);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to track transactions_committed event: {e.Message}");
            }
        }

        /// <summary>
        /// Track when transaction commit fails
        /// </summary>
        private static async Task TrackTransactionsCommitFailed(List<string> transactionIds, string errorMessage, int errorCode, string commitMethod)
        {
            try
            {
                var properties = new Dictionary<string, object>
                {
                    { "transaction_count", transactionIds?.Count ?? 0 },
                    { "transaction_ids", transactionIds },
                    { "error_message", errorMessage ?? "unknown" },
                    { "error_code", errorCode },
                    { "commit_method", commitMethod }
                };

                await MixPanelManager.SendEvent(Constants.MixpanelEvent.TRANSACTIONS_COMMIT_FAILED, 0, properties);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to track transactions_commit_failed event: {e.Message}");
            }
        }

        /// <summary>
        /// Track successful commit using legacy checkpoint method
        /// </summary>
        private static async Task TrackLegacyCommitSuccess(string lastProcessedTransactionId)
        {
            try
            {
                // For legacy checkpoint commits, we don't have individual transaction details
                // Just track the checkpoint ID and method
                var properties = new Dictionary<string, object>
                {
                    { "last_processed_transaction_id", lastProcessedTransactionId },
                    { "commit_method", "checkpoint" },
                    { "success", true }
                };

                await MixPanelManager.SendEvent(Constants.MixpanelEvent.TRANSACTIONS_COMMITTED, 0, properties);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to track legacy commit success event: {e.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Logs out the current player by clearing the auth token, profile, and all
        /// associated local state (transactions, user properties, analytics queue).
        /// Call this when your player signs out.
        /// </summary>
        public static void Logout()
        {
            authToken = null;
            profile = null;
            PlayerPrefs.DeleteKey("authToken");

            ClearUserProperties();
            ClearSdkTransactionSyncState();
            TransactionsManager.ClearTransactions();
            MixPanelEventQueue.ClearQueue();

            PlayerPrefsSaveManager.ForceSaveImmediate(); // Critical: must complete before method returns
            Debug.Log("[PlaySuper] Player logged out — all session state cleared");
        }

        #endregion
    }

    internal class MixPanelManager
    {
        private static string _deviceId;
        private static string _advertisingId;
        private static string _advertisingIdSource;
        private static Dictionary<string, object> _userProperties;
        private static bool _userPropertiesLoaded = false;

        #region User Properties Management

        /// <summary>
        /// Load user properties from PlayerPrefs (lazy loaded)
        /// </summary>
        private static void EnsureUserPropertiesLoaded()
        {
            if (_userPropertiesLoaded)
                return;

            _userProperties = new Dictionary<string, object>();
            _userPropertiesLoaded = true;

            if (PlayerPrefs.HasKey(Constants.userPropertiesKey))
            {
                try
                {
                    string json = PlayerPrefs.GetString(Constants.userPropertiesKey);
                    if (!string.IsNullOrEmpty(json))
                    {
                        // Parse simple JSON object
                        var parsed = ParseSimpleJsonObject(json);
                        if (parsed != null)
                        {
                            _userProperties = parsed;
                            Debug.Log($"[MixPanel] Loaded {_userProperties.Count} user properties from storage");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MixPanel] Failed to load user properties: {e.Message}");
                    _userProperties = new Dictionary<string, object>();
                }
            }
        }

        /// <summary>
        /// Save user properties to PlayerPrefs
        /// </summary>
        private static void SaveUserProperties()
        {
            try
            {
                string json = SerializeUserPropertiesToJson();
                PlayerPrefs.SetString(Constants.userPropertiesKey, json);
                PlayerPrefsSaveManager.ScheduleSave();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MixPanel] Failed to save user properties: {e.Message}");
            }
        }

        /// <summary>
        /// Serialize user properties to JSON string for storage
        /// </summary>
        private static string SerializeUserPropertiesToJson()
        {
            if (_userProperties == null || _userProperties.Count == 0)
                return "{}";

            var pairs = new List<string>();
            foreach (var kvp in _userProperties)
            {
                string jsonValue = ConvertValueToJson(kvp.Value);
                pairs.Add($"\"{kvp.Key}\":{jsonValue}");
            }
            return "{" + string.Join(",", pairs) + "}";
        }

        /// <summary>
        /// Parse a simple JSON object (supports string, number, bool)
        /// </summary>
        private static Dictionary<string, object> ParseSimpleJsonObject(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(json) || json == "{}")
                return result;

            // Remove outer braces
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            if (string.IsNullOrEmpty(json.Trim()))
                return result;

            // Simple parser for key:value pairs
            int i = 0;
            while (i < json.Length)
            {
                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;

                // Parse key (expect quoted string)
                if (json[i] != '"') { i++; continue; }
                i++; // skip opening quote
                int keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string key = json.Substring(keyStart, i - keyStart);
                i++; // skip closing quote

                // Skip to colon
                while (i < json.Length && json[i] != ':') i++;
                i++; // skip colon

                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                // Parse value
                object value = null;
                if (i < json.Length)
                {
                    if (json[i] == '"')
                    {
                        // String value
                        i++;
                        int valueStart = i;
                        while (i < json.Length && json[i] != '"')
                        {
                            if (json[i] == '\\' && i + 1 < json.Length) i++; // skip escaped char
                            i++;
                        }
                        value = json.Substring(valueStart, i - valueStart).Replace("\\\"", "\"").Replace("\\\\", "\\");
                        i++; // skip closing quote
                    }
                    else if (json[i] == 't' || json[i] == 'f')
                    {
                        // Boolean
                        if (json.Substring(i).StartsWith("true"))
                        {
                            value = true;
                            i += 4;
                        }
                        else if (json.Substring(i).StartsWith("false"))
                        {
                            value = false;
                            i += 5;
                        }
                    }
                    else if (json[i] == 'n' && json.Substring(i).StartsWith("null"))
                    {
                        value = null;
                        i += 4;
                    }
                    else if (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '.')
                    {
                        // Number
                        int numStart = i;
                        while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == 'e' || json[i] == 'E' || json[i] == '+'))
                            i++;
                        string numStr = json.Substring(numStart, i - numStart);
                        if (numStr.Contains("."))
                        {
                            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d))
                                value = d;
                        }
                        else
                        {
                            if (int.TryParse(numStr, out int intVal))
                                value = intVal;
                            else if (long.TryParse(numStr, out long longVal))
                                value = longVal;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(key))
                    result[key] = value;

                // Skip to comma or end
                while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                if (i < json.Length && json[i] == ',') i++;
            }

            return result;
        }

        internal static void SetUserProperties(Dictionary<string, object> properties)
        {
            EnsureUserPropertiesLoaded();
            foreach (var kvp in properties)
            {
                _userProperties[kvp.Key] = kvp.Value;
            }
            SaveUserProperties();
        }

        internal static void ClearUserProperties()
        {
            EnsureUserPropertiesLoaded();
            _userProperties.Clear();
            PlayerPrefs.DeleteKey(Constants.userPropertiesKey);
            PlayerPrefsSaveManager.ScheduleSave();
        }

        internal static Dictionary<string, object> GetUserProperties()
        {
            EnsureUserPropertiesLoaded();
            return new Dictionary<string, object>(_userProperties);
        }

        /// <summary>
        /// Convert user properties to JSON property strings
        /// </summary>
        private static List<string> GetUserPropertiesAsJsonStrings()
        {
            EnsureUserPropertiesLoaded();
            var result = new List<string>();
            foreach (var kvp in _userProperties)
            {
                string jsonValue = ConvertValueToJson(kvp.Value);
                if (jsonValue != null)
                {
                    result.Add($@"""user_{kvp.Key}"": {jsonValue}");
                }
            }
            return result;
        }

        /// <summary>
        /// Convert a value to its JSON representation
        /// </summary>
        private static string ConvertValueToJson(object value)
        {
            if (value == null)
                return "null";

            switch (value)
            {
                case string s:
                    return $"\"{EscapeJsonString(s)}\"";
                case bool b:
                    return b ? "true" : "false";
                case int i:
                    return i.ToString();
                case long l:
                    return l.ToString();
                case float f:
                    return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    // For other types, convert to string
                    return $"\"{EscapeJsonString(value.ToString())}\"";
            }
        }

        #endregion

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
                    return PlaySuperUnitySDK.GetProfileData()?.id;
                }
                else
                    return null;
            }
        }

        private static GameData gameData;

        internal static async Task SendEvent(string eventName, long timestamp = 0, Dictionary<string, object> customProperties = null)
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

                // Guard against failed fetch - skip event if we have no game data
                if (gameData == null)
                {
                    Debug.LogWarning("[PlaySuper] Cannot send event - game data unavailable");
                    return;
                }

                // Get IP address (with fallback)
                string ipAddress = await NetworkUtils.GetPublicIPAddress();

                // Safe accessors for nested properties (studio/organization may be null)
                string studioOrgId = gameData.studio?.organizationId ?? "";
                string studioName = gameData.studio?.organization?.name ?? "";
                string studioHandle = gameData.studio?.organization?.handle ?? "";

                // Build properties list
                var properties = new List<string>
                {
                    // Core identifiers
                    $@"""$device_id"": ""{DeviceId}""",
                    $@"""time"": {actualEventTime}",
                    $@"""$insert_id"": ""{Guid.NewGuid()}""",
                    $@"""$ip"": ""{ipAddress}""",
                    
                    // App info
                    $@"""app_version"": ""{Application.version}""",
                    
                    // Platform & Device
                    $@"""platform"": ""{Application.platform}""",
                    $@"""os_version"": ""{EscapeJsonString(SystemInfo.operatingSystem)}""",
                    $@"""device_model"": ""{EscapeJsonString(SystemInfo.deviceModel)}""",
                    
                    // Game context
                    $@"""gameId"": ""{gameData.id}""",
                    $@"""gameName"": ""{gameData.name}""",
                    $@"""studioId"": ""{gameData.studioId}""",
                    $@"""studioOrganizationId"": ""{studioOrgId}""",
                    $@"""studioName"": ""{studioName}""",
                    $@"""studioHandle"": ""{studioHandle}""",
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

                // Add user properties (set via SetUserProperties)
                var userProps = GetUserPropertiesAsJsonStrings();
                if (userProps.Count > 0)
                {
                    properties.AddRange(userProps);
                }

                // Add custom properties if provided
                if (customProperties != null)
                {
                    foreach (var kvp in customProperties)
                    {
                        if (kvp.Value == null) continue;

                        if (kvp.Value is string strValue)
                        {
                            properties.Add($@"""{kvp.Key}"": ""{EscapeJsonString(strValue)}""");
                        }
                        else if (kvp.Value is bool boolValue)
                        {
                            properties.Add($@"""{kvp.Key}"": {boolValue.ToString().ToLower()}");
                        }
                        else if (kvp.Value is int || kvp.Value is long || kvp.Value is float || kvp.Value is double)
                        {
                            properties.Add($@"""{kvp.Key}"": {kvp.Value}");
                        }
                        else if (kvp.Value is string[] strArray)
                        {
                            var arrayJson = "[" + string.Join(",", strArray.Select(s => $@"""{EscapeJsonString(s)}""")) + "]";
                            properties.Add($@"""{kvp.Key}"": {arrayJson}");
                        }
                        else if (kvp.Value is List<string> strList)
                        {
                            var arrayJson = "[" + string.Join(",", strList.Select(s => $@"""{EscapeJsonString(s)}""")) + "]";
                            properties.Add($@"""{kvp.Key}"": {arrayJson}");
                        }
                    }
                }

                // Create clean payload
                var mixPanelPayload =
                    $@"{{
    ""event_name"": ""{eventName}"",
    ""properties"": {{
        {string.Join(",\n        ", properties)}
    }}
}}";

                // Log full payload for debugging
                Debug.Log($"[Analytics] Sending event '{eventName}':\n{mixPanelPayload}");

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

        /// <summary>
        /// Escape special characters for JSON string values
        /// </summary>
        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        internal static void Dispose()
        {
            // Clear cached data
            gameData = null;
            _deviceId = null;
            _advertisingId = null;
            _userProperties.Clear();

            Debug.Log("[MixPanel] Manager disposed");
        }
    }
}
