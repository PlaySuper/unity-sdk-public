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
                "https://playsuper.club/",
                new GpmWebViewRequest.Configuration()
                {
                    style = GpmWebViewStyle.FULLSCREEN,
                    orientation = GpmOrientation.UNSPECIFIED,
                    isClearCookie = false,
                    isClearCache = false,
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
                // See the end of the code example
                OnCallback,
                new List<string>()
                {
            "USER_CUSTOM_SCHEME"
                });
        }

        // Popup default
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
                // See the end of the code example
                OnCallback,
                new List<string>()
                {
            "http://"
                });
        }

        // Popup custom position and size
        public static void ShowUrlPopupPositionSize()
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
                    position = new GpmWebViewRequest.Position
                    {
                        hasValue = true,
                        x = (int)(Screen.width * 0.1f),
                        y = (int)(Screen.height * 0.1f)
                    },
                    size = new GpmWebViewRequest.Size
                    {
                        hasValue = true,
                        width = (int)(Screen.width * 0.8f),
                        height = (int)(Screen.height * 0.8f)
                    },
                    supportMultipleWindows = true,
#if UNITY_IOS
            contentMode = GpmWebViewContentMode.MOBILE,
            isMaskViewVisible = true,
#endif
                }, null, null);
        }

        // Popup custom margins
        public static void ShowUrlPopupMargins()
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
                    margins = new GpmWebViewRequest.Margins
                    {
                        hasValue = true,
                        left = (int)(Screen.width * 0.1f),
                        top = (int)(Screen.height * 0.1f),
                        right = (int)(Screen.width * 0.1f),
                        bottom = (int)(Screen.height * 0.1f)
                    },
                    supportMultipleWindows = true,
#if UNITY_IOS
            contentMode = GpmWebViewContentMode.MOBILE,
            isMaskViewVisible = true,
#endif
                }, null, null);
        }

        private static void OnCallback(
            GpmWebViewCallback.CallbackType callbackType,
            string data,
            GpmWebViewError error)
        {
            Debug.Log("OnCallback: " + callbackType);
            Debug.Log("data: " + data);
            if (data.Contains("sendTokenToSDK"))
            {
                GpmWebView.ExecuteJavaScript("localStorage.getItem('authToken');");
            }
            switch (callbackType)
            {
                case GpmWebViewCallback.CallbackType.ExecuteJavascript:
                    if (string.IsNullOrEmpty(data) == false)
                    {
                        Debug.LogFormat("ExecuteJavascript data : {0}, error : {1}", data, error);
                        PlaySuperUnitySDK.Instance.OnTokenReceive(data);
                    }
                    break;
            }
        }
    }
}
