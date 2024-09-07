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

        internal const string MIXPANEL_TOKEN = "c349a0c47b10507f76af7af71addb382";
        internal const string MIXPANEL_URL = "https://api.mixpanel.com/track";

        internal const string deviceIdName = "device_id";

        internal const string lastCloseTimestampName = "lastCloseTimestamp";
        internal const string lastCloseDoneName = "lastCloseDone";
    }
}