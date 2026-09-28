using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace PlaySuperUnity
{
    /// <summary>
    /// Resolves the PlaySuper API and store base URLs at runtime.
    ///
    /// Resolution order: the remote config file (Constants.SDK_CONFIG_URL) → the last
    /// remote value that passed a health probe (persisted in PlayerPrefs) → the URL
    /// baked into this SDK build.
    ///
    /// The config is a static JSON file on Google Cloud Storage, deliberately outside
    /// playsuper.club so it still answers when that zone or our load balancer does
    /// not. It is fetched once per launch and again on resume after an hour, with
    /// If-None-Match so an unchanged file costs a 304. The persisted value means an
    /// override applies from the first request of the next cold start. One probe runs
    /// only when the resolved URL changes, and a URL that fails it is never adopted —
    /// so a bad config cannot move devices off a working host.
    ///
    /// File format (v1):
    /// {
    ///   "schemaVersion": 1,
    ///   "prod": { "apiUrl": "...", "storeUrl": "...", "apps": { "&lt;bundle id&gt;": { "apiUrl": "..." } } },
    ///   "dev":  { ... }
    /// }
    /// A field left out means the baked URL; an app entry overrides its environment.
    /// </summary>
    internal static class SdkEndpoints
    {
        private const int ProbeTimeoutSeconds = 10;
        private const int ConfigTimeoutSeconds = 5;
        private static readonly TimeSpan ConfigRefreshInterval = TimeSpan.FromHours(1);

        private static bool isDev;
        private static bool applying;

        // Bumped on every Initialize so a probe still in flight from before a
        // Dispose/Initialize cycle cannot write into the new environment.
        private static int generation;

        // A candidate that failed its probe is not retried for the rest of the session,
        // so a bad config costs one request per session rather than one per refresh.
        private static string rejectedApiUrl;
        private static string rejectedStoreUrl;

        private static bool fetchingConfig;
        private static DateTime lastConfigFetchUtc;
        // Kept for the session only: the first fetch of each launch always reads the
        // whole file, so a value a previous session rejected gets another probe.
        private static string configEtag;

        /// <summary>API base URL, no trailing slash (e.g. https://api.playsuper.club).</summary>
        internal static string ApiUrl { get; private set; } = Constants.prodApiUrl;

        /// <summary>Store base URL, always with a trailing slash (e.g. https://store.playsuper.club/).</summary>
        internal static string StoreUrl { get; private set; } = Constants.prodStoreUrl;

        internal static string DefaultApiUrl => isDev ? Constants.devApiUrl : Constants.prodApiUrl;
        internal static string DefaultStoreUrl => isDev ? Constants.devStoreUrl : Constants.prodStoreUrl;

        // Separate keys per environment so switching isDev never mixes hosts.
        private static string ApiPrefsKey => isDev ? "ps_api_url_dev" : "ps_api_url";
        private static string StorePrefsKey => isDev ? "ps_store_url_dev" : "ps_store_url";

        /// <summary>
        /// Load the endpoints for this environment synchronously, so every request made
        /// during init already goes to the right host.
        /// </summary>
        internal static void Initialize(bool dev)
        {
            isDev = dev;
            generation++;
            applying = false;
            rejectedApiUrl = null;
            rejectedStoreUrl = null;
            fetchingConfig = false;
            lastConfigFetchUtc = DateTime.MinValue;
            configEtag = null;

            ApiUrl = NormalizeApiUrl(PlayerPrefs.GetString(ApiPrefsKey, "")) ?? DefaultApiUrl;
            StoreUrl = NormalizeStoreUrl(PlayerPrefs.GetString(StorePrefsKey, "")) ?? DefaultStoreUrl;

            if (ApiUrl != DefaultApiUrl || StoreUrl != DefaultStoreUrl)
            {
                Debug.Log($"[PlaySuper][Endpoints] Using remote endpoints from last session: api={ApiUrl}, store={StoreUrl}");
            }
        }

        /// <summary>
        /// Fetch the remote config and apply it. Fire-and-forget from the main thread.
        /// Unless forced, does nothing within an hour of the last successful fetch.
        ///
        /// A 404 is an answer — there is no config, so the baked URLs apply. Any other
        /// failure (offline, 5xx, a private bucket, a malformed file) is not, and the
        /// current endpoints stay as they are.
        /// </summary>
        internal static async Task RefreshFromConfig(bool force = false)
        {
            if (fetchingConfig) return;
            if (!force && DateTime.UtcNow - lastConfigFetchUtc < ConfigRefreshInterval) return;
            fetchingConfig = true;
            int gen = generation;

            try
            {
                string apiUrl = null;
                string storeUrl = null;

                using (var request = UnityWebRequest.Get(Constants.SDK_CONFIG_URL))
                {
                    request.timeout = ConfigTimeoutSeconds;
                    if (!string.IsNullOrEmpty(configEtag))
                        request.SetRequestHeader("If-None-Match", configEtag);

                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();
                    if (gen != generation) return;

                    if (request.responseCode == 304)
                    {
                        lastConfigFetchUtc = DateTime.UtcNow;
                        return;
                    }

                    if (request.responseCode == 404)
                    {
                        lastConfigFetchUtc = DateTime.UtcNow;
                        // apiUrl / storeUrl stay null: the baked URLs.
                    }
                    else if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[PlaySuper][Endpoints] Remote config unavailable ({request.responseCode} {request.error}) - keeping {ApiUrl}");
                        return;
                    }
                    else if (!TryResolveConfig(request.downloadHandler.text, isDev, Application.identifier, out apiUrl, out storeUrl))
                    {
                        Debug.LogWarning("[PlaySuper][Endpoints] Ignoring remote config - not a valid v1 file");
                        return;
                    }
                    else
                    {
                        configEtag = request.GetResponseHeader("ETag");
                        lastConfigFetchUtc = DateTime.UtcNow;
                    }
                }

                await ApplyRemote(apiUrl, storeUrl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper][Endpoints] Failed to refresh remote config: {ex.Message}");
            }
            finally
            {
                if (gen == generation) fetchingConfig = false;
            }
        }

        /// <summary>
        /// Pick this app's URLs out of a v1 config file. An entry under
        /// <c>apps[bundleId]</c> overrides its environment; a field left out resolves to
        /// null (the baked URL). Returns false only when the file itself is unusable.
        /// </summary>
        internal static bool TryResolveConfig(string json, bool dev, string bundleId, out string apiUrl, out string storeUrl)
        {
            apiUrl = null;
            storeUrl = null;

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception)
            {
                return false;
            }

            var version = root["schemaVersion"];
            if (version == null || version.Type != JTokenType.Integer || version.Value<int>() != 1)
                return false;

            var env = root[dev ? "dev" : "prod"] as JObject;
            if (env == null) return true;

            var app = string.IsNullOrEmpty(bundleId) ? null : (env["apps"] as JObject)?[bundleId] as JObject;
            apiUrl = StringField(app, "apiUrl") ?? StringField(env, "apiUrl");
            storeUrl = StringField(app, "storeUrl") ?? StringField(env, "storeUrl");
            return true;
        }

        private static string StringField(JObject obj, string key)
        {
            var token = obj?[key];
            return token != null && token.Type == JTokenType.String ? token.Value<string>() : null;
        }

        /// <summary>
        /// Apply resolved URLs. Null means the baked default; a malformed, non-https or
        /// off-domain value is treated the same way.
        /// </summary>
        internal static async Task ApplyRemote(string remoteApiUrl, string remoteStoreUrl)
        {
            if (applying) return;
            applying = true;
            int gen = generation;

            try
            {
                string api = Resolve(remoteApiUrl, NormalizeApiUrl, DefaultApiUrl, "api");
                if (api != ApiUrl && api != rejectedApiUrl)
                {
                    bool healthy = await ProbeApi(api);
                    if (gen != generation) return;
                    if (healthy)
                    {
                        Debug.Log($"[PlaySuper][Endpoints] API endpoint switched: {ApiUrl} -> {api}");
                        ApiUrl = api;
                        Persist(ApiPrefsKey, api, DefaultApiUrl);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper][Endpoints] API endpoint {api} failed health probe - staying on {ApiUrl}");
                        rejectedApiUrl = api;
                    }
                }

                string store = Resolve(remoteStoreUrl, NormalizeStoreUrl, DefaultStoreUrl, "store");
                if (store != StoreUrl && store != rejectedStoreUrl)
                {
                    bool healthy = await ProbeStore(store);
                    if (gen != generation) return;
                    if (healthy)
                    {
                        Debug.Log($"[PlaySuper][Endpoints] Store endpoint switched: {StoreUrl} -> {store}");
                        StoreUrl = store;
                        Persist(StorePrefsKey, store, DefaultStoreUrl);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlaySuper][Endpoints] Store endpoint {store} failed health probe - staying on {StoreUrl}");
                        rejectedStoreUrl = store;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper][Endpoints] Failed to apply remote endpoints: {ex.Message}");
            }
            finally
            {
                if (gen == generation) applying = false;
            }
        }

        /// <summary>
        /// True if the URL is on the current store host or either baked store host.
        /// Used to decide whether a WebView page should receive credentials.
        /// </summary>
        internal static bool IsStoreUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return ContainsHost(url, StoreUrl)
                || ContainsHost(url, Constants.prodStoreUrl)
                || ContainsHost(url, Constants.devStoreUrl);
        }

        private static bool ContainsHost(string url, string baseUrl)
        {
            return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                && url.IndexOf(uri.Host, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Resolve(string remote, Func<string, string> normalize, string fallback, string label)
        {
            if (string.IsNullOrEmpty(remote)) return fallback;

            string normalized = normalize(remote);
            if (normalized == null)
            {
                Debug.LogWarning($"[PlaySuper][Endpoints] Ignoring remote {label} URL '{remote}' - must be an absolute https URL on playsuper.club or playsuper.ai");
                return fallback;
            }
            return normalized;
        }

        private static string NormalizeApiUrl(string url)
        {
            return IsAllowedUrl(url) ? url.Trim().TrimEnd('/') : null;
        }

        private static string NormalizeStoreUrl(string url)
        {
            return IsAllowedUrl(url) ? url.Trim().TrimEnd('/') + "/" : null;
        }

        // These hosts receive the game's API key, player tokens and store credentials,
        // so a remote value may only move traffic between PlaySuper's own domains —
        // whatever is written into the config file, or into a bucket squatting its name.
        private static bool IsAllowedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && string.IsNullOrEmpty(uri.Query)
                && (IsHostUnder(uri.Host, "playsuper.club") || IsHostUnder(uri.Host, "playsuper.ai"));
        }

        private static bool IsHostUnder(string host, string domain)
        {
            return string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }

        // The default is never persisted: that way a later SDK build with a new baked
        // URL takes effect instead of being pinned to this build's value.
        private static void Persist(string key, string value, string defaultValue)
        {
            if (value == defaultValue)
                PlayerPrefs.DeleteKey(key);
            else
                PlayerPrefs.SetString(key, value);
            PlayerPrefsSaveManager.ScheduleSave();
        }

        // Same request GameManager.GetGameData makes, so passing it means the host is a
        // PlaySuper API that accepts this game's key.
        private static async Task<bool> ProbeApi(string apiUrl)
        {
            using (var request = UnityWebRequest.Get($"{apiUrl}/player/game"))
            {
                request.timeout = ProbeTimeoutSeconds;
                request.SetRequestHeader("x-api-key", PlaySuperUnitySDK.GetApiKey() ?? "");
                PlaySuperUnitySDK.ApplyGameUuidHeader(request);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                return request.result == UnityWebRequest.Result.Success;
            }
        }

        private static async Task<bool> ProbeStore(string storeUrl)
        {
            using (var request = UnityWebRequest.Head(storeUrl))
            {
                request.timeout = ProbeTimeoutSeconds;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                return request.result == UnityWebRequest.Result.Success;
            }
        }
    }
}
