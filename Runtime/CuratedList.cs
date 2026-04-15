using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace PlaySuperUnity
{
    /// <summary>
    /// Type of curated list to fetch
    /// </summary>
    public enum CuratedListType
    {
        Products,
        Rewards
    }

    #region Response Classes

    /// <summary>
    /// Base API response wrapper
    /// </summary>
    [Serializable]
    public class ApiResponseWrapper<T>
    {
        public T data;
        public int statusCode;
        public string message;
        public string requestId;
        public string timestamp;
    }

    /// <summary>
    /// Data container for curated products
    /// </summary>
    [Serializable]
    public class CuratedProductsData
    {
        public List<CuratedProduct> products;
    }

    /// <summary>
    /// Data container for curated rewards
    /// </summary>
    [Serializable]
    public class CuratedRewardsData
    {
        public List<CuratedReward> rewards;
    }

    /// <summary>
    /// Response for curated products endpoint
    /// </summary>
    [Serializable]
    public class CuratedProductsResponse : ApiResponseWrapper<CuratedProductsData> { }

    /// <summary>
    /// Response for curated rewards endpoint
    /// </summary>
    [Serializable]
    public class CuratedRewardsResponse : ApiResponseWrapper<CuratedRewardsData> { }

    #endregion

    #region Product Classes

    [Serializable]
    public class CuratedProduct
    {
        public string id;
        public string sourceId;
        public string pluginName;
        public string name;

        /// <summary>
        /// Call-to-action URL for this product in the PlaySuper store
        /// </summary>
        [JsonIgnore]
        public string ctaUrl;
        public string description;
        public string plainTextDescription;
        public string brandName;
        public string imageUrl;
        public List<string> images;
        public bool isActive;
        public string categoryId;
        public string sourceCategory;
        public string sourceSubCategory;
        public List<ProductSpecification> specifications;
        public string createdAt;
        public string updatedAt;
        public string lastSyncedAt;
        public float? rating;
        public ProductInventory inventory;
        public bool hasException;
        public List<SkuStore> skus;
        public List<OptionType> optionTypes;
    }

    [Serializable]
    public class ProductSpecification
    {
        public string group;
        public string label;
        public string value;
        public bool isHighlight;
    }

    [Serializable]
    public class OptionType
    {
        public string id;
        public string name;
        public string slug;
        public string displayName;
        public string inputType;
        public int displayOrder;
        public bool isRequired;
        public List<OptionValue> values;
    }

    [Serializable]
    public class OptionValue
    {
        public string id;
        public string value;
        public string label;
        public string colorCode;
        public string imageUrl;
        public int displayOrder;
    }

    [Serializable]
    public class SkuStore
    {
        public string id;
        public string name;
        public string barcode;
        public string imageUrl;
        public SkuInventory inventory;
        public string createdAt;
        public string updatedAt;
        public bool isActive;
        public bool isPricingAvailable;
        public float? competitorPrice;
        public string competitorPlatform;
        public PlaySuperPriceStore playSuperPrice;
        public List<SkuOptionValue> optionValues;
    }

    [Serializable]
    public class SkuOptionValue
    {
        public string optionTypeId;
        public string optionTypeName;
        public string optionTypeSlug;
        public string optionValueId;
        public string value;
        public string label;
        public string colorCode;
        public string imageUrl;
    }

    [Serializable]
    public class PlaySuperPriceStore
    {
        public string listingPrice;
        public string discountedListingPrice;
        public string discountPercent;
        public string marginPercent;
        public string discountValue;
        public string marginValue;
        public string coinSpread;
        public string supplierPrice;
        public string mrp;
        public int? coinRequiredForDiscount;
        public int? coinsRequiredForMaxDiscount;
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

    #endregion

    #region Reward Classes

    [Serializable]
    public class CuratedReward
    {
        public string id;
        public string brandId;
        public string brandName;

        /// <summary>
        /// Call-to-action URL for this reward in the PlaySuper store
        /// </summary>
        [JsonIgnore]
        public string ctaUrl;
        public string organizationId;
        public RewardMetadataFull metadata;
        public RewardInventory inventory;
        public List<RewardPrice> price;
    }

    [Serializable]
    public class RewardMetadataFull
    {
        public List<string> brandCategory;
        public string brandLogoImage;
        public string brandName;
        public Dictionary<string, object> campaignAssets;
        public string campaignCoverImage;
        public string campaignExpiryDate;
        public string campaignSubTitle;
        public string campaignTitle;
        public string couponExpiryDate;
        public bool couponExpiryDateExists;
        public string howToRedeem;
        public string termsAndConditions;
        public string type;
    }

    [Serializable]
    public class RewardInventory
    {
        public int availableQuantity;
        public string type;
    }

    [Serializable]
    public class RewardPrice
    {
        public float amount;
        public string coinId;
    }

    #endregion

    #region Unified Response

    /// <summary>
    /// Unified response for curated list fetch
    /// </summary>
    [Serializable]
    public class CuratedListResponse
    {
        public CuratedListType type;
        public List<CuratedProduct> products;
        public List<CuratedReward> rewards;
    }

    #endregion

    /// <summary>
    /// Service for fetching curated lists from PlaySuper API
    /// </summary>
    public static class CuratedListService
    {
        /// <summary>
        /// Fetch a curated list by type and name
        /// </summary>
        /// <param name="type">Type of curated list (Products or Rewards)</param>
        /// <param name="listName">Name of the curated list</param>
        /// <param name="coinId">Coin ID for pricing calculations</param>
        /// <param name="apiKey">Your game's API key</param>
        /// <param name="baseUrl">Base API URL</param>
        /// <param name="version">Optional API version for rewards (e.g., "2.0.0")</param>
        /// <returns>CuratedListResponse containing products or rewards based on type</returns>
        public static async Task<CuratedListResponse> GetCuratedListAsync(
            CuratedListType type,
            string listName,
            string coinId,
            string apiKey,
            string baseUrl,
            string version = null)
        {
            if (string.IsNullOrEmpty(listName))
            {
                throw new ArgumentException("List name cannot be null or empty", nameof(listName));
            }

            if (string.IsNullOrEmpty(coinId))
            {
                throw new ArgumentException("Coin ID cannot be null or empty", nameof(coinId));
            }

            string typeSegment = type == CuratedListType.Products ? "products" : "rewards";
            string url = $"{baseUrl}/curated/{typeSegment}/{listName}?coinId={coinId}";

            if (type == CuratedListType.Rewards && !string.IsNullOrEmpty(version))
            {
                url += $"&version={version}";
            }

            using (var webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.SetRequestHeader("x-api-key", apiKey);

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[PlaySuper] Failed to fetch curated {typeSegment}: {webRequest.responseCode} - {webRequest.downloadHandler.text}");
                    throw new Exception($"Failed to fetch curated list: {webRequest.responseCode}");
                }

                string json = webRequest.downloadHandler.text;
                Debug.Log($"[PlaySuper] Curated list raw response: {json.Substring(0, Math.Min(500, json.Length))}...");

                var result = new CuratedListResponse { type = type };

                // Determine store URL based on environment
                string storeUrl = baseUrl == Constants.devApiUrl ? Constants.devStoreUrl : Constants.prodStoreUrl;

                if (type == CuratedListType.Products)
                {
                    var productsResponse = JsonConvert.DeserializeObject<CuratedProductsResponse>(json);
                    result.products = productsResponse?.data?.products ?? new List<CuratedProduct>();

                    // Populate CTA URLs for each product
                    foreach (var product in result.products)
                    {
                        product.ctaUrl = $"{storeUrl}gcommerce/products/{product.id}";
                    }

                    Debug.Log($"[PlaySuper] Parsed {result.products.Count} products");
                }
                else
                {
                    var rewardsResponse = JsonConvert.DeserializeObject<CuratedRewardsResponse>(json);
                    result.rewards = rewardsResponse?.data?.rewards ?? new List<CuratedReward>();

                    // Populate CTA URLs for each reward
                    foreach (var reward in result.rewards)
                    {
                        reward.ctaUrl = $"{storeUrl}rewards/{reward.id}";
                    }

                    Debug.Log($"[PlaySuper] Parsed {result.rewards.Count} rewards");
                }

                return result;
            }
        }
    }
}
