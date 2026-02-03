using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gpm.WebView;
using UnityEngine;

namespace PlaySuperUnity
{
    internal class WebView
    {
        private static ScreenOrientation originalOrientation;

        public static void ShowUrlFullScreen(bool isDev = false, string url = null, string utmContent = null)
        {
            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            string targetUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);

            // Append utm_content as query parameter if provided
            if (!string.IsNullOrEmpty(utmContent))
            {
                string separator = targetUrl.Contains("?") ? "&" : "?";
                targetUrl = $"{targetUrl}{separator}utm_content={Uri.EscapeDataString(utmContent)}";
            }

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.FULLSCREEN,
                    orientation = GpmOrientation.PORTRAIT,
#if UNITY_IOS
                    isNavigationBarVisible = true,
                    navigationBarColor = "#FFFFFF",
                    title = "",
                    isBackButtonVisible = false,
                    isForwardButtonVisible = false,
                    isCloseButtonVisible = true,
                    contentMode = GpmWebViewContentMode.MOBILE,
#else
                    isNavigationBarVisible = false,
#endif
                    supportMultipleWindows = true,
                },
                OnCallback,
                new List<string>() { "USER_CUSTOM_SCHEME" }
            );
        }

        public static void ShowUrlPopupDefault(bool isDev = false, string url = null, string utmContent = null)
        {
            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            string targetUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);

            // Append utm_content as query parameter if provided
            if (!string.IsNullOrEmpty(utmContent))
            {
                string separator = targetUrl.Contains("?") ? "&" : "?";
                targetUrl = $"{targetUrl}{separator}utm_content={Uri.EscapeDataString(utmContent)}";
            }

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.PORTRAIT,
#if UNITY_IOS
                    isNavigationBarVisible = true,
                    navigationBarColor = "#FFFFFF",
                    title = "",
                    isBackButtonVisible = false,
                    isForwardButtonVisible = false,
                    isCloseButtonVisible = true,
                    contentMode = GpmWebViewContentMode.MOBILE,
                    isMaskViewVisible = true,
#else
                    isNavigationBarVisible = false,
#endif
                    supportMultipleWindows = true,
                },
                OnCallback,
                new List<string>() { "USER_CUSTOM_SCHEME" }
            );
        }

        public static void ShowUrlPopupPositionSize(bool isDev = false, string url = null, string utmContent = null)
        {
            // Save original orientation before opening WebView
            originalOrientation = Screen.orientation;

            // Set to portrait for the WebView
            Screen.orientation = ScreenOrientation.Portrait;

            Rect safeArea = Screen.safeArea;
            string targetUrl = !string.IsNullOrEmpty(url) ? url : (isDev ? Constants.devStoreUrl : Constants.prodStoreUrl);

            // Append utm_content as query parameter if provided
            if (!string.IsNullOrEmpty(utmContent))
            {
                string separator = targetUrl.Contains("?") ? "&" : "?";
                targetUrl = $"{targetUrl}{separator}utm_content={Uri.EscapeDataString(utmContent)}";
            }

            GpmWebView.ShowUrl(
                targetUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.PORTRAIT,
#if UNITY_IOS
                    isNavigationBarVisible = true,
                    navigationBarColor = "#FFFFFF",
                    title = "",
                    isBackButtonVisible = false,
                    isForwardButtonVisible = false,
                    isCloseButtonVisible = true,
                    contentMode = GpmWebViewContentMode.MOBILE,
                    isMaskViewVisible = true,
#else
                    isNavigationBarVisible = false,
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
                new List<string>() { "USER_CUSTOM_SCHEME" }
            );
        }

        private static async void OnCallback(
            GpmWebViewCallback.CallbackType callbackType,
            string data,
            GpmWebViewError error
        )
        {
            Debug.Log($"WebView callback: {callbackType}, URL: {data}");

            switch (callbackType)
            {
                case GpmWebViewCallback.CallbackType.Close:
                    // Restore original orientation
                    Screen.orientation = originalOrientation;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Timestamp unix timestamp
                            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            await MixPanelManager.SendEvent(
                                Constants.MixpanelEvent.STORE_CLOSE,
                                unixTimestamp
                            );
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in background MixPanel event: {ex.Message}");
                        }
                    });
                    break;

                case GpmWebViewCallback.CallbackType.Open:
                    // Also move this to background thread for consistency
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            await MixPanelManager.SendEvent(
                                Constants.MixpanelEvent.STORE_OPEN,
                                unixTimestamp
                            );
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in background MixPanel event: {ex.Message}");
                        }
                    });

                    break;

                case GpmWebViewCallback.CallbackType.PageStarted:
                    GpmWebView.ExecuteJavaScript("localStorage.getItem('authToken');");

                    Debug.Log($"Page started: {data}");

                    // More reliable URL check
                    if (data.Contains("store.playsuper.club"))
                    {
                        string js =
                            $"localStorage.setItem('apiKey', '{PlaySuperUnitySDK.GetApiKey()}')";
                        GpmWebView.ExecuteJavaScript(js);
                        Debug.Log("Store URL detected - Injecting credentials");
                        InjectCredentials();
                    }
                    break;
                case GpmWebViewCallback.CallbackType.ExecuteJavascript:
                    Debug.Log("ExecuteJavascript: " + data);
                    if (string.IsNullOrEmpty(data) == false && data.Length > 2 && data != "null")
                    {
                        try
                        {
                            // Extract the token
                            int startIndex = 14;
                            int endIndex = data.IndexOf("\"", startIndex);
                            string token = data.Substring(startIndex, endIndex - startIndex - 1);

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
            }
        }

        internal static void InjectCredentials()
        {
            // Get credentials from SDK
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            string token = PlaySuperUnitySDK.GetAuthToken();

            // Log what we're injecting
            Debug.Log(
                $"Injecting - API Key: {apiKey}, Token present: {!string.IsNullOrEmpty(token)}"
            );

            // Set API key
            string jsApiKey =
                $"localStorage.setItem('apiKey', '{apiKey}'); console.log('API key set: {apiKey}');";
            GpmWebView.ExecuteJavaScript(jsApiKey);

            // Set auth token if available
            if (!string.IsNullOrEmpty(token))
            {
                // IMPORTANT: Format token properly for JavaScript
                string safeToken = token.Replace("'", "\\'").Replace("\n", "\\n");

                string jsToken =
                    $"localStorage.setItem('authToken', '{safeToken}'); console.log('Auth token set (first 5 chars): ' + localStorage.getItem('authToken').substring(0,5));";
                GpmWebView.ExecuteJavaScript(jsToken);

                // Verify injection
                string jsVerify =
                    "console.log('Auth token verification: ' + (localStorage.getItem('authToken') ? 'Present' : 'Missing'));";
                GpmWebView.ExecuteJavaScript(jsVerify);
            }
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
    }
}
