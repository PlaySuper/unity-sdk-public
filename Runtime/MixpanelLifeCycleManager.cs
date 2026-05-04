using System.Collections;
using UnityEngine;

namespace PlaySuperUnity
{
    internal class MixPanelLifecycleManager : MonoBehaviour
    {
        private static MixPanelLifecycleManager instance;
        private Coroutine processLoopCoroutine;
        private bool isRunning = true;

        public static void Initialize()
        {
            if (instance != null)
                return;

            GameObject go = new GameObject("MixPanelLifecycleManager");
            instance = go.AddComponent<MixPanelLifecycleManager>();
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

                if (isRunning && MixPanelEventQueue.HasQueuedEvents())
                {
                    _ = MixPanelEventQueue.ProcessQueue();
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
            if (MixPanelEventQueue.HasQueuedEvents())
            {
                _ = MixPanelEventQueue.ProcessQueue();
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
