using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gpm.WebView;
using UnityEngine;
using UnityEngine.Networking;

namespace PlaySuperUnity
{
    internal class WebView
    {
        private static ScreenOrientation originalOrientation;
        private static bool isPollingTransactions = false;
        private static Coroutine pollingCoroutine;
        private static bool isPrewarmed = false;
        private static bool isPrefetched = false;

        // Valid WHATWG scheme — iOS 26+ requires this. Legacy kept for old store builds.
        private const string CustomScheme = "psbridge";
        private const string LegacyScheme = "USER_CUSTOM_SCHEME";

        // UPI / payment-app URL schemes emitted by the Cashfree checkout when the
        // user taps a UPI app. Intercepted via GPM's scheme list (interception
        // cancels the WebView navigation — a WebView cannot load these itself)
        // and launched natively in LaunchExternalPaymentApp. "intent:" is the
        // Chrome-style wrapper some apps use and needs Intent.parseUri on Android.
        private static readonly List<string> PaymentAppSchemes = new List<string>
        {
            "upi:", "intent:", "tez:", "phonepe:", "paytmmp:", "gpay:", "credpay:", "bhim:"
        };

        private static string cachedPatchedUserAgent;

        /// <summary>
        /// Build the store URL with credentials and utm_content as query params.
        /// The store consumes these synchronously on render (via themeProvider),
        /// eliminating the race between JS-injection and React hydration.
        /// </summary>
        private static string BuildStoreUrl(string baseUrl, string utmContent)
        {
            var queryParams = new List<string>();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string authToken = PlaySuperUnitySDK.GetAuthToken();

            // After Logout(), tell the store to wipe stale localStorage from
            // the previous user before applying the new auth.
            if (PlaySuperUnitySDK.ConsumeLogoutPending())
                queryParams.Add("clearSession=true");

            if (!string.IsNullOrEmpty(apiKey))
                queryParams.Add($"apiKey={Uri.EscapeDataString(apiKey)}");
            if (!string.IsNullOrEmpty(authToken))
                queryParams.Add($"authToken={Uri.EscapeDataString(authToken)}");
            if (!string.IsNullOrEmpty(utmContent))
                queryParams.Add($"utm_content={Uri.EscapeDataString(utmContent)}");
            queryParams.Add($"psSdkScheme={CustomScheme}");

            if (queryParams.Count == 0) return baseUrl;

            string separator = baseUrl.Contains("?") ? "&" : "?";
            return $"{baseUrl}{separator}{string.Join("&", queryParams)}";
        }

