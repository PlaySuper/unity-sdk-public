using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
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

    [Serializable]
    public class CuratedProductsData
    {
        public List<ProductListingStore> products;
    }

    [Serializable]
    public class CuratedProductsResponse
    {
        public CuratedProductsData data;
        public int statusCode;
        public string message;
        public string requestId;
        public string timestamp;
    }

    [Serializable]
    public class CuratedRewardsData
    {
        public List<AvailableReward> rewards;
    }

    [Serializable]
    public class CuratedRewardsResponse
    {
        public CuratedRewardsData data;
        public int statusCode;
        public string message;
        public string requestId;
        public string timestamp;
    }

    #endregion

    #region Product Classes

    [Serializable]
    public class ProductListingStore
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
        public string sourceCategory;
        public string sourceSubCategory;
        public string createdAt;
        public string updatedAt;
        public string lastSyncedAt;
        public float? rating;
        public Inventory inventory;
        public bool hasException;
        public List<SkuStore> skus;
    }

    [Serializable]
    public class SkuStore
    {
        public string id;
        public string name;
        public string barcode;
        public string imageUrl;
        public Inventory inventory;
        public string createdAt;
        public string updatedAt;
        public bool isActive;
        public bool isPricingAvailable;
        public float? competitorPrice;
        public string competitorPlatform;
        public PlaySuperPriceStore playSuperPrice;
    }

    [Serializable]
    public class PlaySuperPriceStore
    {
        public float listingPrice;
        public float discountedListingPrice;
        public float discountPercent;
        public float marginPercent;
        public float discountValue;
        public float marginValue;
        public float coinSpread;
        public float? supplierPrice;
        public float? coinRequiredForDiscount;
    }

    [Serializable]
    public class Inventory
    {
        public string totalQuantity;
    }

    #endregion

    #region Reward Classes

    [Serializable]
    public class AvailableReward
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

    // RewardMetadata is already defined in Touchpoint.cs

    #endregion

    #region Unified Response

    /// <summary>
    /// Unified response for curated list fetch
    /// </summary>
    [Serializable]
    public class CuratedListResponse
    {
        public CuratedListType type;
        public List<ProductListingStore> products;
        public List<AvailableReward> rewards;
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

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[PlaySuper] Failed to fetch curated {typeSegment}: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Failed to fetch curated list: {response.StatusCode}");
                }

                string json = await response.Content.ReadAsStringAsync();
                Debug.Log($"[PlaySuper] Curated list raw response: {json.Substring(0, Math.Min(500, json.Length))}...");

                var result = new CuratedListResponse { type = type };

                if (type == CuratedListType.Products)
                {
                    var productsResponse = JsonConvert.DeserializeObject<CuratedProductsResponse>(json);
                    result.products = productsResponse?.data?.products ?? new List<ProductListingStore>();
                    Debug.Log($"[PlaySuper] Parsed {result.products.Count} products");
                }
                else
                {
                    var rewardsResponse = JsonConvert.DeserializeObject<CuratedRewardsResponse>(json);
                    result.rewards = rewardsResponse?.data?.rewards ?? new List<AvailableReward>();
                    Debug.Log($"[PlaySuper] Parsed {result.rewards.Count} rewards");
                }

                return result;
            }
        }
    }
}
