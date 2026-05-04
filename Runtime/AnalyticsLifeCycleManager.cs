using System.Collections;
using UnityEngine;

namespace PlaySuperUnity
{
    internal class AnalyticsLifecycleManager : MonoBehaviour
    {
        private static AnalyticsLifecycleManager instance;
        private Coroutine processLoopCoroutine;
        private bool isRunning = true;

        public static void Initialize()
        {
            if (instance != null)
                return;

            GameObject go = new GameObject("AnalyticsLifecycleManager");
            instance = go.AddComponent<AnalyticsLifecycleManager>();
            DontDestroyOnLoad(go);
        }

        void Start()
        {
            isRunning = true;
            processLoopCoroutine = StartCoroutine(ProcessLoop());
        }

        void OnDestroy()
        {
            isRunning = false;
            if (processLoopCoroutine != null)
            {
                StopCoroutine(processLoopCoroutine);
                processLoopCoroutine = null;
            }
        }

        IEnumerator ProcessLoop()
        {
            while (isRunning)
            {
                yield return new WaitForSeconds(Constants.PROCESS_INTERVAL);

                if (isRunning && AnalyticsEventQueue.HasQueuedEvents())
                {
                    _ = AnalyticsEventQueue.ProcessQueue();
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) // App resumed
            {
                TryProcessQueue();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) // App got focus
            {
                TryProcessQueue();
            }
        }

        private void TryProcessQueue()
        {
            if (AnalyticsEventQueue.HasQueuedEvents())
            {
                _ = AnalyticsEventQueue.ProcessQueue();
            }
        }

        public static void Dispose()
        {
            if (instance != null)
            {
                DestroyImmediate(instance.gameObject);
                instance = null;
            }
        }
    }
}
