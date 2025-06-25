using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlaySuperUnity
{
    [System.Serializable]
    internal class MixPanelEvent
    {
        public string eventName;
        public long originalTimestamp;
        public string insertId;
        public string payloadJson; // Pre-serialized JSON payload

        public MixPanelEvent(string eventName, long timestamp, string payloadJson)
        {
            this.eventName = eventName;
            this.originalTimestamp = timestamp;
            this.insertId = Guid.NewGuid().ToString();
            this.payloadJson = payloadJson;
        }
    }

    [System.Serializable]
    internal class MixPanelEventListWrapper
    {
        public List<MixPanelEvent> events;

        public MixPanelEventListWrapper(List<MixPanelEvent> events)
        {
            this.events = events;
        }
    }

    internal class MixPanelEventQueue
    {
        private static readonly string QueueFilePath = Path.Combine(
            Application.persistentDataPath,
            "mixpanel_queue.json"
        );

        private static List<MixPanelEvent> eventQueue = new List<MixPanelEvent>();
        private static bool isProcessing = false;
        private static readonly object queueLock = new object();

        // Retry logic
        private static int retryCount = 0;
        private static float nextRetryTime = 0f;

        static MixPanelEventQueue()
        {
            LoadQueueFromFile();
        }

        public static void EnqueueEvent(string eventName, long timestamp, string payloadJson)
        {
            lock (queueLock)
            {
                // Clean old events first
                CleanOldEvents();

                eventQueue.Add(new MixPanelEvent(eventName, timestamp, payloadJson));

                // Enforce size limit
                if (eventQueue.Count > Constants.MAX_QUEUE_SIZE)
                {
                    eventQueue.RemoveAt(0);
                    Debug.LogWarning("[MixPanel] Queue exceeded max size; dropped oldest event");
                }

                SaveQueueToFile();
            }
            Debug.Log($"[MixPanel] Event queued: {eventName}, queue size: {GetQueueSize()}");
        }

        public static async Task ProcessQueue()
        {
            // Check retry backoff
            if (Time.realtimeSinceStartup < nextRetryTime)
                return;

            // Quick network check
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("[MixPanel] No network connectivity, skipping queue processing");
                return;
            }

            lock (queueLock)
            {
                if (isProcessing || eventQueue.Count == 0)
                    return;
                isProcessing = true;
            }

            List<MixPanelEvent> snapshot;
            lock (queueLock)
            {
                snapshot = new List<MixPanelEvent>(eventQueue);
            }

            int sentCount = 0;
            bool overallSuccess = true;

            Debug.Log(
                $"[MixPanel] Processing {snapshot.Count} queued events in batches of {Constants.BATCH_SIZE}"
            );

            // Process in batches
            for (int i = 0; i < snapshot.Count; i += Constants.BATCH_SIZE)
            {
                int batchCount = Mathf.Min(Constants.BATCH_SIZE, snapshot.Count - i);
                var batch = new List<MixPanelEvent>();
                for (int j = 0; j < batchCount; j++)
                {
                    batch.Add(snapshot[i + j]);
                }

                string batchPayload = BuildBatchPayload(batch);
                bool success = await SendBatchToMixPanel(batchPayload);

                if (success)
                {
                    sentCount += batch.Count;
                    // Remove successful events from main queue
                    lock (queueLock)
                    {
                        foreach (var ev in batch)
                        {
                            eventQueue.Remove(ev);
                        }
                        SaveQueueToFile();
                    }
                    retryCount = 0; // Reset on success
                }
                else
                {
                    overallSuccess = false;
                    break; // Stop processing on failure
                }

                // Small delay between batches
                await Task.Delay(100);
            }

            lock (queueLock)
            {
                isProcessing = false;

                // Schedule retry if failed
                if (!overallSuccess && eventQueue.Count > 0)
                {
                    retryCount++;
                    float backoff =
                        Mathf.Min(60f, Mathf.Pow(2, retryCount)) + UnityEngine.Random.Range(0f, 1f);
                    nextRetryTime = Time.realtimeSinceStartup + backoff;
                    Debug.LogWarning(
                        $"[MixPanel] Batch send failed, retry #{retryCount} in {backoff:F1}s"
                    );
                }
            }

            Debug.Log(
                $"[MixPanel] ProcessQueue complete: sent {sentCount}, remaining {GetQueueSize()}"
            );
        }

        private static string BuildBatchPayload(List<MixPanelEvent> batch)
        {
            var eventsJsonArray = new List<string>();

            foreach (var ev in batch)
            {
                try
                {
                    // Parse the JSON to ensure it's valid and properly structured
                    var eventJson = ev.payloadJson;

                    // Validate it's proper JSON by attempting to parse
                    if (
                        string.IsNullOrEmpty(eventJson)
                        || !eventJson.Trim().StartsWith("{")
                        || !eventJson.Trim().EndsWith("}")
                    )
                    {
                        Debug.LogWarning(
                            $"[MixPanel] Invalid JSON payload for event {ev.eventName}, skipping"
                        );
                        continue;
                    }

                    // For now, trust the payload is correctly formatted
                    // TODO: Consider using Newtonsoft.Json for robust parsing/rebuilding
                    eventsJsonArray.Add(eventJson.Trim());
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[MixPanel] Error processing event {ev.eventName}: {ex.Message}"
                    );
                    continue;
                }
            }

            if (eventsJsonArray.Count == 0)
            {
                Debug.LogWarning("[MixPanel] No valid events to send in batch");
                return "[]";
            }

            // Build proper JSON array
            var batchJson = "[\n  " + string.Join(",\n  ", eventsJsonArray) + "\n]";

            Debug.Log($"[MixPanel] Built batch with {eventsJsonArray.Count} events");
            return batchJson;
        }

        private static async Task<bool> SendBatchToMixPanel(string jsonPayload)
        {
            try
            {
                Debug.Log($"[MixPanel] Sending batch with {jsonPayload.Length} characters");

                using (
                    var request = new UnityEngine.Networking.UnityWebRequest(
                        Constants.MIXPANEL_URL_BATCH,
                        "POST"
                    )
                )
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 30;

                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    // Handle different response codes appropriately
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[MixPanel] Batch sent successfully: {request.responseCode}");
                        return true;
                    }
                    else
                    {
                        // Check if it's a permanent error (4xx) vs transient (5xx/network)
                        if (request.responseCode >= 400 && request.responseCode < 500)
                        {
                            Debug.LogError(
                                $"[MixPanel] Permanent error - bad request: {request.responseCode} - {request.error}"
                            );
                            Debug.LogError($"[MixPanel] Response: {request.downloadHandler.text}");

                            // For permanent errors, we should probably clear the queue or handle differently
                            // For now, still return false to trigger retry, but log as permanent
                            return false;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[MixPanel] Transient error: {request.responseCode} - {request.error}"
                            );
                            Debug.LogWarning(
                                $"[MixPanel] Response: {request.downloadHandler.text}"
                            );
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MixPanel] Exception sending batch: {ex.Message}");
                return false;
            }
        }

        private static void CleanOldEvents()
        {
            long cutoffTime = DateTimeOffset
                .UtcNow.AddDays(-Constants.MAX_EVENT_AGE_DAYS)
                .ToUnixTimeSeconds();
            int removedCount = eventQueue.RemoveAll(e => e.originalTimestamp < cutoffTime);

            if (removedCount > 0)
            {
                Debug.Log(
                    $"[MixPanel] Removed {removedCount} old events (older than {Constants.MAX_EVENT_AGE_DAYS} days)"
                );
            }
        }

        private static void SaveQueueToFile()
        {
            try
            {
                var wrapper = new MixPanelEventListWrapper(eventQueue);
                string json = JsonUtility.ToJson(wrapper);
                string tmpPath = QueueFilePath + ".tmp";

                // Atomic write: write to temp file, then replace
                File.WriteAllText(tmpPath, json);
                if (File.Exists(QueueFilePath))
                {
                    File.Delete(QueueFilePath);
                }
                File.Move(tmpPath, QueueFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MixPanel] Error saving queue to file: {ex.Message}");
            }
        }

        private static void LoadQueueFromFile()
        {
            try
            {
                if (File.Exists(QueueFilePath))
                {
                    string json = File.ReadAllText(QueueFilePath);
                    var wrapper = JsonUtility.FromJson<MixPanelEventListWrapper>(json);
                    eventQueue = wrapper?.events ?? new List<MixPanelEvent>();

                    // Clean old events on load
                    CleanOldEvents();

                    Debug.Log($"[MixPanel] Loaded {eventQueue.Count} events from file");
                }
                else
                {
                    eventQueue = new List<MixPanelEvent>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MixPanel] Error loading queue from file: {ex.Message}");
                // Backup corrupted file and start fresh
                try
                {
                    if (File.Exists(QueueFilePath))
                    {
                        File.Move(QueueFilePath, QueueFilePath + ".corrupted");
                    }
                }
                catch { }
                eventQueue = new List<MixPanelEvent>();
            }
        }

        public static void ClearQueue()
        {
            lock (queueLock)
            {
                eventQueue.Clear();
                try
                {
                    if (File.Exists(QueueFilePath))
                    {
                        File.Delete(QueueFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MixPanel] Error deleting queue file: {ex.Message}");
                }
            }
            Debug.Log("[MixPanel] Queue cleared");
        }

        public static int GetQueueSize()
        {
            lock (queueLock)
            {
                return eventQueue.Count;
            }
        }

        public static bool HasQueuedEvents()
        {
            lock (queueLock)
            {
                return eventQueue.Count > 0;
            }
        }

        public static void Dispose()
        {
            lock (queueLock)
            {
                // Save any remaining events before disposing
                if (eventQueue.Count > 0)
                {
                    Debug.Log(
                        $"[MixPanel] Disposing with {eventQueue.Count} events - saving to file"
                    );
                    SaveQueueToFile();
                }

                // Clear in-memory queue
                eventQueue.Clear();

                // Reset processing state
                isProcessing = false;
                retryCount = 0;
                nextRetryTime = 0f;
            }

            Debug.Log("[MixPanel] EventQueue disposed");
        }
    }
}
