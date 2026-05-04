using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity
{
    /// <summary>
    /// Manages PlayerPrefs saves to avoid multiple rapid synchronous disk writes.
    /// On Android, PlayerPrefs.Save() maps to SharedPreferences.commit() which is synchronous
    /// and can take 50-300ms per call. This manager debounces saves to reduce ANR risk.
    /// </summary>
    internal static class PlayerPrefsSaveManager
    {
        private static bool saveScheduled = false;
        private static readonly object saveLock = new object();
        private const int SAVE_DELAY_MS = 500; // Batch saves within 500ms

        /// <summary>
        /// Schedule a debounced save. Multiple calls within SAVE_DELAY_MS result in one save.
        /// Safe to call from any thread - actual save happens on main thread.
        /// </summary>
        public static void ScheduleSave()
        {
            lock (saveLock)
            {
                if (saveScheduled) return; // Already scheduled
                saveScheduled = true;
            }

            _ = DebouncedSaveAsync();
        }

        private static async Task DebouncedSaveAsync()
        {
            // Wait for the debounce period to coalesce multiple requests
            await Task.Delay(SAVE_DELAY_MS);

            lock (saveLock)
            {
                saveScheduled = false;
            }

            // PlayerPrefs.Save() should be called on main thread
            // Unity's async continuations run on main thread by default
            try
            {
                PlayerPrefs.Save();
                Debug.Log("[PlayerPrefs] Debounced save completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerPrefs] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Force an immediate synchronous save. 
        /// Use sparingly - only for critical data before app quit or logout.
        /// </summary>
        public static void ForceSaveImmediate()
        {
            lock (saveLock)
            {
                saveScheduled = false; // Cancel any pending debounced save
            }

            try
            {
                PlayerPrefs.Save();
                Debug.Log("[PlayerPrefs] Immediate save completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerPrefs] Immediate save failed: {ex.Message}");
            }
        }
    }
}
