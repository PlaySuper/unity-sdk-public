using System.Runtime.CompilerServices;
using UnityEngine;
using Gpm.WebView;

using System;
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

        private string authToken;

        private static string baseUrl;

        public static void Initialize(string _apiKey)
        {
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


        public async Task DistributeCoins(string coinId, int amount)
        {
            Debug.Log(baseUrl);
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

        public void OpenStore()
        {
            WebView.ShowUrlFullScreen();
        }

        internal async void OnTokenReceive(string _token)
        {
            if (IsLoggedIn()) return;
            authToken = _token;
            Debug.Log("auth token is set: " + _token);

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

        public bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(authToken);
        }

        internal string GetApiKey()
        {
            return apiKey;
        }

        internal string GetAuthToken()
        {
            return authToken;
        }


        internal void SetAuthToken(string token)
        {
            this.authToken = token;
        }

        internal static List<Transaction> GetLocalTransactions()
        {
            string json = PlayerPrefs.GetString("transactions");
            TransactionListWrapper wrapper = JsonUtility.FromJson<TransactionListWrapper>(json);
            return wrapper.transactions;
        }

    }
}
