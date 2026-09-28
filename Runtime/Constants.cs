namespace PlaySuperUnity
{
    internal static class Constants
    {
        internal static class AnalyticsEvent
        {
            public const string GAME_OPEN = "ps_sdk.game_opened";
            public const string GAME_CLOSE = "ps_sdk.game_closed";
            public const string TOUCHPOINT_CLICKED = "ps_sdk.touchpoint_clicked";
            public const string STORE_OPEN = "ps_sdk.store_opened";
            public const string STORE_CLOSE = "ps_sdk.store_closed";
            public const string PLAYER_IDENTIFY = "ps_sdk.player_identified";

            // Transaction events
            public const string TRANSACTIONS_FETCHED = "ps_sdk.transactions_fetched";
            public const string TRANSACTIONS_COMMITTED = "ps_sdk.transactions_committed";
            public const string TRANSACTIONS_COMMIT_FAILED = "ps_sdk.transactions_commit_failed";

            // ANR / crash self-reporting — emitted on the cold start that follows a bad exit
            // (ActivityManager.getHistoricalProcessExitReasons, API 30+ Android only).
            public const string PREVIOUS_EXIT_OBSERVED = "ps_sdk.previous_exit";
        }

        // SDK version — keep in sync with package.json. Attached to telemetry
        // (e.g. ps_sdk.previous_exit) so events can be sliced by SDK version.
        internal const string SDK_VERSION = "4.6.0";

        // Analytics API endpoints (same as playsuper-store and playsuper-api)
        internal const string PS_ANALYTICS_URL = "https://analytics.playsuper.club";
        internal const string PS_ANALYTICS_EVENT_URL = PS_ANALYTICS_URL + "/events/track";
        internal const string PS_ANALYTICS_BATCH_URL = PS_ANALYTICS_URL + "/events/track/batch";

        // Legacy Lambda URLs (deprecated, kept for fallback)
        internal const string MIXPANEL_URL =
            "https://7ecybbalvlg4pem67c4amx464i0fhpbx.lambda-url.ap-south-1.on.aws/sdk-event";
        internal const string MIXPANEL_URL_BATCH =
            "https://7ecybbalvlg4pem67c4amx464i0fhpbx.lambda-url.ap-south-1.on.aws/sdk-batch";

        internal const string deviceIdName = "device_id";
        internal const string userPropertiesKey = "ps_user_properties";

        internal const string lastCloseTimestampName = "lastCloseTimestamp";
        internal const string lastCloseDoneName = "lastCloseDone";

        internal const string devStoreUrl = "https://dev-store.playsuper.club/";
        internal const string prodStoreUrl = "https://store.playsuper.club/";

        // Baked fallbacks. The live values come from SdkEndpoints, which can
        // override them from SDK_CONFIG_URL below.
        internal const string devApiUrl = "https://dev.playsuper.club";
        internal const string prodApiUrl = "https://api.playsuper.club";

        // Remote endpoint config (see SdkEndpoints for the format). On Google Cloud
        // Storage rather than playsuper.club so it answers when our own domain does
        // not. Versioned in the path: a v2 format gets a new file, and SDKs that only
        // know v1 keep reading this one.
        internal const string SDK_CONFIG_URL = "https://storage.googleapis.com/playsuper-sdk-config/v1/sdk-config.json";

        // The game's own player uuid (the one passed to CreatePlayerWithUuid /
        // LoginFederatedByStudio). Sent on PlaySuper requests except the routes where
        // the API lets it override the Bearer token — see PlaySuperUnitySDK.GameUuidKey.
        internal const string GAME_UUID_HEADER = "x-game-uuid";

        // Analytics Event Queue
        internal const float PROCESS_INTERVAL = 30f;
        internal const int MAX_QUEUE_SIZE = 1024;
        internal const int BATCH_SIZE = 128;
        internal const int MAX_QUEUE_SIZE_BYTES = 3 * 1024 * 1024; // 3MB limit

        // GrowthBook Configuration
        internal const string GROWTHBOOK_API_URL = "https://growthbook-api.playsuper.club";
        internal const string GROWTHBOOK_SDK_KEY = "sdk-7lLklUP0lUDKF2Q8";
        internal const int GROWTHBOOK_REFRESH_INTERVAL_SECONDS = 300; // How often to refresh feature flags
    }
}
