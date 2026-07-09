using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity
{
    /// <summary>
    /// First-party reporting of the previous process's exit when it was an ANR
    /// or crash. Reads Android's ActivityManager.getHistoricalProcessExitReasons
    /// (API 30+) on a worker thread and emits one analytics event per new bad
    /// exit on the main thread.
    ///
    /// Goal: verify the v4.2.0 ANR fix landed without waiting on Play Console
    /// vitals (which are sampled and have a 7-day window). Bad exits show up
    /// in our own analytics pipeline on the next cold start.
    ///
    /// Pre-API-30 Android, iOS, and Editor are silent no-ops.
    /// </summary>
    internal static class AnrTelemetry
    {
        // Persisted across launches so a single bad exit is reported exactly
        // once, not on every cold start.
        private const string LastReportedTimestampKey = "ps_anr_last_reported_ts";

        // android.app.ApplicationExitInfo.REASON_* constants we care about.
        private const int REASON_CRASH = 4;
        private const int REASON_CRASH_NATIVE = 5;
        private const int REASON_ANR = 6;

        // How many history entries to ask Android for. The OS buffer is small
        // and 10 is more than enough for a single launch.
        private const int MaxEntries = 10;

        /// <summary>
        /// Fire-and-forget. Inspects historical exit reasons and emits one
        /// <c>ps_sdk.previous_exit</c> event for each new ANR / crash since
        /// we last reported.
        /// </summary>
        public static void ReportPreviousExitIfBad()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _ = InspectAndEmitAsync();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task InspectAndEmitAsync()
        {
            try
            {
                // Main-thread reads: Unity APIs and PlayerPrefs.
                string gameVersion = Application.version ?? string.Empty;
                long lastReportedTs = ReadLastReportedTimestamp();

                // Worker thread: all the JNI / Android API access. Returns the
                // events to emit + the new high-watermark timestamp.
                InspectionResult result = await Task.Run(
                    () => InspectOnBackgroundThread(gameVersion, lastReportedTs));

                // Back on Unity sync context: fire events + persist watermark.
                // AnalyticsManager.SendEvent uses UnityWebRequest internally
                // and must be called from the main thread.
                if (result.Events != null)
                {
                    foreach (PendingEvent evt in result.Events)
                    {
                        _ = AnalyticsManager.SendEvent(
                            Constants.AnalyticsEvent.PREVIOUS_EXIT_OBSERVED,
                            evt.TimestampSec,
                            evt.Props);
                    }
                }

                if (result.NewestTs > lastReportedTs)
                {
                    WriteLastReportedTimestamp(result.NewestTs);
                }

                if (result.Events != null && result.Events.Count > 0)
                {
                    Debug.Log($"[PlaySuper] AnrTelemetry emitted {result.Events.Count} previous-exit event(s)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] AnrTelemetry failed: {ex.Message}");
            }
        }

        private struct PendingEvent
        {
            public long TimestampSec;
            public Dictionary<string, object> Props;
        }

        private struct InspectionResult
        {
            public List<PendingEvent> Events;
            public long NewestTs;
        }

        private static InspectionResult InspectOnBackgroundThread(string gameVersion, long lastReportedTs)
        {
            InspectionResult result = new InspectionResult
            {
                Events = null,
                NewestTs = lastReportedTs,
            };

            // 1. Resolve current Activity.
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return result;

            // 2. API gate. getHistoricalProcessExitReasons is API 30+.
            int sdkInt;
            using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                sdkInt = versionClass.GetStatic<int>("SDK_INT");
            }
            if (sdkInt < 30) return result;

            // 3. Resolve ActivityManager and package name.
            using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            string packageName = context.Call<string>("getPackageName");
            string activityServiceConst;
            using (var contextClass = new AndroidJavaClass("android.content.Context"))
            {
                activityServiceConst = contextClass.GetStatic<string>("ACTIVITY_SERVICE");
            }
            using var am = context.Call<AndroidJavaObject>("getSystemService", activityServiceConst);

            // 4. Game versionCode via PackageManager — matches the slice the
            //    Play Console report uses (versionCode 829 in the report).
            int gameVersionCode = 0;
            try
            {
                using var pm = context.Call<AndroidJavaObject>("getPackageManager");
                using var pkgInfo = pm.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                gameVersionCode = pkgInfo.Get<int>("versionCode");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] AnrTelemetry: versionCode read failed: {ex.Message}");
            }

            // 5. Fetch up to MaxEntries historical exit records.
            using var reasonsList = am.Call<AndroidJavaObject>(
                "getHistoricalProcessExitReasons", packageName, 0, MaxEntries);
            if (reasonsList == null) return result;
            int size = reasonsList.Call<int>("size");
            if (size == 0) return result;

            List<PendingEvent> events = new List<PendingEvent>();

            for (int i = 0; i < size; i++)
            {
                using var info = reasonsList.Call<AndroidJavaObject>("get", i);
                if (info == null) continue;

                long ts = info.Call<long>("getTimestamp");
                if (ts <= lastReportedTs) continue;

                // Advance the watermark for any non-stale entry, even uninteresting
                // ones — otherwise an EXIT_SELF older than an ANR could keep masking
                // it across launches.
                if (ts > result.NewestTs) result.NewestTs = ts;

                int reason = info.Call<int>("getReason");
                if (reason != REASON_ANR && reason != REASON_CRASH && reason != REASON_CRASH_NATIVE)
                {
                    continue;
                }

                Dictionary<string, object> props = new Dictionary<string, object>
                {
                    { "reason", reason },
                    { "reason_name", ReasonName(reason) },
                    { "description", SafeString(info, "getDescription") },
                    { "importance", info.Call<int>("getImportance") },
                    { "pss_kb", info.Call<long>("getPss") },
                    { "rss_kb", info.Call<long>("getRss") },
                    { "process_name", SafeString(info, "getProcessName") },
                    { "exit_timestamp_ms", ts },
                    { "sdk_version", Constants.SDK_VERSION },
                    { "game_version", gameVersion },
                    { "game_version_code", gameVersionCode },
                };

                events.Add(new PendingEvent
                {
                    TimestampSec = ts / 1000L,
                    Props = props,
                });
            }

            result.Events = events;
            return result;
        }

        private static string SafeString(AndroidJavaObject obj, string method)
        {
            try { return obj.Call<string>(method) ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string ReasonName(int reason)
        {
            switch (reason)
            {
                case REASON_CRASH: return "CRASH";
                case REASON_CRASH_NATIVE: return "CRASH_NATIVE";
                case REASON_ANR: return "ANR";
                default: return "OTHER_" + reason;
            }
        }

        private static long ReadLastReportedTimestamp()
        {
            if (!PlayerPrefs.HasKey(LastReportedTimestampKey)) return 0;
            return long.TryParse(PlayerPrefs.GetString(LastReportedTimestampKey, "0"), out long ts) ? ts : 0;
        }

        private static void WriteLastReportedTimestamp(long ts)
        {
            PlayerPrefs.SetString(LastReportedTimestampKey, ts.ToString());
            PlayerPrefsSaveManager.ScheduleSave();
        }
#endif
    }
}
