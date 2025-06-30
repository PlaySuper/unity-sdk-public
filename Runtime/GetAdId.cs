using UnityEngine;
using System;
using System.Collections;
using PlaySuperUnity;

public class GetAdsId : MonoBehaviour
{
    string adsid;
    string adsidSource;

    private void Start()
    {
        StartCoroutine(GetAdIdDelayed());
    }

    private System.Collections.IEnumerator GetAdIdDelayed()
    {
        yield return new WaitForEndOfFrame();

        // Get advertising ID with platform-specific methods
        yield return StartCoroutine(GetAdvertisingIdCoroutine());

        if (!string.IsNullOrEmpty(adsid))
        {
            Debug.Log($"Advertising ID obtained from {adsidSource}: {adsid}");
        }
        else
        {
            Debug.Log($"No advertising ID available - source: {adsidSource}");
        }
    }

    private IEnumerator GetAdvertisingIdCoroutine()
    {
        adsid = "";
        adsidSource = "";

#if UNITY_IOS && !UNITY_EDITOR
        yield return StartCoroutine(GetIOSAdvertisingId());
#elif UNITY_ANDROID && !UNITY_EDITOR
        yield return StartCoroutine(GetAndroidAdvertisingId());
#elif UNITY_EDITOR
        GetEditorMockId();
#else
        Debug.LogWarning("Platform not supported for advertising ID");
        adsidSource = "unsupported";
#endif

        yield return null;
    }

