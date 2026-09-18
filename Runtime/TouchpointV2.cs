using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace PlaySuperUnity
{
    #region Response Types

    /// <summary>
    /// The frame the SDK renders. The studio picks this in the console, either
    /// from the preset library or by uploading their own artwork.
    /// </summary>
    [Serializable]
    public class TouchpointV2Asset
    {
        /// <summary>"PRESET" or "UPLOAD".</summary>
        public string source;

        /// <summary>The frame image. Always present when the asset is.</summary>
        public string imageUrl;

        /// <summary>"PORTRAIT" or "LANDSCAPE".</summary>
        public string orientation;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? width;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? height;
    }

    /// <summary>
    /// What the offer costs. Every figure is already computed server-side for
    /// this player and this game's coin — the game does no arithmetic.
    /// </summary>
    [Serializable]
    public class TouchpointV2Pricing
    {
        /// <summary>ISO currency, e.g. "INR" or "USD".</summary>
        public string currency;

        /// <summary>Struck-through reference: MRP, or a gift card's face value.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? mrp;

        /// <summary>Cash payable before coins are applied.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? price;

        /// <summary>Cash payable once every eligible coin is spent.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? priceAfterCoins;

        /// <summary>
        /// The "% OFF" badge. Pre-coin — it is the MRP to price discount, and
        /// deliberately does not fold in the coin saving, which is shown
        /// separately.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? discountPct;
    }

    /// <summary>
    /// The coin side of the offer. Null when this game's coin cannot be spent
    /// on the item, or when the saving rounds to nothing.
    /// </summary>
    [Serializable]
    public class TouchpointV2Coins
    {
        public string coinId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string coinName;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string coinImageUrl;

        /// <summary>How many coins the player spends to get <c>coinDiscount</c> off.</summary>
        public int coinsRequired;

        /// <summary>The cash value those coins are worth against this item.</summary>
        public float coinDiscount;
    }

    /// <summary>Where the frame goes when tapped.</summary>
    [Serializable]
    public class TouchpointV2Cta
    {
        /// <summary>Button copy, e.g. "Claim now".</summary>
        public string label;

        /// <summary>
        /// Absolute store URL with any UTMs already appended. Hand it to
        /// <see cref="TouchpointV2Manager.OpenCta"/> rather than building a URL.
        /// </summary>
        public string url;
    }

    /// <summary>
    /// The concrete offer that fills the frame's centre.
    /// </summary>
    [Serializable]
    public class TouchpointV2Item
    {
        /// <summary>"PRODUCT", "COUPON" or "GIFT_CARD".</summary>
        public string type;

        public string id;

        public string title;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string subtitle;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string brandName;

        /// <summary>The offer's art, drawn inside the frame.</summary>
        public string imageUrl;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointV2Pricing pricing;

        /// <summary>
        /// Pre-formatted offer line for coupons, e.g. "Flat 40% off up to ₹200".
        /// Display as given — it is authored copy, not a computed string.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string offerText;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointV2Coins coins;

        public TouchpointV2Cta cta;
    }

    /// <summary>
    /// One screen's touchpoint: the frame plus the offer inside it.
    /// </summary>
    [Serializable]
    public class TouchpointV2Response
    {
        /// <summary>The screen this was served for, echoed back.</summary>
        public string screen;

        /// <summary>Null when the studio has no active placement for the screen.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string placementId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointV2Asset asset;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TouchpointV2Item item;

        /// <summary>ISO-8601 timestamp the server composed this at.</summary>
        public string servedAt;

        #region Helper Properties

        /// <summary>
        /// Whether there is something to draw.
        /// <para>
        /// The API returns asset and item together or not at all — a frame with
        /// an empty centre reads as a broken build — so this single check is
        /// enough before rendering. Check it rather than null-testing the
        /// response: a successful call with nothing configured is a 200, and is
        /// a normal state rather than a failure.
        /// </para>
        /// </summary>
        [JsonIgnore]
        public bool HasTouchpoint => asset != null && item != null;

        /// <summary>Convenience: the frame image, or null when nothing is configured.</summary>
        [JsonIgnore]
        public string FrameImageUrl => asset?.imageUrl;

        /// <summary>Convenience: the offer art, or null when nothing is configured.</summary>
        [JsonIgnore]
        public string OfferImageUrl => item?.imageUrl;

        /// <summary>Whether the frame is taller than it is wide.</summary>
        [JsonIgnore]
        public bool IsPortrait => asset?.orientation == "PORTRAIT";

        #endregion
    }

    #endregion

    #region Internal API Response Wrapper

    [Serializable]
    internal class TouchpointV2ApiResponse
    {
        public TouchpointV2Response data;
        public int statusCode;
        public string message;
    }

    #endregion

    /// <summary>
    /// Touchpoints 2.0.
    ///
    /// <para>
    /// A touchpoint is an offer the studio places on one of their game's
    /// screens from the PlaySuper console. The game asks for the screen it is
    /// about to show; the server answers with the artwork the studio chose and
    /// one concrete offer already composed into it — priced for this player,
    /// with the coin maths done. The game draws two images and opens a URL.
    /// </para>
    ///
    /// <para>
    /// This replaces <see cref="TouchpointManager"/>, whose nodes the game had
    /// to walk and interpret. Nothing here needs interpreting.
    /// </para>
    /// </summary>
    public static class TouchpointV2Manager
    {
        /// <summary>
        /// Screens a touchpoint can be placed on.
        ///
        /// <para>
        /// Only <c>home</c> is live. The rest exist in the taxonomy but have no
        /// artwork yet, and the API rejects them with a 400 naming the valid
        /// ones. These slugs ship inside game code, so they are stable
        /// identifiers rather than display labels.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Named <c>Screens</c>, not <c>Screen</c>: <c>UnityEngine.Screen</c> is
        /// in scope in every file that does <c>using UnityEngine</c>, and a
        /// nested type shadowing it is a trap for whoever edits this next.
        /// </remarks>
        public static class Screens
        {
            public const string Home = "home";
        }

        /// <summary>Offer kinds, for the optional <c>type</c> filter.</summary>
        public static class ItemType
        {
            public const string Product = "product";
            public const string Coupon = "coupon";
            public const string GiftCard = "gift-card";
        }

        /// <summary>
        /// Fetch the touchpoint for a screen.
        /// </summary>
        ///
        /// <example>
        /// <code>
        /// var tp = await TouchpointV2Manager.Serve(TouchpointV2Manager.Screens.Home);
        /// if (tp != null &amp;&amp; tp.HasTouchpoint)
        /// {
        ///     // draw tp.FrameImageUrl, with tp.OfferImageUrl in its centre
        ///     // button label: tp.item.cta.label
        /// }
        /// </code>
        /// </example>
        ///
        /// <param name="screen">
        /// Which screen is about to be shown. See <see cref="Screens"/>.
        /// </param>
        /// <param name="type">
        /// Optional offer kind. Needed only when the studio has placed more than
        /// one kind on this screen — the API rejects an ambiguous call rather
        /// than picking for you. Omit it otherwise.
        /// </param>
        /// <param name="utmSource">Optional, appended to the CTA URL.</param>
        /// <param name="utmMedium">Optional, appended to the CTA URL.</param>
        /// <param name="utmCampaign">Optional, appended to the CTA URL.</param>
        ///
        /// <returns>
        /// The composed touchpoint, or <c>null</c> if the request itself failed.
        ///
        /// <para>
        /// <b>A non-null result with <see cref="TouchpointV2Response.HasTouchpoint"/>
        /// false is not an error.</b> It means the studio has not configured this
        /// screen, or has but no catalogue item is currently available. Both are
        /// ordinary states and the game should simply draw nothing. Only
        /// <c>null</c> means the call did not succeed.
        /// </para>
        /// </returns>
        public static async Task<TouchpointV2Response> Serve(
            string screen,
            string type = null,
            string utmSource = null,
            string utmMedium = null,
            string utmCampaign = null)
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlaySuper] Cannot serve touchpoint - SDK not initialized (no API key)");
                return null;
            }

            if (string.IsNullOrEmpty(screen))
            {
                Debug.LogError("[PlaySuper] Cannot serve touchpoint - screen is required");
                return null;
            }

            try
            {
                // Built rather than concatenated: a screen or utm carrying a
                // space or ampersand would otherwise truncate the query and the
                // server would answer for the wrong screen.
                var query = new List<string>
                {
                    $"screen={Uri.EscapeDataString(screen)}"
                };

                if (!string.IsNullOrEmpty(type))
                    query.Add($"type={Uri.EscapeDataString(type)}");
                if (!string.IsNullOrEmpty(utmSource))
                    query.Add($"utm_source={Uri.EscapeDataString(utmSource)}");
                if (!string.IsNullOrEmpty(utmMedium))
                    query.Add($"utm_medium={Uri.EscapeDataString(utmMedium)}");
                if (!string.IsNullOrEmpty(utmCampaign))
                    query.Add($"utm_campaign={Uri.EscapeDataString(utmCampaign)}");

                var url = $"{baseUrl}/v2/touchpoints/serve?{string.Join("&", query)}";

                using (var webRequest = UnityWebRequest.Get(url))
                {
                    webRequest.SetRequestHeader("Accept", "application/json");
                    webRequest.SetRequestHeader("x-api-key", apiKey);

                    // Sent when the player is signed in so the offer can be
                    // priced for them. The endpoint authenticates on the API
                    // key alone, so an anonymous call still returns a
                    // touchpoint — just without player-specific pricing.
                    if (!string.IsNullOrEmpty(authToken))
                        webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string json = webRequest.downloadHandler.text;
                        var apiResponse = JsonConvert.DeserializeObject<TouchpointV2ApiResponse>(json);
                        return apiResponse?.data;
                    }

                    // 400 is worth calling out separately: it is what an
                    // unrecognised screen returns, and the body names the
                    // screens that would have worked. Logging only the status
                    // would send the studio hunting for a configuration problem
                    // that does not exist.
                    if (webRequest.responseCode == 400)
                    {
                        Debug.LogError(
                            $"[PlaySuper] Touchpoint request rejected for screen '{screen}': " +
                            $"{webRequest.downloadHandler?.text}");
                    }
                    else
                    {
                        Debug.LogError(
                            $"[PlaySuper] Error serving touchpoint: {webRequest.responseCode} - {webRequest.error}");
                    }

                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaySuper] Unexpected error in Serve: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Open the store at the offer the touchpoint was advertising.
        ///
        /// <para>
        /// Hands the CTA's own URL to the store, so the player lands on the item
        /// they tapped rather than the storefront. The URL already carries the
        /// campaign attribution the server appended, which
        /// <c>OpenStore</c> reads back off it — building the URL by hand loses
        /// that, and the visit stops being attributable to the touchpoint.
        /// </para>
        /// </summary>
        /// <param name="touchpoint">The response a <see cref="Serve"/> call returned.</param>
        public static void OpenCta(TouchpointV2Response touchpoint)
        {
            var url = touchpoint?.item?.cta?.url;

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[PlaySuper] OpenCta called with no touchpoint to open");
                return;
            }

            if (PlaySuperUnitySDK.Instance == null)
            {
                Debug.LogError("[PlaySuper] Cannot open store - SDK not initialized");
                return;
            }

            PlaySuperUnitySDK.Instance.OpenStore(url);
        }
    }
}
