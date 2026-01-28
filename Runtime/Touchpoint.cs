using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlaySuperUnity
{
    /// <summary>
    /// Node data type - defines what kind of data this node contains
    /// </summary>
    public enum NodeDataType
    {
        ASSET,   // Pure visual node (images, text only)
        STATIC,  // Static data (specific rewardIds/productIds)
        DYNAMIC  // Dynamic fetching (query based on dynamicConfig)
    }

    /// <summary>
    /// Touchpoint node - represents a single element in the touchpoint tree.
    /// Fields match the schema exactly, with Json fields as JToken for flexibility.
    /// </summary>
    [Serializable]
    public class TouchpointNode
    {
        // Identity
        public string id;

        // Visual - Json fields (flexible structure)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken background;  // { color?, gradient?, images[]?, opacity? }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] images;  // Primary content images

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken overlay;  // { color?, gradient?, images[]?, opacity? }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] additionalAssets;  // Secondary/supporting assets

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken title;  // [{ text, color, fontSize, fontWeight, ... }]

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken subtitle;  // [{ text, color, fontSize, fontWeight, ... }]

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken badge;  // { text, backgroundImage, icon, style }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken cta;  // { label, action, backgroundImage, frontImage, style }

        // Data references (for STATIC type)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string rewardId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string productId;

        // Dynamic fetching config (for DYNAMIC type)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken dynamicConfig;  // { source, limit, sortBy, filters, ... }

        // Layout/styling hints for SDK
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string layoutHint;  // e.g., "carousel", "grid", "list", "hero"

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken styleHint;  // Additional styling metadata

        // Meta
        public int displayOrder;

        // Hydrated data (populated by API - reward or product details)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JToken data;

        // Nested content
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointNode[] nodes;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointNode popup;

        #region Helper Properties

        /// <summary>
        /// Check if this node has reward data
        /// </summary>
        public bool HasReward => !string.IsNullOrEmpty(rewardId) || (data != null && data.Value<string>("type") != "product");

        /// <summary>
        /// Check if this node has product data
        /// </summary>
        public bool HasProduct => !string.IsNullOrEmpty(productId) || data?.Value<string>("type") == "product";

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get a value from the data object
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (data == null || data[key] == null)
                return defaultValue;
            return data[key].ToObject<T>();
        }

        /// <summary>
        /// Get a value from the background object
        /// </summary>
        public T GetBackground<T>(string key, T defaultValue = default)
        {
            if (background == null || background[key] == null)
                return defaultValue;
            return background[key].ToObject<T>();
        }

        /// <summary>
        /// Get a value from the badge object
        /// </summary>
        public T GetBadge<T>(string key, T defaultValue = default)
        {
            if (badge == null || badge[key] == null)
                return defaultValue;
            return badge[key].ToObject<T>();
        }

        /// <summary>
        /// Get a value from the cta object
        /// </summary>
        public T GetCta<T>(string key, T defaultValue = default)
        {
            if (cta == null || cta[key] == null)
                return defaultValue;
            return cta[key].ToObject<T>();
        }

        /// <summary>
        /// Get a style hint value by key
        /// </summary>
        public T GetStyleHint<T>(string key, T defaultValue = default)
        {
            if (styleHint == null || styleHint[key] == null)
                return defaultValue;
            return styleHint[key].ToObject<T>();
        }

        /// <summary>
        /// Convert the entire data object to a typed class if needed
        /// </summary>
        public T GetDataAs<T>() where T : class
        {
            return data?.ToObject<T>();
        }

        #endregion
    }

    #region Response Types

    /// <summary>
    /// Touchpoint response - represents a complete touchpoint with nodes
    /// </summary>
    [Serializable]
    public class TouchpointResponse
    {
        public string id;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string gameId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string name;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointNode[] nodes;
    }

    /// <summary>
    /// List response for getting all touchpoints
    /// </summary>
    [Serializable]
    public class TouchpointListResponse
    {
        public TouchpointResponse[] touchpoints;
        public int total;
    }

    #endregion

    #region Internal API Response Wrappers

    /// <summary>
    /// Internal API wrapper for touchpoint response
    /// </summary>
    [Serializable]
    internal class TouchpointApiResponse
    {
        public TouchpointResponse data;
        public int statusCode;
        public string message;
    }

    /// <summary>
    /// Internal API wrapper for touchpoint list response
    /// </summary>
    [Serializable]
    internal class TouchpointListApiResponse
    {
        public TouchpointListResponse data;
        public int statusCode;
        public string message;
    }

    #endregion

    /// <summary>
    /// Manager class for Touchpoint API operations
    /// </summary>
    public class TouchpointManager
    {
        /// <summary>
        /// Get a touchpoint by its name
        /// </summary>
        /// <param name="name">Touchpoint name (e.g., "Main Store")</param>
        /// <param name="coinId">Coin ID for price calculations</param>
        /// <returns>TouchpointResponse or null on error</returns>
        public static async Task<TouchpointResponse> GetTouchpointByName(string name, string coinId)
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - API key not set");
                return null;
            }

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - name is required");
                return null;
            }

            if (string.IsNullOrEmpty(coinId))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - coinId is required");
                return null;
            }

            try
            {
                var client = new HttpClient();
                var encodedName = Uri.EscapeDataString(name);
                var url = $"{baseUrl}/touchpoints/name/{encodedName}?coinId={Uri.EscapeDataString(coinId)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("x-api-key", apiKey);

                if (!string.IsNullOrEmpty(authToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {authToken}");
                }

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    TouchpointApiResponse apiResponse = JsonConvert.DeserializeObject<TouchpointApiResponse>(json);
                    return apiResponse?.data;
                }
                else
                {
                    Debug.LogError($"[PlaySuper] Error fetching touchpoint by name: {response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"[PlaySuper] Error GetTouchpointByName: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Unexpected error in GetTouchpointByName: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a touchpoint by its ID
        /// </summary>
        /// <param name="id">Touchpoint UUID</param>
        /// <param name="coinId">Coin ID for price calculations</param>
        /// <returns>TouchpointResponse or null on error</returns>
        public static async Task<TouchpointResponse> GetTouchpointById(string id, string coinId)
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - API key not set");
                return null;
            }

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - id is required");
                return null;
            }

            if (string.IsNullOrEmpty(coinId))
            {
                Debug.LogError("[PlaySuper] Cannot get touchpoint - coinId is required");
                return null;
            }

            try
            {
                var client = new HttpClient();
                var encodedId = Uri.EscapeDataString(id);
                var url = $"{baseUrl}/touchpoints/{encodedId}?coinId={Uri.EscapeDataString(coinId)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("x-api-key", apiKey);

                if (!string.IsNullOrEmpty(authToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {authToken}");
                }

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    TouchpointApiResponse apiResponse = JsonConvert.DeserializeObject<TouchpointApiResponse>(json);
                    return apiResponse?.data;
                }
                else
                {
                    Debug.LogError($"[PlaySuper] Error fetching touchpoint by id: {response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"[PlaySuper] Error GetTouchpointById: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Unexpected error in GetTouchpointById: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// List all active touchpoints for the game
        /// </summary>
        /// <param name="coinId">Coin ID for price calculations</param>
        /// <returns>TouchpointListResponse or null on error</returns>
        public static async Task<TouchpointListResponse> ListTouchpoints(string coinId)
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlaySuper] Cannot list touchpoints - API key not set");
                return null;
            }

            if (string.IsNullOrEmpty(coinId))
            {
                Debug.LogError("[PlaySuper] Cannot list touchpoints - coinId is required");
                return null;
            }

            try
            {
                var client = new HttpClient();
                var url = $"{baseUrl}/touchpoints?coinId={Uri.EscapeDataString(coinId)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("x-api-key", apiKey);

                if (!string.IsNullOrEmpty(authToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {authToken}");
                }

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    TouchpointListApiResponse apiResponse = JsonConvert.DeserializeObject<TouchpointListApiResponse>(json);
                    return apiResponse?.data;
                }
                else
                {
                    Debug.LogError($"[PlaySuper] Error listing touchpoints: {response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"[PlaySuper] Error ListTouchpoints: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Unexpected error in ListTouchpoints: {e.Message}");
                return null;
            }
        }
    }
}