        /// <summary>
        /// Android default WebView user agent with the "; wv" and "Version/4.0"
        /// WebView markers stripped, so the store presents as regular mobile
        /// Chrome. Cashfree (and other PSPs) hide the UPI Intent app buttons
        /// when they detect a WebView UA — they can't know the host handles
        /// upi:// links. We do (see PaymentAppSchemes), so opting out of the
        /// marker is safe and restores UPI at checkout.
        /// Returns empty on iOS/Editor or on failure → GPM keeps its default UA
        /// (its native side only applies non-empty values).
        /// </summary>
        private static string GetPatchedUserAgent()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (cachedPatchedUserAgent != null) return cachedPatchedUserAgent;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var webSettings = new AndroidJavaClass("android.webkit.WebSettings"))
                {
                    string ua = webSettings.CallStatic<string>("getDefaultUserAgent", activity);
                    if (!string.IsNullOrEmpty(ua))
                    {
                        cachedPatchedUserAgent = ua.Replace("; wv", "").Replace("Version/4.0 ", "");
                        return cachedPatchedUserAgent;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] Could not derive patched user agent: {ex.Message}");
            }
            return string.Empty;
#else
            return string.Empty;
#endif
        }

        private static List<string> GetSchemeList()
        {
            var schemes = new List<string> { CustomScheme, LegacyScheme };
#if UNITY_ANDROID
            // Android only — iOS checkout behavior is deliberately untouched.
            schemes.AddRange(PaymentAppSchemes);
#endif
            return schemes;
        }

        private static bool IsPaymentAppUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            foreach (string scheme in PaymentAppSchemes)
            {
                if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Hand a payment-app URL to the OS. The WebView stays open underneath;
        /// when the user returns from the UPI app, the checkout page resumes its
        /// own payment-status polling.
        /// </summary>
        private static void LaunchExternalPaymentApp(string url)
        {
            Debug.Log($"[PlaySuper] Launching external payment app for scheme: {url.Split(':')[0]}");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (url.StartsWith("intent:", StringComparison.OrdinalIgnoreCase))
            {
                LaunchAndroidIntentUri(url);
                return;
            }
#endif
            Application.OpenURL(url);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void LaunchAndroidIntentUri(string url)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = intentClass.CallStatic<AndroidJavaObject>(
                    "parseUri", url, 1 /* Intent.URI_INTENT_SCHEME */))
                {
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception ex)
            {
                // Target app not installed (ActivityNotFoundException) or bad URI.
                Debug.LogWarning($"[PlaySuper] Could not launch payment intent: {ex.Message}");
                string fallback = ExtractIntentFallbackUrl(url);
                if (!string.IsNullOrEmpty(fallback))
                {
                    Application.OpenURL(fallback);
                }
            }
        }

        /// <summary>
        /// Extract the Chrome-convention S.browser_fallback_url extra from an
        /// intent: URI, used when the target app is not installed.
        /// </summary>
        private static string ExtractIntentFallbackUrl(string intentUrl)
        {
            const string key = "S.browser_fallback_url=";
            int i = intentUrl.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            int start = i + key.Length;
            int end = intentUrl.IndexOf(';', start);
            string encoded = end < 0 ? intentUrl.Substring(start) : intentUrl.Substring(start, end - start);
            try
            {
                return Uri.UnescapeDataString(encoded);
            }
            catch
            {
                return null;
            }
        }
#endif

        public static void ShowUrlFullScreen(bool isDev = false, string url = null, string utmContent = null)
        {
            // Show branded loading screen instantly so the user gets visual
            // feedback before the native WebView paints (~50–300ms gap).
            // Dismissed in OnCallback when GpmWebView fires Open / Close.
            LoadingScreen.Show();

            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            string baseUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);
            string targetUrl = BuildStoreUrl(baseUrl, utmContent);

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.FULLSCREEN,
                    orientation = GpmOrientation.PORTRAIT,
                    isNavigationBarVisible = false,
                    userAgentString = GetPatchedUserAgent(),
#if UNITY_IOS
                    contentMode = GpmWebViewContentMode.MOBILE,
#endif
                    supportMultipleWindows = true,
                },
                OnCallback,
                GetSchemeList()
            );
        }

        public static void ShowUrlPopupDefault(bool isDev = false, string url = null, string utmContent = null)
        {
            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            string baseUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);
            string targetUrl = BuildStoreUrl(baseUrl, utmContent);

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.PORTRAIT,
                    isNavigationBarVisible = false,
                    userAgentString = GetPatchedUserAgent(),
#if UNITY_IOS
                    contentMode = GpmWebViewContentMode.MOBILE,
                    isMaskViewVisible = true,
#endif
                    supportMultipleWindows = true,
                },
                OnCallback,
                GetSchemeList()
            );
        }

        public static void ShowUrlPopupPositionSize(bool isDev = false, string url = null, string utmContent = null)
        {
            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            Rect safeArea = Screen.safeArea;
            string baseUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);
            string targetUrl = BuildStoreUrl(baseUrl, utmContent);

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.PORTRAIT,
                    isNavigationBarVisible = false,
                    userAgentString = GetPatchedUserAgent(),
#if UNITY_IOS
                    contentMode = GpmWebViewContentMode.MOBILE,
                    isMaskViewVisible = true,
