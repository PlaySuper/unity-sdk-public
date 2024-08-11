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
            var client = new HttpClient();
            var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjQ4NWRlYi01NGIxLTQyMjEtYjJmMS1mYjc5NjFiMzM4NjgiLCJwaG9uZSI6Iis5MTk0NjA2MTAxODAiLCJpYXQiOjE3MjMzNjYwNzUsImV4cCI6MTcyNTk1ODA3NX0.Lncf3jn8WRq3B8RY62IiXV3bxjO_szuoE9tKBC3jC6g";
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
            request.Headers.Add("Authorization", $"Bearer {token}");

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.Log("Response received successfully:");
                Debug.Log(responseContent);
            }
            else
            {
                Debug.Log($"Error: {response}");
            }
        }

        public void OpenStore()
        {
            WebView.ShowUrlFullScreen();
        }
    }
}
