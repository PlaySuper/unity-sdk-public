using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity
{
    #region Enums

    public enum WidgetType
    {
        MULTI_SECTION,
        SINGLE_SECTION,
        LIST_WITH_POPUP
    }

    public enum ListItemType
    {
        COUPON,
        PRODUCT
    }

    public enum SectionType
    {
        Static,
        Dynamic
    }

    public enum LabelType
    {
        Asset,
        Text
    }

    public enum CtaStyle
    {
        Outlined,
        Filled,
        Text
    }

    #endregion

    #region Nested Types

    [Serializable]
    public class TitleDto
    {
        public string text;
        public string color;
        public string icon;
    }

    [Serializable]
    public class SubtitleDto
    {
        public string text;
        public string color;
    }

    [Serializable]
    public class LabelDto
    {
        public string type;
        public string assetUrl;
        public string text;
        public string color;
        public string backgroundColor;
    }

    [Serializable]
    public class BadgeDto
    {
        public string assetUrl;
        public string text;
    }

    [Serializable]
    public class ListItemBadgeDto
    {
        public string text;
        public string textColor;
        public string backgroundColor;
        public string logo;
    }

    [Serializable]
    public class PriceDto
    {
        public float value;
        public string currency;
        public string icon;
    }

    [Serializable]
    public class AdditionalAssetDto
    {
        public string id;
        public string assetUrl;
    }

    [Serializable]
    public class CtaDto
    {
        public string text;
        public string link;
        public string backgroundColor;
        public string textColor;
    }

    [Serializable]
    public class CtaWithIconDto
    {
        public string icon;
        public string text;
        public string backgroundColor;
        public string textColor;
        public string link;
    }

    [Serializable]
    public class SectionCtaDto
    {
        public string text;
        public string link;
        public string backgroundImage;
        public string frontImage;
        public LabelDto label;
    }

    [Serializable]
    public class SecondaryCtaDto
    {
        public string text;
        public string link;
        public string style;
    }

    [Serializable]
    public class CtaHighlightedDto
    {
        public string icon;
        public string text;
        public string highlightedText;
        public string highlightedTextColor;
        public string backgroundColor;
        public string textColor;
        public string link;
    }

    [Serializable]
    public class PopupDto
    {
        public TitleDto title;
        public SubtitleDto subtitle;
        public string backgroundImage;
        public string image;
        public string couponImage;
        public string logo;
        public string brandName;
        public string offerTitle;
        public string offerSubtitle;
        public float mrp;
        public float discountedListingPrice;
        public string currency;
        public string backgroundColor;
        public BadgeDto badge;
        public LabelDto label;
        public CtaHighlightedDto cta;
    }

    #endregion

    #region Detailed Types

    #region Reward Types

    [Serializable]
    public class CampaignAssets
    {
        public string banner;
        public string thumbnail;
    }

    [Serializable]
    public class RewardMetadata
    {
        public string brandName;
        public List<string> brandCategory;
        public string campaignTitle;
        public string campaignSubTitle;
        public string campaignCoverImage;
        public CampaignAssets campaignAssets;
        public string campaignDetails;
        public string brandLogoImage;
        public string termsAndConditions;
        public string howToRedeem;
        public string brandRedirectionLink;
        public bool couponExpiryDateExists;
        public string couponExpiryDate;
        public string denomination;
        public string discountOffPercentage;
    }

    /// <summary>
    /// Full reward data from the reward API
    /// </summary>
    [Serializable]
    public class RewardDetail
    {
        public string id;
        public string name;
        public string description;
        public string startDate;
        public string endDate;
        public float price;
        public int availableQuantity;
        public string brandId;
        public string brandName;
        public string organizationId;
        public string brandRedirectionLink;
        public RewardMetadata metadata;
    }

    #endregion

    #region Product Types

    [Serializable]
    public class ProductCategory
    {
        public string id;
        public string name;
        public string slug;
    }

    [Serializable]
    public class ProductInventory
    {
        public string totalQuantity;
    }

    [Serializable]
    public class SkuInventory
    {
        public string quantity;
    }

    [Serializable]
    public class PlaySuperPrice
    {
        public string listingPrice;
        public string discountedListingPrice;
        public string discountPercent;
        public string marginPercent;
        public string discountValue;
        public string marginValue;
        public string coinSpread;
        public string mrp;
        public float coinRequiredForDiscount;
        public float coinsRequiredForMaxDiscount;
    }

    [Serializable]
    public class ProductSku
    {
        public string id;
        public string name;
        public string barcode;
        public string imageUrl;
        public SkuInventory inventory;
        public string createdAt;
        public string updatedAt;
        public bool isPricingAvailable;
        public float competitorPrice;
        public string competitorPlatform;
        public PlaySuperPrice playSuperPrice;
        public bool isActive;
    }

    /// <summary>
    /// Full product data from the product API
    /// </summary>
    [Serializable]
    public class ProductDetail
    {
        public string id;
        public string sourceId;
        public string name;
        public string description;
        public string plainTextDescription;
        public string brandName;
        public string imageUrl;
        public List<string> images;
        public bool isActive;
        public string categoryId;
        public ProductCategory category;
        public string sourceCategory;
        public string sourceSubCategory;
        public string createdAt;
        public string updatedAt;
        public string lastSyncedAt;
        public float rating;
        public ProductInventory inventory;
        public bool hasException;
        public List<ProductSku> skus;
    }

    #endregion

    #endregion

    #region Item Response Types

    [Serializable]
    public class ItemResponse
    {
        public string id;
        public string rewardId;
        public string productId;
        public string title;
        public string subtitle;
        public string image;
        public string logo;
        public string brandName;
        public float mrp;
        public float listingPrice;
        public float discountedListingPrice;
        public string currency;
        public float coinsRequired;
        public string coinsIcon;
        public string backgroundColor;
        public string textColor;
        public BadgeDto badge;

        // Full objects (complete data from product/reward API)
        public ProductDetail product;
        public RewardDetail reward;
    }

    [Serializable]
    public class ListItemResponse
    {
        public string id;
        public string rewardId;
        public string productId;
        public string logo;
        public string image;
        public string brandLogo;
        public string brandName;
        public string title;
        public string subtitle;
        public string backgroundColor;
        public string textColor;
        public float mrp;
        public float listingPrice;
        public float discountedListingPrice;
        public float coinsRequiredForDiscount;
        public string currency;
        public string coinsIcon;
        public PriceDto price;
        public ListItemBadgeDto badge;
        public CtaWithIconDto cta;
        public PopupDto popup;

        // Full objects (complete data from product/reward API)
        public ProductDetail product;
        public RewardDetail reward;
    }

    [Serializable]
    public class SectionResponse
    {
        public string id;
        public string type;
        public string backgroundImage;
        public string frontImage;
        public SectionCtaDto cta;
        public BadgeDto badge;
        public List<ItemResponse> items;
    }

    #endregion

    #region Main Response Types

    /// <summary>
    /// Runtime touchpoint response - use this for rendering widgets
    /// </summary>
    [Serializable]
    public class TouchpointResponse
    {
        public string touchpoint;
        public string type;
        public string listItemType;

        // Layout properties
        public TitleDto title;
        public SubtitleDto subtitle;
        public string backgroundColor;
        public string backgroundImage;
        public string image;
        public string brandLogo;
        public string brandName;
        public LabelDto label;
        public BadgeDto badge;

        // Content arrays (depends on widget type)
        public List<SectionResponse> sections;
        public List<ItemResponse> items;
        public List<ListItemResponse> listItems;

        // Additional elements
        public List<AdditionalAssetDto> additionalAssets;
        public PopupDto popup;
        public string ctaLink;
        public CtaWithIconDto cta;
        public CtaDto footerCta;
        public SecondaryCtaDto secondaryFooterCta;
    }

    /// <summary>
    /// Config response - used for admin/listing purposes
    /// </summary>
    [Serializable]
    public class TouchpointConfigResponse
    {
        public string id;
        public string name;
        public string type;
        public string listItemType;
        public string gameId;
        public bool isActive;
        public int priority;
        public string createdAt;
        public string updatedAt;

        // Config fields
        public TitleDto title;
        public SubtitleDto subtitle;
        public string backgroundColor;
        public string backgroundImage;
        public string image;
        public string brandLogo;
        public string brandName;
        public LabelDto label;
        public BadgeDto badge;
        public PopupDto popup;
        public string ctaLink;
        public CtaWithIconDto cta;
        public CtaDto footerCta;
        public SecondaryCtaDto secondaryFooterCta;
        public List<AdditionalAssetDto> additionalAssets;
        public List<SectionResponse> sections;
        public List<ItemResponse> items;
    }

    /// <summary>
    /// List response for getting all touchpoints
    /// </summary>
    [Serializable]
    public class TouchpointListResponse
    {
        public List<TouchpointConfigResponse> touchpoints;
        public int total;
    }

    /// <summary>
    /// API wrapper for touchpoint response
    /// </summary>
    [Serializable]
    internal class TouchpointApiResponse
    {
        public TouchpointResponse data;
        public int statusCode;
        public string message;
    }

    /// <summary>
    /// API wrapper for touchpoint list response
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
        /// <param name="name">Touchpoint name (e.g., "ftue", "daily_reward")</param>
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
                var encodedCoinId = Uri.EscapeDataString(coinId);
                var url = $"{baseUrl}/touchpoints/name/{encodedName}?coinId={encodedCoinId}";

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
                    TouchpointApiResponse apiResponse = JsonUtility.FromJson<TouchpointApiResponse>(json);
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
                var encodedCoinId = Uri.EscapeDataString(coinId);
                var url = $"{baseUrl}/touchpoints/{encodedId}?coinId={encodedCoinId}";

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
                    TouchpointApiResponse apiResponse = JsonUtility.FromJson<TouchpointApiResponse>(json);
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
        /// <returns>TouchpointListResponse or null on error</returns>
        public static async Task<TouchpointListResponse> ListTouchpoints()
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlaySuper] Cannot list touchpoints - API key not set");
                return null;
            }

            try
            {
                var client = new HttpClient();
                var url = $"{baseUrl}/touchpoints";

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
                    TouchpointListApiResponse apiResponse = JsonUtility.FromJson<TouchpointListApiResponse>(json);
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
