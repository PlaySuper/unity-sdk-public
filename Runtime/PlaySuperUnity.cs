using UnityEngine;
using Gpm.WebView;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlaySuperUnity
{
    public class PlaySuperUnitySDK : MonoBehaviour
    {

        private static PlaySuperUnitySDK _instance;
        private static string apiKey;

        private string authToken;
        public static void Initialize(string _apiKey)
        {
            if (_instance == null)
            {
                GameObject sdkObject = new GameObject("PlaySuper");
                _instance = sdkObject.AddComponent<PlaySuperUnitySDK>();
                DontDestroyOnLoad(sdkObject);

                apiKey = _apiKey;

                Debug.Log("PlaySuperUnity initialized with API Key: " + apiKey);
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


        public async void DistributeCoins(string coinId, int amount)
        {
            if (authToken == null)
            {
                TransactionsManager.AddTransaction(coinId, amount);
                return;
            }
            var client = new HttpClient();
            var jsonPayload = $@"{{
                ""amount"": {amount}
            }}";

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var url = $"https://dev.playsuper.club/coins/{coinId}/distribute";

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
                Debug.Log($"Error from DistributeCoins: {response}");
            }
        }

        public void OpenStore()
        {
            WebView.ShowUrlFullScreen();
        }

        internal void OnTokenReceive(string _token)
        {
            authToken = _token;
            Debug.Log("Auth Token is set now: " + _token);
            List<Transaction> transactions = TransactionsManager.GetTransactions();
            if (transactions != null)
            {
                foreach (Transaction t in transactions)
                {
                    Debug.Log("Distributing coins: " + t.GetAmount() + " of " + t.GetCoinId());
                    PlaySuperUnitySDK.Instance.DistributeCoins(t.GetCoinId(), t.GetAmount());
                }
            }
        }

        public bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(authToken);
        }
    }
}
