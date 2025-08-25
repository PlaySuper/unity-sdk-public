namespace PlaySuperUnity
{
    internal static class Constants
    {
        internal static class MixpanelEvent
        {
            public const string GAME_OPEN = "ps_sdk.game_opened";
            public const string GAME_CLOSE = "ps_sdk.game_closed";
            public const string STORE_OPEN = "ps_sdk.store_opened";
            public const string STORE_CLOSE = "ps_sdk.store_closed";
            public const string PLAYER_IDENTIFY = "ps_sdk.player_identified";
        }

        // TODO: Add kafka double write for mixpanel events
        internal const string MIXPANEL_URL =
            "https://7ecybbalvlg4pem67c4amx464i0fhpbx.lambda-url.ap-south-1.on.aws/sdk-event";
        internal const string MIXPANEL_URL_BATCH =
            "https://7ecybbalvlg4pem67c4amx464i0fhpbx.lambda-url.ap-south-1.on.aws/sdk-batch";

        internal const string PS_ANALYTICS_URL_PROD =
            "https://analytics.playsuper.club";

        internal const string deviceIdName = "device_id";

        internal const string lastCloseTimestampName = "lastCloseTimestamp";
        internal const string lastCloseDoneName = "lastCloseDone";

        internal const string devStoreUrl = "https://dev-store.playsuper.club/";
        internal const string prodStoreUrl = "https://store.playsuper.club/";

        internal const string devApiUrl = "https://dev.playsuper.club";
        internal const string prodApiUrl = "https://api.playsuper.club";

        // Mixpanel Event Queue
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
