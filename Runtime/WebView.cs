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
                    title = "PlaySuper Store",
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

        public static void ShowUrlPopupPositionSize()
        {
            Rect safeArea = Screen.safeArea;
            GpmWebView.ShowUrl(
                "https://store.playsuper.club/",
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.POPUP,
                    orientation = GpmOrientation.UNSPECIFIED,
                    isClearCookie = true,
                    isClearCache = true,
                    isNavigationBarVisible = true,
                    isCloseButtonVisible = true,
                    position = new GpmWebViewRequest.Position
                    {
                        hasValue = true,
                        x = (int)safeArea.xMin,
                        y = (int)safeArea.yMin
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
            switch (callbackType)
            {
                case GpmWebViewCallback.CallbackType.Close:
                    await MixPanelManager.SendEvent(Constants.MixpanelEvent.STORE_CLOSE);
                    break;
                case GpmWebViewCallback.CallbackType.PageStarted:
                    GpmWebView.ExecuteJavaScript("localStorage.getItem('authToken');");
                    if (data == "https://store.playsuper.club/")
                    {
                        string js = $"localStorage.setItem('apiKey', '{PlaySuperUnitySDK.GetApiKey()}')";
                        GpmWebView.ExecuteJavaScript(js);
                    }
                    break;
                case GpmWebViewCallback.CallbackType.ExecuteJavascript:
                    if (string.IsNullOrEmpty(data) == false && data.Length > 2 && data != "null")
                    {
                        // Extract the token
                        int startIndex = 14;
                        int endIndex = data.IndexOf("\"", startIndex);
                        string token = data.Substring(startIndex, endIndex - startIndex - 1);

                        PlaySuperUnitySDK.Instance.OnTokenReceive(token);
                    }
                    break;
            }
        }
    }
}
