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
        public int retryCount;

        public MixPanelEvent(string eventName, long timestamp, string payloadJson)
        {
            this.eventName = eventName;
            this.originalTimestamp = timestamp;
            this.insertId = Guid.NewGuid().ToString();
            this.payloadJson = payloadJson;
            this.retryCount = 0;
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

        // Save coalescing - prevents multiple rapid disk writes
        private static bool savePending = false;
        private static readonly object saveLock = new object();
        private const int SAVE_DELAY_MS = 100; // Coalesce saves within 100ms

        public enum SendResult { Success, TransientFailure, PermanentFailure }

        static MixPanelEventQueue()
        {
            Initialize();
        }

        public static void EnqueueEvent(string eventName, long timestamp, string payloadJson)
        {
            lock (queueLock)
            {
                eventQueue.Add(new MixPanelEvent(eventName, timestamp, payloadJson));

                // Enforce size limit (3MB) - remove oldest events if exceeded
                while (GetQueueSizeInBytes() > Constants.MAX_QUEUE_SIZE_BYTES && eventQueue.Count > 0)
                {
                    eventQueue.RemoveAt(0); // Remove oldest (FIFO)
                    Debug.LogWarning("[Analytics] Queue exceeded 3MB size limit; dropped oldest event");
                }

                // Fallback count-based limit
                if (eventQueue.Count > Constants.MAX_QUEUE_SIZE)
                {
                    eventQueue.RemoveAt(0);
                    Debug.LogWarning("[Analytics] Queue exceeded max count; dropped oldest event");
                }
            }

            // Schedule async save OUTSIDE the lock to avoid blocking
            ScheduleSave();

            Debug.Log($"[Analytics] Event queued: {eventName}, queue size: {GetQueueSize()}, bytes: {GetQueueSizeInBytes()}");

            // Always try to process queue when adding an event
            _ = ProcessQueue();
        }

        public static async Task ProcessQueue()
        {
            // Check retry backoff
            if (Time.realtimeSinceStartup < nextRetryTime)
                return;

            // Quick network check
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("[Analytics] No network connectivity, skipping queue processing");
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
            bool queueModified = false;  // Track if we need to save

            Debug.Log(
                $"[Analytics] Processing {snapshot.Count} queued events in batches of {Constants.BATCH_SIZE}"
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
                SendResult result = await SendBatchToAnalytics(batchPayload);

                if (result == SendResult.Success)
                {
                    sentCount += batch.Count;
                    // Remove successfully sent events
                    lock (queueLock)
                    {
                        var successfulInsertIds = new HashSet<string>();
                        foreach (var ev in batch)
                        {
                            successfulInsertIds.Add(ev.insertId);
                        }
                        eventQueue.RemoveAll(ev => successfulInsertIds.Contains(ev.insertId));
                    }
                    queueModified = true;
                    retryCount = 0; // Reset on success
                }
                else if (result == SendResult.PermanentFailure)
                {
                    // Remove permanently failed events - they'll never succeed
                    Debug.LogError("[Analytics] Removing permanently failed batch to prevent infinite retries");
                    lock (queueLock)
                    {
                        var failedInsertIds = new HashSet<string>();
                        foreach (var ev in batch)
                        {
                            failedInsertIds.Add(ev.insertId);
                        }
                        eventQueue.RemoveAll(ev => failedInsertIds.Contains(ev.insertId));
                    }
                    queueModified = true;
                    // Continue processing other batches
                }
                else // TransientFailure
                {
                    // Increment retry count on events for next attempt
                    lock (queueLock)
                    {
                        var failedInsertIds = new HashSet<string>();
                        foreach (var ev in batch)
                        {
                            failedInsertIds.Add(ev.insertId);
                        }
                        foreach (var ev in eventQueue)
                        {
                            if (failedInsertIds.Contains(ev.insertId))
                            {
                                ev.retryCount++;
                            }
                        }
                    }
                    queueModified = true;
                    overallSuccess = false;
                    break; // Stop processing and retry later
                }

                // Small delay between batches (~100ms at 60fps)
                for (int f = 0; f < 6; f++)
                    await Task.Yield();
            }

            // Single save at end of processing (instead of per-batch)
            if (queueModified)
            {
                ScheduleSave();
            }

            lock (queueLock)
            {
                isProcessing = false;

                // Schedule retry if failed
                if (!overallSuccess && eventQueue.Count > 0)
                {
                    retryCount++;
                    float backoff =
                        Mathf.Min(90f, Mathf.Pow(2, retryCount)) + UnityEngine.Random.Range(0f, 1f);
                    nextRetryTime = Time.realtimeSinceStartup + backoff;
                    Debug.LogWarning(
                        $"[Analytics] Batch send failed, retry #{retryCount} in {backoff:F1}s"
                    );
                }
            }

            Debug.Log(
                $"[Analytics] ProcessQueue complete: sent {sentCount}, remaining {GetQueueSize()}"
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
                            $"[Analytics] Invalid JSON payload for event {ev.eventName}, skipping"
                        );
                        continue;
                    }

                    // The payloadJson has format: {"event_name": "...", "properties": {...}}
                    // We need to extract just the inner "properties" object for analytics API
                    string propertiesJson = ExtractPropertiesFromPayload(eventJson.Trim());

                    if (string.IsNullOrEmpty(propertiesJson))
                    {
                        Debug.LogWarning(
                            $"[Analytics] Could not extract properties from payload for event {ev.eventName}, skipping"
                        );
                        continue;
                    }

                    // Build event in analytics.playsuper.club format
                    var analyticsEvent = $@"{{
      ""eventName"": ""{ev.eventName}"",
      ""properties"": {propertiesJson},
      ""timestamp"": {ev.originalTimestamp * 1000},
      ""eventId"": ""{ev.insertId}"",
      ""retryCount"": {ev.retryCount}
    }}";

                    eventsJsonArray.Add(analyticsEvent);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[Analytics] Error processing event {ev.eventName}: {ex.Message}"
                    );
                    continue;
                }
            }

            if (eventsJsonArray.Count == 0)
            {
                Debug.LogWarning("[Analytics] No valid events to send in batch");
                return "{}";
            }

            // Build payload matching analytics.playsuper.club format
            var batchJson = $@"{{
  ""events"": [
    {string.Join(",\n    ", eventsJsonArray)}
  ],
  ""batchMetadata"": {{
    ""sentAt"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}"",
    ""queueSize"": {GetQueueSize()},
    ""flushReason"": ""sdk_batch"",
    ""sdkVersion"": ""unity"",
    ""platform"": ""{GetPlatform()}""
  }}
}}";

            // Log event IDs being sent
            foreach (var ev in batch)
            {
                Debug.Log($"[Analytics] Sending event: {ev.eventName} (id: {ev.insertId})");
            }
            Debug.Log($"[Analytics] Built batch with {eventsJsonArray.Count} events");
            return batchJson;
        }

        private static string GetPlatform()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_WEBGL
            return "webgl";
