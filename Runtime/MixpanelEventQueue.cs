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
                    Debug.LogWarning("[MixPanel] Queue exceeded 3MB size limit; dropped oldest event");
                }

                // Fallback count-based limit
                if (eventQueue.Count > Constants.MAX_QUEUE_SIZE)
                {
                    eventQueue.RemoveAt(0);
                    Debug.LogWarning("[MixPanel] Queue exceeded max count; dropped oldest event");
                }

                SaveQueueToFile();
            }
            Debug.Log($"[MixPanel] Event queued: {eventName}, queue size: {GetQueueSize()}, bytes: {GetQueueSizeInBytes()}");

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
                SendResult result = await SendBatchToMixPanel(batchPayload);

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
                        SaveQueueToFile();
                    }
                    retryCount = 0; // Reset on success
                }
                else if (result == SendResult.PermanentFailure)
                {
                    // Remove permanently failed events - they'll never succeed
                    Debug.LogError("[MixPanel] Removing permanently failed batch to prevent infinite retries");
                    lock (queueLock)
                    {
                        var failedInsertIds = new HashSet<string>();
                        foreach (var ev in batch)
                        {
                            failedInsertIds.Add(ev.insertId);
                        }
                        eventQueue.RemoveAll(ev => failedInsertIds.Contains(ev.insertId));
                        SaveQueueToFile();
                    }
                    // Continue processing other batches
                }
                else // TransientFailure
                {
                    overallSuccess = false;
                    break; // Stop processing and retry later
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
                        Mathf.Min(90f, Mathf.Pow(2, retryCount)) + UnityEngine.Random.Range(0f, 1f);
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

        private static async Task<SendResult> SendBatchToMixPanel(string jsonPayload)
        {
            try
            {
                Debug.Log($"[MixPanel] Sending batch with {jsonPayload.Length} characters");

                var endpoint = PlaySuperUnitySDK.GetResolvedEventBatchUrl();
                Debug.Log($"[MixPanel] Using batch endpoint: {endpoint}");

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
                        Debug.LogError("[MixPanel] Network request exceeded timeout");
                        request.Abort();
                        return SendResult.TransientFailure;
                    }

                    // Handle different response codes appropriately
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"[MixPanel] Batch sent successfully: {request.responseCode}");
                        return SendResult.Success;
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
                            return SendResult.PermanentFailure;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[MixPanel] Transient error: {request.responseCode} - {request.error}"
                            );
                            Debug.LogWarning(
                                $"[MixPanel] Response: {request.downloadHandler.text}"
                            );
                            return SendResult.TransientFailure;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MixPanel] Exception sending batch: {ex.Message}");
                return SendResult.TransientFailure;
            }
        }

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

        public static void Initialize()
        {
            lock (queueLock)
            {
                LoadQueueFromFile();
            }
        }
    }
}
