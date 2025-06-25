using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity
{
    internal static class NetworkUtils
    {
        private static DateTime lastConnectivityCheck = DateTime.MinValue;
        private static bool lastConnectivityResult = false;
        private static readonly TimeSpan CONNECTIVITY_CACHE_DURATION = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Quick network availability check with caching to avoid excessive calls
        /// </summary>
        public static bool IsNetworkAvailable()
        {
            // Use cached result if recent
            if (DateTime.UtcNow - lastConnectivityCheck < CONNECTIVITY_CACHE_DURATION)
            {
                return lastConnectivityResult;
            }

            try
            {
                // Unity's built-in network reachability check
                bool isReachable =
                    Application.internetReachability != NetworkReachability.NotReachable;

                // Cache the result
                lastConnectivityCheck = DateTime.UtcNow;
                lastConnectivityResult = isReachable;

                return isReachable;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error checking network availability: {ex.Message}");
                return false; // Assume offline on error
            }
        }

        /// <summary>
        /// More thorough connectivity test - use sparingly
        /// </summary>
        public static async Task<bool> CanReachInternet(int timeoutMs = 5000)
        {
            if (!IsNetworkAvailable())
                return false;

            try
            {
                // Use Google's DNS server as a reliable endpoint
                using (var request = UnityEngine.Networking.UnityWebRequest.Get("https://8.8.8.8"))
                {
                    request.timeout = timeoutMs / 1000; // Convert to seconds
                    var operation = request.SendWebRequest();

                    float startTime = Time.realtimeSinceStartup;
                    while (
                        !operation.isDone
                        && (Time.realtimeSinceStartup - startTime) < (timeoutMs / 1000f)
                    )
                    {
                        await Task.Yield();
                    }

                    return request.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Internet connectivity test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get current network type for analytics
        /// </summary>
        public static string GetNetworkType()
        {
            try
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        return "cellular";
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        return "wifi";
                    case NetworkReachability.NotReachable:
                        return "none";
                    default:
                        return "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Check if we're on a metered connection (cellular)
        /// </summary>
        public static bool IsMeteredConnection()
        {
            return Application.internetReachability
                == NetworkReachability.ReachableViaCarrierDataNetwork;
        }

        public static string GetIPAddress()
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList.First(f =>
                    f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                )
                .ToString();
        }

        public static async Task<string> GetPublicIPAddress()
        {
            try
            {
                using (
                    var request = UnityEngine.Networking.UnityWebRequest.Get(
                        "https://api.ipify.org"
                    )
                )
                {
                    request.timeout = 10;
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        string ip = request.downloadHandler.text.Trim();
                        Debug.Log($"[NetworkUtils] Public IP: {ip}");
                        return ip;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[NetworkUtils] Failed to get public IP: {request.error}"
                        );
                        return GetIPAddress(); // Fallback to local IP
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkUtils] Error getting public IP: {ex.Message}");
                return GetIPAddress(); // Fallback to local IP
            }
        }
    }
}
