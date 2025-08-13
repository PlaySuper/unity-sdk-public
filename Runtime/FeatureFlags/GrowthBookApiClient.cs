using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Responsible for making GrowthBook API calls
    /// </summary>
    internal class GrowthBookApiClient
    {
        private string clientKey;
        private string apiUrl;
        private const int REQUEST_TIMEOUT = 10;

        public GrowthBookApiClient(string clientKey)
        {
            this.clientKey = clientKey;
            this.apiUrl = Constants.GROWTHBOOK_API_URL;
        }

        /// <summary>
        /// Fetch raw response from GrowthBook API
        /// </summary>
        /// <returns>Raw JSON response or null if failed</returns>
        public async Task<string> FetchRawResponse()
        {
            if (string.IsNullOrEmpty(clientKey))
            {
                Debug.LogWarning("[PlaySuper][FeatureFlags] Client key is null or empty");
                return null;
            }

            var url = $"{apiUrl}/api/features/{clientKey}";

            try
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = REQUEST_TIMEOUT;
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var json = request.downloadHandler.text;
                        Debug.Log($"[PlaySuper][FeatureFlags] Successfully fetched feature flags from API");
                        return json;
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper][FeatureFlags] Failed to fetch flags: {request.error}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper][FeatureFlags] Error fetching flags: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if network is available for API calls
        /// </summary>
        /// <returns>True if network is available</returns>
        public bool IsNetworkAvailable()
        {
            // In Unity, we can check Application.internetReachability
            return Application.internetReachability != NetworkReachability.NotReachable;
        }
    }
}