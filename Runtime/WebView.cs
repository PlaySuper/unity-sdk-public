using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Gpm.WebView;

namespace PlaySuperUnity
{
    internal class WebView
    {
        public static void ShowUrlFullScreen()
        {
            GpmWebView.ShowUrl(
                "https://store.playsuper.club/",
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.FULLSCREEN,
                    orientation = GpmOrientation.UNSPECIFIED,
                    isClearCookie = true,
                    isClearCache = true,
                    backgroundColor = "#FFFFFF",
                    isNavigationBarVisible = true,
                    navigationBarColor = "#4B96E6",
                    title = "Offer Zone",
                    isBackButtonVisible = true,
                    isForwardButtonVisible = true,
                    isCloseButtonVisible = true,
                    supportMultipleWindows = true,
#if UNITY_IOS
            contentMode = GpmWebViewContentMode.MOBILE
#endif
                },
                OnCallback,
                new List<string>()
                {
            "USER_CUSTOM_SCHEME"
                });
        }

        public static void ShowUrlPopupDefault()
        {
            GpmWebView.ShowUrl(
                "https://playsuper.club/",
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.UNSPECIFIED,
                    isClearCookie = true,
                    isClearCache = true,
                    isNavigationBarVisible = true,
                    isCloseButtonVisible = true,
                    supportMultipleWindows = true,
#if UNITY_IOS
            contentMode = GpmWebViewContentMode.MOBILE,
            isMaskViewVisible = true,
#endif
                },
                OnCallback,
                new List<string>()
                {
            "http://"
                });
        }

        public static void ShowUrlPopupPositionSize(bool isDev = false)
        {
            Rect safeArea = Screen.safeArea;
            GpmWebView.ShowUrl(
                isDev ? Constants.devStoreUrl : Constants.prodStoreUrl,
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = getWebOrientation(),
                    isClearCookie = true,
                    isClearCache = true,
                    isNavigationBarVisible = true,
                    isCloseButtonVisible = true,
                    position = new GpmWebViewRequest.Position
                    {
                        hasValue = true,
                        x = (int)safeArea.xMin,
                        y = (int)(Screen.height - safeArea.height)
                    },
                    size = new GpmWebViewRequest.Size
                    {
                        hasValue = true,
                        width = (int)safeArea.width,
                        height = (int)safeArea.height
                    },
                    supportMultipleWindows = true,
#if UNITY_IOS
            contentMode = GpmWebViewContentMode.MOBILE,
            isMaskViewVisible = true,
#endif
                }, OnCallback, null);
        }

        private static async void OnCallback(
            GpmWebViewCallback.CallbackType callbackType,
            string data,
            GpmWebViewError error)
        {
            Debug.Log($"WebView callback: {callbackType}, URL: {data}");

            switch (callbackType)
            {
                case GpmWebViewCallback.CallbackType.Close:
                    await MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_CLOSE);
                    break;

                case GpmWebViewCallback.CallbackType.Open:
                    await MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_OPEN);
                    break;

                case GpmWebViewCallback.CallbackType.PageStarted:
                    GpmWebView.ExecuteJavaScript("localStorage.getItem('authToken');");

                    Debug.Log($"Page started: {data}");

                    // More reliable URL check
                    if (data.Contains("store.playsuper.club"))
                    {
                        string js = $"localStorage.setItem('apiKey', '{PlaySuperUnitySDK.GetApiKey()}')";
                        GpmWebView.ExecuteJavaScript(js);
                        Debug.Log("Store URL detected - Injecting credentials");
                        InjectCredentials();
                    }
                    break;
                case GpmWebViewCallback.CallbackType.ExecuteJavascript:
                    Debug.Log("ExecuteJavascript: " + data);
                    if (string.IsNullOrEmpty(data) == false && data.Length > 2 && data != "null")
                    {
                        // Extract the token
                        int startIndex = 14;
                        int endIndex = data.IndexOf("\"", startIndex);
                        string token = data.Substring(startIndex, endIndex - startIndex - 1);

                        Debug.Log("Token: " + token);
                        if (!PlaySuperUnitySDK.IsLoggedIn()) PlaySuperUnitySDK.Instance.OnTokenReceive(token);
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
            Debug.Log($"Injecting - API Key: {apiKey}, Token present: {!string.IsNullOrEmpty(token)}");

            // Set API key
            string jsApiKey = $"localStorage.setItem('apiKey', '{apiKey}'); console.log('API key set: {apiKey}');";
            GpmWebView.ExecuteJavaScript(jsApiKey);

            // Set auth token if available
            if (!string.IsNullOrEmpty(token))
            {
                // IMPORTANT: Format token properly for JavaScript
                string safeToken = token.Replace("'", "\\'").Replace("\n", "\\n");

                string jsToken = $"localStorage.setItem('authToken', '{safeToken}'); console.log('Auth token set (first 5 chars): ' + localStorage.getItem('authToken').substring(0,5));";
                GpmWebView.ExecuteJavaScript(jsToken);

                // Verify injection
                string jsVerify = "console.log('Auth token verification: ' + (localStorage.getItem('authToken') ? 'Present' : 'Missing'));";
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
