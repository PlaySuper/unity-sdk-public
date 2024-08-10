using UnityEngine;
using Gpm.WebView;

using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlaySuperUnity
{
    public class PlaySuperUnitySDK : MonoBehaviour
    {

        private static PlaySuperUnitySDK _instance;
        private static string gameId;

        public static void Initialize(string _gameId)
        {
            if (_instance == null)
            {
                GameObject sdkObject = new GameObject("PlaySuper");
                _instance = sdkObject.AddComponent<PlaySuperUnitySDK>();
                DontDestroyOnLoad(sdkObject);

                gameId = _gameId;

                // TODO: Add initialization logic here
                // For example: Set API key and endpoint, establish a connection, etc.
                Debug.Log("PlaySuperUnity initialized with Game ID: " + gameId);
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


        public async void SendCoins(string playerId, string coinId, int amount)
        {
            var client = new HttpClient();
            var jsonPayload = @"{
                ""playerId"": ""efca8dc7-0a15-4a8c-8135-cf5819f856f0"",
                ""amount"": 200
            }";

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://dev.playsuper.club/coins/d2935b01-033e-43cf-afae-7eeccbfa544c/distribute")
            {
                Content = content
            };

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
            request.Headers.Add("x-api-key", "5021b57a47046539eb11643cbdbd958a820ae26d22d88af0dde8264155c72090");

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Read the response content
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.Log("Response received successfully:");
                Debug.Log(responseContent);
            }
            else
            {
                Debug.Log($"Error: {response.StatusCode}");
            }
        }

        public void OpenStore()
        {
            WebView.ShowUrlFullScreen();
        }
    }
}
