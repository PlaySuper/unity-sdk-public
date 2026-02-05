using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlaySuperUnity
{
    #region Supporting Types

    /// <summary>
    /// Background/Overlay configuration
    /// </summary>
    [Serializable]
    public class BackgroundConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string color;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string gradient;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] images;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? opacity;
    }

    /// <summary>
    /// Text item for title/subtitle arrays
    /// </summary>
    [Serializable]
    public class TextItem
    {
        public string text;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string color;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? fontSize;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string fontWeight;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string icon;
    }

    /// <summary>
    /// Badge configuration
    /// </summary>
    [Serializable]
    public class BadgeConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string text;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string backgroundImage;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string icon;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JObject style;
    }

    /// <summary>
    /// Call-to-action configuration
    /// </summary>
    [Serializable]
    public class CtaConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string text;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string action;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string backgroundImage;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string frontImage;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JObject style;
    }

    /// <summary>
    /// Dynamic fetching configuration
    /// </summary>
    [Serializable]
    public class DynamicConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string source;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? limit;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string sortBy;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string sortOrder;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JObject filters;
    }

    /// <summary>
    /// Reward metadata
    /// </summary>
    [Serializable]
    public class RewardMetadata
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandName;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] brandCategory;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandLogoImage;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string campaignTitle;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string campaignSubTitle;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string campaignCoverImage;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string campaignDetails;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string termsAndConditions;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string howToRedeem;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandRedirectionLink;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? couponExpiryDateExists;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string couponExpiryDate;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string type;
    }

    /// <summary>
    /// Inventory information
    /// </summary>
    [Serializable]
    public class InventoryInfo
    {
        public int availableQuantity;
        public string type;
    }

    /// <summary>
    /// Price information
    /// </summary>
    [Serializable]
    public class PriceInfo
    {
        public float amount;
        public string coinId;
    }

    /// <summary>
    /// Hydrated reward data
    /// </summary>
    [Serializable]
    public class HydratedReward
    {
        public string id;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string name;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string description;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandName;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string organizationId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandRedirectionLink;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RewardMetadata metadata;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public InventoryInfo inventory;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public PriceInfo[] price;
    }

    /// <summary>
    /// Hydrated product data
    /// </summary>
    [Serializable]
    public class HydratedProduct
    {
        // Required fields
        public string id;
        public string type;
        public string productId;

        // Product info
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string name;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandName;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string imageUrl;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? rating;

        // Pricing (from default variant's playSuperPrice)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? listingPrice;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? discountedListingPrice;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? mrp;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? discountPercent;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? coinSpread;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? coinRequiredForDiscount;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? coinsRequiredForMaxDiscount;
    }

    #endregion

    /// <summary>
    /// Touchpoint node - represents a single element in the touchpoint tree.
    /// </summary>
    [Serializable]
    public class TouchpointNode
    {
        // Identity
        public string id;

        // Display order
        public int displayOrder;

        // Visual fields
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BackgroundConfig background;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] images;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BackgroundConfig overlay;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] additionalAssets;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TextItem[] title;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TextItem[] subtitle;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BadgeConfig badge;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CtaConfig cta;

        // Data references
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string rewardId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string productId;

        // Dynamic fetching config
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DynamicConfig dynamicConfig;

        // Layout/styling hints
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string layoutHint;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JObject styleHint;

        // Hydrated data (reward or product details)
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
        /// Get a style hint value by key
        /// </summary>
        public T GetStyleHint<T>(string key, T defaultValue = default)
        {
            if (styleHint == null || styleHint[key] == null)
                return defaultValue;
            return styleHint[key].ToObject<T>();
        }

        /// <summary>
        /// Get the hydrated reward data
        /// </summary>
        public HydratedReward GetReward()
        {
            return data?.ToObject<HydratedReward>();
        }

        /// <summary>
        /// Get the hydrated product data
        /// </summary>
        public HydratedProduct GetProduct()
        {
            return data?.ToObject<HydratedProduct>();
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