#endif
                    position = new GpmWebViewRequest.Position
                    {
                        hasValue = true,
                        x = (int)safeArea.xMin,
                        y = (int)(Screen.height - safeArea.height),
                    },
                    size = new GpmWebViewRequest.Size
                    {
                        hasValue = true,
                        width = (int)safeArea.width,
                        height = (int)safeArea.height,
                    },
                    supportMultipleWindows = true,
                },
                OnCallback,
                GetSchemeList()
            );
        }

        private static void OnCallback(
            GpmWebViewCallback.CallbackType callbackType,
            string data,
            GpmWebViewError error
        )
        {
            Debug.Log($"WebView callback: {callbackType}, URL: {data}");

            switch (callbackType)
            {
                case GpmWebViewCallback.CallbackType.Close:
                    // Defensive: in case Open never fired (e.g., WebView failed
                    // to open), ensure the loading screen doesn't get orphaned.
                    LoadingScreen.Hide();

                    // Stop polling for transaction signals
                    StopTransactionPolling();

                    // Restore original orientation
                    Screen.orientation = originalOrientation;

                    // Notify SDK subscribers
                    PlaySuperUnitySDK.NotifyStoreClosed();

                    // Fire-and-forget on main thread (SendEvent uses UnityWebRequest internally)
                    _ = AnalyticsManager.SendEvent(
                        Constants.AnalyticsEvent.STORE_CLOSE,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    );
                    break;

                case GpmWebViewCallback.CallbackType.Open:
                    // WebView is now painted on top of everything; dismiss
                    // the SDK loading screen. The store's EntryLoader takes
                    // over for the JS-load / hydration phase inside the WebView.
                    LoadingScreen.Hide();

                    // Notify SDK subscribers
                    PlaySuperUnitySDK.NotifyStoreOpened();

                    // Start polling for transaction signals from the store
                    StartTransactionPolling();

                    // Fire-and-forget on main thread (SendEvent uses UnityWebRequest internally)
                    _ = AnalyticsManager.SendEvent(
                        Constants.AnalyticsEvent.STORE_OPEN,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    );
                    break;

                case GpmWebViewCallback.CallbackType.PageStarted:
                    GpmWebView.ExecuteJavaScript("localStorage.getItem('authToken');");

                    Debug.Log($"Page started: {data}");

                    // Check for malformed schemes (iOS appends to URL instead of triggering scheme callback)
                    if (data.Contains("psbridge://close") || data.Contains("psbridge:/close") ||
                        data.Contains("USER_CUSTOM_SCHEME://close") || data.Contains("USER_CUSTOM_SCHEME:/close"))
                    {
                        Debug.Log("Detected malformed close scheme in URL - closing WebView");
                        GpmWebView.Close();
                        break;
                    }

                    // More reliable URL check
                    if (data.Contains("store.playsuper.club"))
                    {
                        Debug.Log("Store URL detected - Injecting credentials");
                        InjectCredentials();
                    }
                    break;

                case GpmWebViewCallback.CallbackType.PageLoad:
                    // iOS 26+ rejects ExecuteJavaScript at PageStarted (document not ready).
                    // Re-inject here once the JS context is committed.
                    if (data != null && data.Contains("store.playsuper.club"))
                    {
                        InjectCredentials();
                    }
                    break;

                case GpmWebViewCallback.CallbackType.ExecuteJavascript:
                    Debug.Log("ExecuteJavascript: " + data);
                    if (string.IsNullOrEmpty(data) == false && data.Length > 2 && data != "null" && data != "<null>")
                    {
                        // Check if this is a transaction signal from polling
                        if (data.Contains("PS_TXN:"))
                        {
                            Debug.Log("[PlaySuper] Transaction signal detected via polling");
                            PlaySuperUnitySDK.HandleRealtimeTransaction();
                            break;
                        }

                        try
                        {
                            // Extract the token - expected format: {"accessToken":"<token>"}
                            int startIndex = 14;
                            if (data.Length <= startIndex)
                            {
                                Debug.LogWarning("[PlaySuper] Token data too short to parse");
                                break;
                            }

                            int endIndex = data.IndexOf("\"", startIndex);
                            if (endIndex < startIndex)
                            {
                                Debug.LogWarning("[PlaySuper] Invalid token format - closing quote not found");
                                break;
                            }

                            string token = data.Substring(startIndex, endIndex - startIndex);

                            Debug.Log("Token received"); // Don't log the actual token

                            if (!PlaySuperUnitySDK.IsLoggedIn())
                            {
                                // Handle token on main thread since it affects UI state
                                PlaySuperUnitySDK.Instance.OnTokenReceive(token);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error processing token: {ex.Message}");
                        }
                    }
                    break;

                case GpmWebViewCallback.CallbackType.Scheme:
                    Debug.Log($"Custom scheme received: {data}");
                    if (data.Contains("://close"))
                    {
                        GpmWebView.Close();
                    }
#if UNITY_ANDROID
                    else if (IsPaymentAppUrl(data))
                    {
                        // UPI Intent tap from the Cashfree checkout — open the
                        // payment app; the WebView stays open for status polling.
                        LaunchExternalPaymentApp(data);
                    }
#endif
                    break;
            }
        }

        private static void StartTransactionPolling()
        {
            if (isPollingTransactions) return;

            if (!PlaySuperUnitySDK.IsLoggedIn() || !SdkTransactionSyncManager.HasVisitedStore())
            {
                Debug.Log($"[PlaySuper] Skipping transaction polling (loggedIn={PlaySuperUnitySDK.IsLoggedIn()}, visitedStore={SdkTransactionSyncManager.HasVisitedStore()})");
                return;
            }

            isPollingTransactions = true;

            if (PlaySuperUnitySDK.Instance != null)
            {
                pollingCoroutine = PlaySuperUnitySDK.Instance.StartCoroutine(PollTransactionSignal());
            }
            Debug.Log("[PlaySuper] Transaction polling started");
        }

        internal static void StopTransactionPolling()
        {
            isPollingTransactions = false;
            if (pollingCoroutine != null && PlaySuperUnitySDK.Instance != null)
            {
                PlaySuperUnitySDK.Instance.StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
            Debug.Log("[PlaySuper] Transaction polling stopped");
        }

        // TODO(v4.3.0): replace this JS-exec poll with a push-based signal from
        // the store webview.
        //
        // Why we still poll in v4.2.0:
        //   The natural Android push — store calls
        //     window.location.href = 'USER_CUSTOM_SCHEME://txn?ts=' + ts;
        //   — does not fire the WebView's shouldOverrideUrlLoading callback on
        //   Chromium-backed WebView when invoked without a user gesture. The
        //   navigation is treated as a top-level URL load instead, and the
        //   WebView either errors with ERR_UNKNOWN_URL_SCHEME or routes it out
        //   as an external intent. iOS WKWebView calls decidePolicyForNavigation
        //   for programmatic navigations too, which is why the existing
        //   USER_CUSTOM_SCHEME://close handler works there.
        //
        // Candidates to evaluate for v4.3.0:
        //   1. Iframe-src trick — store creates a hidden iframe whose src is the
        //      custom scheme URL; sub-resource loads DO fire shouldOverrideUrl-
        //      Loading even without a user gesture. Requires store-side change.
        //   2. JavascriptInterface bridge — add a native @JavascriptInterface
        //      (Android) / WKScriptMessageHandler (iOS) so the store can call
        //      window.PSBridge.onTransaction(ts) directly. Requires extending
        //      GpmWebView or wrapping it with a native plugin layer.
        //
        // In the meantime: poll at 5 s instead of 1 s. Cuts JS-exec JNI traffic
        // by 80% while the store WebView is open, with a worst-case 5 s txn-
        // recognition delay that product has signed off on.
        private static IEnumerator PollTransactionSignal()
        {
            while (isPollingTransactions)
            {
                // Read and clear the signal in one atomic JS call.
                // Returns "PS_TXN:<timestamp>" if signal exists, otherwise null.
                GpmWebView.ExecuteJavaScript(@"
                    (function() {
                        var s = localStorage.getItem('psTransactionSignal');
                        if (s) {
                            localStorage.removeItem('psTransactionSignal');
                            return 'PS_TXN:' + s;
                        }
                        return null;
                    })()
                ");

                yield return new WaitForSeconds(5f);
            }
        }

        /// <summary>
        /// Inject SDK credentials and the Unity-WebView flag into the store's
        /// localStorage in a single ExecuteJavaScript call.
        ///
        /// Previously this method made 3–4 separate JNI hops (apiKey set,
        /// isUnityWebView set, authToken set, console-log verify) every time the
        /// store URL loaded. Each call crossed the Unity → Android UI thread →
        /// WebView JS engine bridge while Chromium was still hydrating the page,
        /// adding load at the worst moment for low-end devices. Batching into one
        /// JS string collapses those hops to a single bridge crossing.
        /// </summary>
        internal static void InjectCredentials()
        {
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string token = PlaySuperUnitySDK.GetAuthToken();
            bool hasToken = !string.IsNullOrEmpty(token);

            Debug.Log($"[PlaySuper] Injecting credentials (token present: {hasToken})");

            string safeApiKey = EscapeForSingleQuotedJs(apiKey);
            string safeToken = hasToken ? EscapeForSingleQuotedJs(token) : null;

            // Single batched script: API key + Unity-WebView flag always, auth
            // token only when present. Final console.log gives us a verification
            // line in DevTools without echoing the secret.
            string js =
                $"localStorage.setItem('apiKey', '{safeApiKey}');" +
                "localStorage.setItem('isUnityWebView', 'true');" +
                $"localStorage.setItem('psSdkScheme', '{CustomScheme}');" +
                (hasToken ? $"localStorage.setItem('authToken', '{safeToken}');" : "") +
                $"console.log('[PlaySuper] credentials injected (token: ' + ({(hasToken ? "true" : "false")}) + ')');";

            GpmWebView.ExecuteJavaScript(js);
        }

        /// <summary>
        /// Escape a value to be safely embedded inside a single-quoted JS string
        /// literal. Handles backslashes, quotes, and newlines.
        /// </summary>
        private static string EscapeForSingleQuotedJs(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static int getWebOrientation()
        {
            int or;
            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    or = GpmOrientation.PORTRAIT;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    or = GpmOrientation.PORTRAIT_REVERSE;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    or = GpmOrientation.LANDSCAPE_LEFT;
                    break;
                case ScreenOrientation.LandscapeRight:
                    or = GpmOrientation.LANDSCAPE_REVERSE;
                    break;
                default:
                    or = GpmOrientation.UNSPECIFIED;
                    break;
            }
            return or;
        }

        #region WebView Prewarming

        /// <summary>
        /// Pre-warm the WebView engine and prefetch store resources.
        /// Call this during SDK init to reduce OpenStore latency.
        /// </summary>
        public static void Prewarm(bool isDev)
        {
            string storeUrl = isDev ? Constants.devStoreUrl : Constants.prodStoreUrl;

            // Layer 1: warm DNS / TCP / TLS / HTTP cache — cross-platform, off-thread.
            _ = PrefetchStoreResources(storeUrl);

            // Layer 2: warm the Chromium/WebView provider on the Android UI thread,
            // but only when the main looper is idle. Android-only — iOS WebKit is
            // already linked into every process and WKWebView content-process boot
            // is async, so there is no main-thread cost to dodge there.
#if UNITY_ANDROID && !UNITY_EDITOR
            WarmChromiumOnIdle();
#endif
        }

        /// <summary>
        /// Prefetch store URL to warm DNS cache, TLS handshake, and HTTP cache.
        /// This runs a lightweight HEAD request in background.
        /// </summary>
        private static async Task PrefetchStoreResources(string storeUrl)
        {
            if (isPrefetched) return;
            isPrefetched = true;

            try
            {
                using (var request = UnityWebRequest.Head(storeUrl))
                {
                    request.timeout = 10; // 10 second timeout
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                        await Task.Yield();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[PlaySuper] Store prefetch complete - DNS and connection warmed");
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper] Store prefetch failed: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] Store prefetch error: {ex.Message}");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Warm the WebView/Chromium provider on the Android UI thread during an
        /// idle moment, so the first real OpenStore() does not pay Chromium's
        /// heavy engine-init cost on the UI thread while the user is waiting.
        ///
        /// History: an earlier version posted `new WebView(activity); destroy();`
        /// to the UI thread, which blocked Thread 1 "main" for seconds inside
        /// libwebviewchromium.so on low-end devices — the dominant user-perceived
        /// ANR signature on the affected game (Play Console issue
        /// 89a40858d196a8f5d014721c5dd3cc87). A follow-up moved the warm to a
        /// background thread (startSafeBrowsing), but WebView init has UI-thread
        /// affinity and takes a process-global provider lock, so an off-thread
        /// warm fights that model and only partially warms the construction path.
        ///
        /// This version registers a one-shot MessageQueue.IdleHandler on the UI
        /// thread: the heavy Chromium provider init runs once, early, but only
        /// when the Android main looper is idle — so it never blocks a frame the
        /// user is waiting on. WebSettings.getDefaultUserAgent(context) is the
        /// documented lightweight call that forces WebViewFactory.getProvider()
        /// (the expensive one-time engine/provider load) without constructing a
        /// View or attaching a surface.
        /// </summary>
        private static void WarmChromiumOnIdle()
        {
            if (isPrewarmed) return;
            isPrewarmed = true;

            AndroidJavaObject activity;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
                if (activity == null)
                {
                    Debug.LogWarning("[PlaySuper] Chromium warm skipped — no currentActivity");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] Chromium warm skipped: {ex.Message}");
                return;
            }

            // MessageQueue is per-thread, so register the idle handler from the
            // UI thread itself. Looper.myQueue() (API 1) then returns the main
            // queue — unlike Looper.getQueue() which is API 23+.
            var register = new AndroidJavaRunnable(() =>
            {
                try
                {
                    using (var looper = new AndroidJavaClass("android.os.Looper"))
                    using (var queue = looper.CallStatic<AndroidJavaObject>("myQueue"))
                    {
                        queue.Call("addIdleHandler", new IdleChromiumWarmer(activity));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlaySuper] IdleHandler registration failed: {ex.Message}");
                }
            });

            try
            {
                activity.Call("runOnUiThread", register);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] runOnUiThread failed: {ex.Message}");
            }
        }

        /// <summary>
        /// One-shot <c>MessageQueue.IdleHandler</c>: warms the WebView provider the
        /// first time the Android main thread goes idle, then removes itself by
        /// returning <c>false</c> from <c>queueIdle()</c>.
        /// </summary>
        private class IdleChromiumWarmer : AndroidJavaProxy
        {
            private readonly AndroidJavaObject _context;

            public IdleChromiumWarmer(AndroidJavaObject context)
                : base("android.os.MessageQueue$IdleHandler")
            {
                _context = context;
            }

            // bool queueIdle() — return false to run exactly once, then detach.
            // Must be public: AndroidJavaProxy resolves proxy methods by name via
            // reflection and only binds public members.
            public bool queueIdle()
            {
                try
                {
                    using (var webSettings = new AndroidJavaClass("android.webkit.WebSettings"))
                    {
                        webSettings.CallStatic<string>("getDefaultUserAgent", _context);
                    }
                    Debug.Log("[PlaySuper] Chromium warmed on idle (provider initialized)");
                }
                catch (Exception ex)
                {
                    // Any provider quirk: silent fall-back. The HEAD prefetch still
                    // ran and the WebView will create on first real OpenStore.
                    Debug.LogWarning($"[PlaySuper] Chromium idle warm failed: {ex.Message}");
                }
                return false;
            }
        }
#endif

        #endregion
    }
}
