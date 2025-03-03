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

        internal const string MIXPANEL_URL = "https://7ecybbalvlg4pem67c4amx464i0fhpbx.lambda-url.ap-south-1.on.aws/sdk-event";

        internal const string deviceIdName = "device_id";

        internal const string lastCloseTimestampName = "lastCloseTimestamp";
        internal const string lastCloseDoneName = "lastCloseDone";

        internal const string devStoreUrl = "https://dev-store.playsuper.club/";
        internal const string prodStoreUrl = "https://store.playsuper.club/";

        internal const string devApiUrl = "https://dev.playsuper.club/";
        internal const string prodApiUrl = "https://api.playsuper.club/";

    }
}