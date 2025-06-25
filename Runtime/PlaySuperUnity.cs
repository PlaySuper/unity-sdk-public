using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Gpm.Communicator;
using Gpm.WebView;
using UnityEngine;

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

        // Private constructor to prevent instantiation from outside
        private PlaySuperUnitySDK() { }

        public static void Initialize(string _apiKey, bool _isDev = false)
        {
            // Clean up any existing instance first
            if (_instance != null)
            {
                Debug.LogWarning(
                    "[PlaySuper] SDK already initialized - disposing previous instance"
                );
                Dispose();
            }

            Application.wantsToQuit += OnApplicationWantsToQuit;

            if (_instance == null)
            {
                // Initialize core SDK first
                string env = Environment.GetEnvironmentVariable("PROJECT_ENV") ?? "production";
                isDev = _isDev;
                baseUrl =
                    (env == "development" || _isDev) ? Constants.devApiUrl : Constants.prodApiUrl;
                apiKey = _apiKey;

                // Create SDK GameObject
                GameObject sdkObject = new GameObject("PlaySuper");
                _instance = sdkObject.AddComponent<PlaySuperUnitySDK>();
                DontDestroyOnLoad(sdkObject);

                // Initialize MixPanel lifecycle manager AFTER SDK is ready
                MixPanelLifecycleManager.Initialize();

                Debug.Log("PlaySuperUnity initialized with API Key: " + apiKey);
            }

            // Handle previous session close event
            HandlePreviousSessionClose();

            // Send game open event
            MixPanelManager.SendEvent(Constants.MixpanelEvent.GAME_OPEN);
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

        public async void OpenStore()
        {
            OpenStore(null);
        }

        public async void OpenStore(string token)
        {
            MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_OPEN);
            Debug.Log("OpenStore: with token " + token);
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    authToken = token;
                    if (!ValidateToken(token))
                    {
                        throw new InvalidOperationException("Invalid token");
                    }
                    OnTokenReceive(token);
                    if (profile == null)
                    {
                        profile = await ProfileManager.GetProfileData();
                        // If we get here, token worked correctly
                        Debug.Log("Token valid, profile retrieved successfully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Token error: {ex.Message}");
                    authToken = null;

                    // Throw a more specific exception with context
                    throw new InvalidOperationException(
                        $"Failed to validate token and retrieve profile data: {ex.Message}. Please provide a valid token or initialize SDK properly.",
                        ex
                    );
                }
            }
            WebView.ShowUrlPopupPositionSize(isDev);
        }

        public static bool ValidateToken(string token)
        {
            // Delegate to TokenUtils
            return TokenUtils.ValidateToken(token);
        }

        internal async void OnTokenReceive(string _token)
        {
            if (IsLoggedIn())
                return;
            authToken = _token;

            // Fetch profile data from token
            profile = await ProfileManager.GetProfileData();

            // Send Event to Mixpanel
            await MixPanelManager.SendEvent(Constants.MixpanelEvent.PLAYER_IDENTIFY);

            // Send DistributeCoins requests for transactions stored locally
            if (!TransactionsManager.HasTransactions())
                return;
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

        internal static void SetAuthToken(string token)
        {
            authToken = token;
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

        public static string GetAndroidAdvertiserId()
        {
            string advertisingID = "";
            try
            {
                AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>(
                    "currentActivity"
                );
                AndroidJavaClass client = new AndroidJavaClass(
                    "com.google.android.gms.ads.identifier.AdvertisingIdClient"
                );
                AndroidJavaObject adInfo = client.CallStatic<AndroidJavaObject>(
                    "getAdvertisingIdInfo",
                    currentActivity
                );

                advertisingID = adInfo.Call<string>("getId").ToString();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error GetAndroidAdvertiserId: {e.Message}");
            }
            return advertisingID;
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
                // App is pausing - save current state
                Debug.Log("[PlaySuper] App pausing - saving state");
                MixPanelEventQueue.Dispose(); // This saves the queue
            }
        }

        // Add explicit disposal method for manual cleanup
        public static void Dispose()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
            }
        }
    }

    internal class MixPanelManager
    {
        private static string _deviceId;
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

            Debug.Log("[MixPanel] Manager disposed");
        }
    }
}
