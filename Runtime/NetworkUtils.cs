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

        // Cached local IP to avoid repeated DNS lookups
        private static string cachedLocalIP = null;
        private static DateTime lastIPCacheTime = DateTime.MinValue;
        private static readonly TimeSpan IP_CACHE_DURATION = TimeSpan.FromMinutes(5);

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

        /// <summary>
        /// Get local IP address - returns cached value to avoid blocking.
        /// Use GetIPAddressAsync() for fresh results.
        /// </summary>
        public static string GetIPAddress()
        {
            // Return cached value if available and not expired
            if (!string.IsNullOrEmpty(cachedLocalIP) &&
                DateTime.UtcNow - lastIPCacheTime < IP_CACHE_DURATION)
            {
                return cachedLocalIP;
            }

            // Return cached value even if expired (to avoid blocking)
            // Trigger background refresh
            if (!string.IsNullOrEmpty(cachedLocalIP))
            {
                _ = RefreshLocalIPAsync();
                return cachedLocalIP;
            }

            // No cache at all - return default, trigger background fetch
            _ = RefreshLocalIPAsync();
            return "0.0.0.0";
        }

        /// <summary>
        /// Async version that runs DNS lookup on a background thread.
        /// Safe to await from main thread - will not cause ANR.
        /// </summary>
        public static async Task<string> GetIPAddressAsync()
        {
            // Return cached value if valid
            if (!string.IsNullOrEmpty(cachedLocalIP) &&
                DateTime.UtcNow - lastIPCacheTime < IP_CACHE_DURATION)
            {
                return cachedLocalIP;
            }

            try
            {
                // Run DNS lookup on background thread to avoid ANR
                string ip = await Task.Run(() =>
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    var addr = host.AddressList.FirstOrDefault(f =>
                        f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    );
                    return addr?.ToString() ?? "0.0.0.0";
                });

                // Cache the result
                cachedLocalIP = ip;
                lastIPCacheTime = DateTime.UtcNow;

                return ip;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkUtils] GetIPAddressAsync failed: {ex.Message}");
                return cachedLocalIP ?? "0.0.0.0";
            }
        }

        /// <summary>
        /// Refreshes the cached local IP address in the background.
        /// </summary>
        private static async Task RefreshLocalIPAsync()
        {
            try
            {
                string ip = await Task.Run(() =>
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    var addr = host.AddressList.FirstOrDefault(f =>
                        f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    );
                    return addr?.ToString() ?? "0.0.0.0";
                });

                cachedLocalIP = ip;
                lastIPCacheTime = DateTime.UtcNow;
                Debug.Log($"[NetworkUtils] Local IP refreshed: {ip}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkUtils] Background IP refresh failed: {ex.Message}");
            }
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
                        // Use async fallback instead of blocking sync call
                        return await GetIPAddressAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkUtils] Error getting public IP: {ex.Message}");
                // Use async fallback instead of blocking sync call
                return await GetIPAddressAsync();
            }
        }
    }
}