    // Fixed iOS implementation
    private IEnumerator GetIOSAdvertisingId()
    {
        Debug.Log("Getting iOS advertising identifier...");

        // First check if collection is enabled
        if (!PlaySuperUnitySDK.IsAdvertisingIdEnabled())
        {
            Debug.Log("Advertising ID collection disabled - skipping");
            adsid = "";
            adsidSource = "ios_disabled";
            CacheAdvertisingId(adsid, adsidSource);
            yield break;
        }

        // Check ATT permission
        if (!PlaySuperUnitySDK.ShouldAllowAdvertisingIdCollection())
        {
            Debug.Log("ATT permission not granted - skipping IDFA collection");
            adsid = "";
            adsidSource = "ios_no_att_permission";
            CacheAdvertisingId(adsid, adsidSource);
            yield break;
        }

        // Get advertising ID with conditional compilation
        try
        {
#if ENABLE_ADVERTISING_ID
            adsid = GetAdvertisingIdInternal();
#else
            adsid = "";
            adsidSource = "ios_compile_time_disabled";
            CacheAdvertisingId(adsid, adsidSource);
            yield break;
#endif

            if (string.IsNullOrEmpty(adsid) || adsid == "00000000-0000-0000-0000-000000000000")
            {
                Debug.LogWarning("Got invalid IDFA despite permission being granted");
                adsid = "";
                adsidSource = "ios_invalid_id";
            }
            else
            {
                Debug.Log($"iOS IDFA received: {adsid}");
                adsidSource = "ios_att_authorized";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting iOS advertising ID: {ex.Message}");
            adsid = "";
            adsidSource = "ios_error";
        }

        CacheAdvertisingId(adsid, adsidSource);
        yield return null;
    }

    // Fixed Android implementation
    private IEnumerator GetAndroidAdvertisingId()
    {
        Debug.Log("Getting Android advertising identifier...");

        try
        {
            adsid = GetAndroidAdvertisingIdSync();

            if (!string.IsNullOrEmpty(adsid))
            {
                Debug.Log($"Android advertising ID received: {adsid}");
                adsidSource = "android";
            }
            else
            {
                Debug.LogWarning("Android advertising ID is empty");
                adsid = "";
                adsidSource = "android_error";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting Android advertising ID: {ex.Message}");
            adsid = "";
            adsidSource = "android_error";
        }

        CacheAdvertisingId(adsid, adsidSource);
        yield return null;
    }

    // Editor mock implementation
    private void GetEditorMockId()
    {
        Debug.LogWarning("Running in Unity Editor - generating mock advertising ID");
        adsid = "mock-ad-id-for-testing-" + SystemInfo.deviceUniqueIdentifier;
        adsidSource = "editor";

        CacheAdvertisingId(adsid, adsidSource);
        Debug.Log($"Mock advertising ID: {adsid}");
    }

    // Enhanced cache with platform
    private void CacheAdvertisingId(string adId, string source)
    {
        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string platform = GetCurrentPlatform();

        PlayerPrefs.SetString("advertising_id", adId);
        PlayerPrefs.SetString("advertising_id_source", source);
        PlayerPrefs.SetString("advertising_id_platform", platform);
        PlayerPrefs.SetString("advertising_id_timestamp", currentTimestamp.ToString());
        PlayerPrefs.Save();

        Debug.Log($"Cached advertising ID from {source} on {platform} at {currentTimestamp}");
    }

    // Check if cache is still valid (less than 5 minutes old)
    private static bool IsCacheValid()
    {
        if (!PlayerPrefs.HasKey("advertising_id_timestamp"))
        {
            return false;
        }

        string timestampStr = PlayerPrefs.GetString("advertising_id_timestamp");
        if (!long.TryParse(timestampStr, out long cachedTimestamp))
        {
            return false;
        }

        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long secondsDiff = currentTimestamp - cachedTimestamp;

        // Cache is valid for 5 minutes
        bool isValid = secondsDiff < 300;

        if (!isValid)
        {
            Debug.Log($"Advertising ID cache expired ({secondsDiff} seconds old), will refresh");
        }

        return isValid;
    }

    // Static version
    private static void CacheAdvertisingIdStatic(string adId, string source)
    {
        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string platform = GetCurrentPlatform();

        PlayerPrefs.SetString("advertising_id", adId);
        PlayerPrefs.SetString("advertising_id_source", source);
        PlayerPrefs.SetString("advertising_id_platform", platform);
        PlayerPrefs.SetString("advertising_id_timestamp", currentTimestamp.ToString());
        PlayerPrefs.Save();
    }

    // Helper method to get current platform
    private static string GetCurrentPlatform()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return "ios";
#elif UNITY_ANDROID && !UNITY_EDITOR
        return "android";
#elif UNITY_EDITOR
        return "editor";
#else
        return "unknown";
#endif
    }

    // Updated static method
    public static AdvertisingIdResult GetAdvertisingId()
    {
        // Check if advertising ID collection is enabled
        if (!PlaySuperUnitySDK.IsAdvertisingIdEnabledStatic())
        {
            return new AdvertisingIdResult("", "disabled", GetCurrentPlatform());
        }

        // Check cache first
        if (IsCacheValid() &&
            PlayerPrefs.HasKey("advertising_id") &&
            PlayerPrefs.HasKey("advertising_id_source") &&
            PlayerPrefs.HasKey("advertising_id_platform"))
        {
            string cachedId = PlayerPrefs.GetString("advertising_id");
            string cachedSource = PlayerPrefs.GetString("advertising_id_source");
            string cachedPlatform = PlayerPrefs.GetString("advertising_id_platform");

            Debug.Log($"Using valid cached advertising ID from {cachedSource} on {cachedPlatform}");
            return new AdvertisingIdResult(cachedId, cachedSource, cachedPlatform);
        }

        // Get fresh data
        Debug.Log("Getting fresh advertising ID (cache expired or missing)");
        string platform = GetCurrentPlatform();

#if UNITY_IOS && !UNITY_EDITOR
        // Check if tracking permission was granted
        if (!PlaySuperUnitySDK.HasTrackingPermissionStatic())
        {
            string source = "ios_no_permission";
            CacheAdvertisingIdStatic("", source);
            return new AdvertisingIdResult("", source, platform);
        }
        
#if ENABLE_ADVERTISING_ID
        string id = UnityEngine.iOS.Device.advertisingIdentifier;
        string resultSource;
        
        if (string.IsNullOrEmpty(id) || id == "00000000-0000-0000-0000-000000000000")
        {
            id = "";
            resultSource = "ios_invalid_id";
        }
        else
        {
            resultSource = "ios_permission_granted";
        }
#else
        string id = "";
        string resultSource = "ios_compile_time_disabled";
#endif
        
        CacheAdvertisingIdStatic(id, resultSource);
        return new AdvertisingIdResult(id, resultSource, platform);
        
#elif UNITY_ANDROID && !UNITY_EDITOR
        string id = GetAndroidAdvertisingIdSync();
        string source = string.IsNullOrEmpty(id) ? "android_error" : "android";
        
        CacheAdvertisingIdStatic(id, source);
        return new AdvertisingIdResult(id, source, platform);
        
#elif UNITY_EDITOR
        string id = "mock-ad-id-for-testing-" + SystemInfo.deviceUniqueIdentifier;
        string source = "editor";
        
        CacheAdvertisingIdStatic(id, source);
        return new AdvertisingIdResult(id, source, platform);
        
#else
        return new AdvertisingIdResult("", "unsupported", platform);
#endif
    }

    // Android implementation (unchanged)
    private static string GetAndroidAdvertisingIdSync()
    {
        string advertisingID = "";

        Debug.Log("Starting Android advertising ID request...");

        try
        {
            if (Application.isPlaying)
            {
                AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");

                if (currentActivity == null)
                {
                    Debug.LogError("Current activity is null");
                    return "";
                }

                AndroidJavaClass client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                AndroidJavaObject adInfo = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", currentActivity);

                if (adInfo == null)
                {
                    Debug.LogError("Advertising info is null");
                    return "";
                }

                advertisingID = adInfo.Call<string>("getId");

                if (string.IsNullOrEmpty(advertisingID))
                {
                    Debug.LogWarning("Advertising ID is empty");
                }
                else
                {
                    Debug.Log($"Got Android advertising ID: {advertisingID}");
                }
            }
            else
            {
                Debug.LogError("App not playing");
            }
        }
        catch (AndroidJavaException androidEx)
        {
            Debug.LogError($"Android Java Exception: {androidEx.Message}");

            if (androidEx.Message.Contains("AdvertisingIdClient"))
            {
                Debug.LogError("Missing Google Play Services Ads Identifier dependency. Add to mainTemplate.gradle:\nimplementation 'com.google.android.gms:play-services-ads-identifier:18.2.0'");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"General Exception: {ex.Message}");
        }

        return advertisingID;
    }

    public static void GetAdvertisingIdAsync(System.Action<AdvertisingIdResult> callback)
    {
        GameObject tempObj = new GameObject("TempAdIdGetter");
        GetAdsId component = tempObj.AddComponent<GetAdsId>();
        component.StartCoroutine(component.GetAdvertisingIdAsyncCoroutine(callback, tempObj));
    }

    private IEnumerator GetAdvertisingIdAsyncCoroutine(System.Action<AdvertisingIdResult> callback, GameObject tempObj)
    {
        yield return StartCoroutine(GetAdvertisingIdCoroutine());
        string platform = GetCurrentPlatform();
        callback?.Invoke(new AdvertisingIdResult(adsid, adsidSource, platform));
        Destroy(tempObj);
    }

#if ENABLE_ADVERTISING_ID
    private static string GetAdvertisingIdInternal()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            return UnityEngine.iOS.Device.advertisingIdentifier;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting iOS advertising ID: {ex.Message}");
            return "";
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        return GetAndroidAdvertisingIdSync();
#elif UNITY_EDITOR
        return "mock-ad-id-for-testing-" + SystemInfo.deviceUniqueIdentifier;
#else
        return "";
#endif
    }
#else
    private static string GetAdvertisingIdInternal()
    {
        Debug.Log("[PlaySuper] Advertising ID collection disabled at compile time");
        return "";
    }
#endif
}