#elif UNITY_EDITOR
            return "editor";
#else
            return "unknown";
#endif
        }

        /// <summary>
        /// Extract the "properties" object from a payload JSON string.
        /// The payload format is: {"event_name": "...", "properties": {...}}
        /// We need to return just the inner {...} for the properties key.
        /// </summary>
        private static string ExtractPropertiesFromPayload(string payloadJson)
        {
            try
            {
                // Find the "properties" key
                const string propertiesKey = "\"properties\":";
                int propertiesIndex = payloadJson.IndexOf(propertiesKey);

                if (propertiesIndex == -1)
                {
                    // No properties key found, return the entire payload as properties
                    return payloadJson;
                }

                // Find the start of the properties object (after "properties":)
                int startIndex = propertiesIndex + propertiesKey.Length;

                // Skip whitespace
                while (startIndex < payloadJson.Length && char.IsWhiteSpace(payloadJson[startIndex]))
                {
                    startIndex++;
                }

                if (startIndex >= payloadJson.Length || payloadJson[startIndex] != '{')
                {
                    return null;
                }

                // Find matching closing brace
                int braceCount = 0;
                int endIndex = startIndex;

                for (int i = startIndex; i < payloadJson.Length; i++)
                {
                    char c = payloadJson[i];
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;

                    if (braceCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }

                if (braceCount != 0)
                {
                    return null; // Unbalanced braces
                }

                return payloadJson.Substring(startIndex, endIndex - startIndex + 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Analytics] Error extracting properties: {ex.Message}");
                return null;
            }
        }

        private static async Task<SendResult> SendBatchToAnalytics(string jsonPayload)
        {
            try
            {
                Debug.Log($"[Analytics] Sending batch with {jsonPayload.Length} characters");

                var endpoint = PlaySuperUnitySDK.GetResolvedEventBatchUrl();
                Debug.Log($"[Analytics] Using batch endpoint: {endpoint}");

                using (
                    var request = new UnityEngine.Networking.UnityWebRequest(
                        endpoint,
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

                    // Add timeout protection
                    int attempts = 0;
                    const int maxAttempts = 350; // ~35 seconds

                    while (!operation.isDone && attempts < maxAttempts)
                    {
                        await Task.Yield();
                        attempts++;
                    }

                    if (!operation.isDone)
                    {
                        Debug.LogError("[Analytics] Network request exceeded timeout");
                        request.Abort();
                        return SendResult.TransientFailure;
                    }

                    // Handle different response codes appropriately
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[Analytics] Batch sent successfully: {request.responseCode}");
                        return SendResult.Success;
                    }
                    else
                    {
                        // Check if it's a permanent error (4xx) vs transient (5xx/network)
                        if (request.responseCode >= 400 && request.responseCode < 500)
                        {
                            Debug.LogError(
                                $"[Analytics] Permanent error - bad request: {request.responseCode} - {request.error}"
                            );
                            Debug.LogError($"[Analytics] Response: {request.downloadHandler.text}");
                            return SendResult.PermanentFailure;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[Analytics] Transient error: {request.responseCode} - {request.error}"
                            );
                            Debug.LogWarning(
                                $"[Analytics] Response: {request.downloadHandler.text}"
                            );
                            return SendResult.TransientFailure;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Analytics] Exception sending batch: {ex.Message}");
                return SendResult.TransientFailure;
            }
        }

        /// <summary>
        /// Synchronous save - only used for critical saves (Dispose, app quit)
        /// </summary>
        private static void SaveQueueToFile()
        {
            List<MixPanelEvent> snapshot;
            lock (queueLock)
            {
                snapshot = new List<MixPanelEvent>(eventQueue);
            }

            try
            {
                var wrapper = new MixPanelEventListWrapper(snapshot);
                string json = JsonUtility.ToJson(wrapper);
                string tmpPath = QueueFilePath + ".tmp";

                File.WriteAllText(tmpPath, json);
                if (File.Exists(QueueFilePath))
                {
                    File.Delete(QueueFilePath);
                }
                File.Move(tmpPath, QueueFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Analytics] Error saving queue to file: {ex.Message}");
            }
        }

        /// <summary>
        /// Schedule a coalesced async save. Multiple rapid calls result in a single disk write.
        /// This prevents ANR by moving I/O off the main thread.
        /// </summary>
        private static void ScheduleSave()
        {
            lock (saveLock)
            {
                if (savePending) return;  // Already scheduled
                savePending = true;
            }

            _ = CoalescedSaveAsync();
        }

        /// <summary>
        /// Waits briefly to coalesce rapid save requests, then performs async save.
        /// </summary>
        private static async Task CoalescedSaveAsync()
        {
            // Wait to coalesce multiple rapid events
            await Task.Delay(SAVE_DELAY_MS);

            lock (saveLock)
            {
                savePending = false;
            }

            await SaveQueueToFileAsync();
        }

        /// <summary>
        /// Async version that runs file I/O on a background thread.
        /// Safe to call from main thread - will not cause ANR.
        /// </summary>
        private static async Task SaveQueueToFileAsync()
        {
            List<MixPanelEvent> snapshot;
            lock (queueLock)
            {
                if (eventQueue.Count == 0)
                {
                    // Clear file if queue is empty
                    try
                    {
                        await Task.Run(() =>
                        {
                            if (File.Exists(QueueFilePath))
                                File.Delete(QueueFilePath);
                        });
                    }
                    catch { }
                    return;
                }
                snapshot = new List<MixPanelEvent>(eventQueue);
            }

            // Perform I/O on background thread to avoid ANR
            await Task.Run(() =>
            {
                try
                {
                    var wrapper = new MixPanelEventListWrapper(snapshot);
                    string json = JsonUtility.ToJson(wrapper);
                    string tmpPath = QueueFilePath + ".tmp";

                    File.WriteAllText(tmpPath, json);
                    if (File.Exists(QueueFilePath))
                    {
                        File.Delete(QueueFilePath);
                    }
                    File.Move(tmpPath, QueueFilePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Analytics] Error in async save: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Force an immediate synchronous save. Use sparingly - only for critical data before app quit.
        /// </summary>
        private static void ForceSaveImmediate()
        {
            lock (saveLock)
            {
                savePending = false;  // Cancel any pending async save
            }
            SaveQueueToFile();
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

                    Debug.Log($"[Analytics] Loaded {eventQueue.Count} events from file");
                }
                else
                {
                    eventQueue = new List<MixPanelEvent>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Analytics] Error loading queue from file: {ex.Message}");
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
                    Debug.LogError($"[Analytics] Error deleting queue file: {ex.Message}");
                }
            }
            Debug.Log("[Analytics] Queue cleared");
        }

        public static int GetQueueSize()
        {
            lock (queueLock)
            {
                return eventQueue.Count;
            }
        }

        private static int GetQueueSizeInBytes()
        {
            lock (queueLock)
            {
                int totalBytes = 0;
                foreach (var ev in eventQueue)
                {
                    // Calculate approximate size: eventName + payloadJson + insertId + some overhead
                    totalBytes += Encoding.UTF8.GetByteCount(ev.eventName ?? "");
                    totalBytes += Encoding.UTF8.GetByteCount(ev.payloadJson ?? "");
                    totalBytes += Encoding.UTF8.GetByteCount(ev.insertId ?? "");
                    totalBytes += 8; // timestamp (long) + some overhead
                }
                return totalBytes;
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
            // Force immediate save before disposing (synchronous, blocks until complete)
            // This ensures no data loss on app quit
            int eventCount;
            lock (queueLock)
            {
                eventCount = eventQueue.Count;
            }

            if (eventCount > 0)
            {
                Debug.Log($"[Analytics] Disposing with {eventCount} events - forcing immediate save");
                ForceSaveImmediate();
            }

            lock (queueLock)
            {
                // Clear in-memory queue
                eventQueue.Clear();

                // Reset processing state
                isProcessing = false;
                retryCount = 0;
                nextRetryTime = 0f;
            }

            Debug.Log("[Analytics] EventQueue disposed");
        }

        public static void Initialize()
        {
            lock (queueLock)
            {
                LoadQueueFromFile();
            }
        }
    }
}
