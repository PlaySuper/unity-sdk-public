using System.Runtime.CompilerServices;
using UnityEngine;
using Gpm.WebView;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("playsuper.unity.Runtime.Tests")]

namespace PlaySuperUnity
{
    public class PlaySuperUnitySDK : MonoBehaviour
    {

        private static PlaySuperUnitySDK _instance;
        private static string apiKey;
<<<<<<< HEAD

        private string authToken;

=======
        private static string authToken;
>>>>>>> 94e4968b94462e41a9284d222c4fff2edd72bea3
        private static string baseUrl;
        private static ProfileData profile;

        // Private constructor to prevent instantiation from outside
        private PlaySuperUnitySDK() { }

        public static void Initialize(string _apiKey)
        {
            Application.wantsToQuit += OnApplicationWantsToQuit;
            if (_instance == null)
            {
                string env = Environment.GetEnvironmentVariable("PROJECT_ENV") ?? "production";
                if (env == "development")
                {
                    baseUrl = "https://dev.playsuper.club";
                }
                else
                {
                    baseUrl = "https://api.playsuper.club";
                }
                GameObject sdkObject = new GameObject("PlaySuper");
                _instance = sdkObject.AddComponent<PlaySuperUnitySDK>();
                DontDestroyOnLoad(sdkObject);

                apiKey = _apiKey;

                Debug.Log("PlaySuperUnity initialized with API Key: " + apiKey);
            }
            else
            {
                Debug.LogError("PlaySuperUnity Instance already initialized");
            }
            if (PlayerPrefs.HasKey(Constants.lastCloseTimestampName))
            {
                if (PlayerPrefs.GetString(Constants.lastCloseDoneName) == "0")
                {
                    string timestamp = PlayerPrefs.GetString(Constants.lastCloseTimestampName);
                    long timestampLong;
                    if (long.TryParse(timestamp, out timestampLong))
                    {
                        MixPanelManager.SendEvent(Constants.MixpanelEvent.GAME_CLOSE, timestampLong);
                        PlayerPrefs.SetString(Constants.lastCloseDoneName, "1");
                    }
                }
            }
            MixPanelManager.SendEvent(Constants.MixpanelEvent.GAME_OPEN);
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
            PlayerPrefs.SetString(Constants.lastCloseTimestampName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetString(Constants.lastCloseDoneName, "0");
            return true;
        }

        public async Task DistributeCoins(string coinId, int amount)
        {
            if (authToken == null)
            {
                TransactionsManager.AddTransaction(coinId, amount);
                Debug.Log("Transaction stored in local: " + PlayerPrefs.GetString("transactions"));
                return;
            }
            var client = new HttpClient();
            var jsonPayload = $@"{{
                ""amount"": {amount}
            }}";

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var url = $"{baseUrl}/coins/{coinId}/distribute";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.Log("Response received successfully:");
                Debug.Log(responseContent);
            }
            else
            {
                Debug.LogError($"Error from DistributeCoins: {response}");
            }
        }

        public async void OpenStore()
        {
            MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_OPEN);
            WebView.ShowUrlFullScreen();
        }

        internal async void OnTokenReceive(string _token)
        {
            if (IsLoggedIn()) return;
            authToken = _token;

            // Fetch profile data from token
            profile = await ProfileManager.GetProfileData();

            // Send Event to Mixpanel
            await MixPanelManager.SendEvent(Constants.MixpanelEvent.PLAYER_IDENTIFY);

            // Send DistributeCoins requests for transactions stored locally
            if (!TransactionsManager.HasTransactions()) return;
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
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
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
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
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
                            CoinBalance cb = new CoinBalance(pc.coinId, pc.coin.name, pc.coin.pictureUrl, pc.balance);
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
                                balances[i].id += t.amount;
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

<<<<<<< HEAD

        internal void SetAuthToken(string token)
=======
        internal static void SetAuthToken(string token)
>>>>>>> 94e4968b94462e41a9284d222c4fff2edd72bea3
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

<<<<<<< HEAD
=======
        internal static string GetBaseUrl()
        {
            return baseUrl;
        }
    }

    internal class MixPanelManager
    {
        private static string _deviceId;
        internal static string DeviceId
        {
            get
            {
                if (!string.IsNullOrEmpty(_deviceId)) return _deviceId;
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
                else return null;
            }
        }

        private static GameData gameData;

        internal static async Task SendEvent(string eventName, long timestamp = 0)
        {
            try
            {
                var client = new HttpClient();
                string insertId = Guid.NewGuid().ToString();
                if (gameData == null)
                {
                    gameData = await GameManager.GetGameData();
                }
                var mixPanelPayload = $@"{{
                    ""event_name"": ""{eventName}"",
                    ""properties"": {{
                        ""$device_id"": ""{DeviceId}"",
                        {(timestamp != 0 ? $@"""time"": {timestamp}," : "")}
                        ""$insert_id"": ""{insertId}"",
                        ""gameId"": ""{gameData.id}"",
                        ""gameName"": ""{gameData.name}"",
                        ""studioId"": ""{gameData.studioId}"",
                        {(!string.IsNullOrEmpty(userId) ? $@", ""$user_id"": ""{userId}""" : "")}
                        ""studioOrganizationId"": ""{gameData.studio.organizationId}"",
                        ""studioName"": ""{gameData.studio.organization.name}""
                    }}
                }}";
                var content = new StringContent(mixPanelPayload, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, Constants.MIXPANEL_URL) { Content = content };
                request.Headers.Accept.Clear();
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.Log($"Mixpanel event: {eventName}, {responseContent}");
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"Error SendEvent: {e.Message}");
            }
        }
>>>>>>> 94e4968b94462e41a9284d222c4fff2edd72bea3
    }
}