// Data structure to hold advertising ID and its source
[System.Serializable]
public class AdvertisingIdResult
{
    public string id;
    public string source;
    public string platform;

    public AdvertisingIdResult(string advertisingId, string advertisingSource, string advertisingPlatform)
    {
        id = advertisingId ?? "";
        source = advertisingSource ?? "";
        platform = advertisingPlatform ?? "";
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(id);
    }

    public bool IsIOS()
    {
        return platform == "ios";
    }

    public bool IsAndroid()
    {
        return platform == "android";
    }

    public bool IsATTAuthorized()
    {
        return source == "ios_authorized";
    }

    public bool IsATTDenied()
    {
        return source == "ios_denied" || source == "ios_restricted";
    }

    public bool IsATTNotDetermined()
    {
        return source == "ios_not_determined";
    }

    public string GetStatusDescription()
    {
        switch (source)
        {
            case "ios_permission_granted": return "iOS IDFA available with tracking permission";
            case "ios_no_permission": return "iOS tracking permission denied by user/game";
            case "ios_invalid_id": return "iOS permission granted but IDFA invalid/zero";
            case "ios_error": return "iOS advertising ID fetch failed with exception";
            case "android": return "Android advertising ID successfully obtained";
            case "android_error": return "Android advertising ID fetch failed";
            case "editor": return "Unity Editor mock advertising ID";
            case "disabled": return "Advertising ID collection disabled in SDK settings";
            case "unsupported": return "Platform does not support advertising ID";
            default: return "Unknown status";
        }
    }
}
