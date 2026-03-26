using UnityEngine;
using System;
using System.Collections.Generic;

namespace PlaySuperUnity
{
    /// <summary>
    /// Individual transaction from SDK sync (purchase debits and refund credits)
    /// </summary>
    [System.Serializable]
    public class SdkTransaction
    {
        public string id;
        public float amount;
        public string type;       // CREDIT or DEBIT
        public string source;     // PURCHASE_DEBIT or REFUND_CREDIT
        public string coinId;
        public string coinName;
        public string description;
        public string createdAt;  // ISO 8601 timestamp
    }

    /// <summary>
    /// Inner data from GET /player/sdk-transactions
    /// </summary>
    [System.Serializable]
    internal class SdkTransactionsData
    {
        public List<SdkTransaction> transactions;
        public string lastSyncedTransactionId;
        public bool hasVisitedStore;
    }

    /// <summary>
    /// Response wrapper from GET /player/sdk-transactions
    /// </summary>
    [System.Serializable]
    internal class SdkTransactionsResponse
    {
        public SdkTransactionsData data;
    }

    /// <summary>
    /// Inner data from POST /player/mark-store-visited
    /// </summary>
    [System.Serializable]
    internal class MarkStoreVisitedData
    {
        public bool success;
        public bool alreadyVisited;
    }

    /// <summary>
    /// Response wrapper from POST /player/mark-store-visited
    /// </summary>
    [System.Serializable]
    internal class MarkStoreVisitedResponse
    {
        public MarkStoreVisitedData data;
    }

    /// <summary>
    /// Request body for POST /player/sdk-transactions/commit
    /// </summary>
    [System.Serializable]
    internal class CommitSdkSyncRequest
    {
        public string lastProcessedTransactionId;

        public CommitSdkSyncRequest(string transactionId)
        {
            lastProcessedTransactionId = transactionId;
        }
    }

    /// <summary>
    /// Inner data from POST /player/sdk-transactions/commit
    /// </summary>
    [System.Serializable]
    internal class CommitSdkSyncData
    {
        public bool success;
        public string newCheckpoint;
    }

    /// <summary>
    /// Response wrapper from POST /player/sdk-transactions/commit
    /// </summary>
    [System.Serializable]
    internal class CommitSdkSyncResponse
    {
        public CommitSdkSyncData data;
    }

    /// <summary>
    /// Wrapper for serializing list of SdkTransactions to JSON
    /// </summary>
    [System.Serializable]
    internal class SdkTransactionListWrapper
    {
        public List<SdkTransaction> transactions;

        public SdkTransactionListWrapper()
        {
            transactions = new List<SdkTransaction>();
        }

        public SdkTransactionListWrapper(List<SdkTransaction> txns)
        {
            transactions = txns;
        }
    }

    /// <summary>
    /// Manages SDK transaction sync state in PlayerPrefs
    /// Stores pending (unprocessed) transactions and the last synced checkpoint
    /// </summary>
    internal static class SdkTransactionSyncManager
    {
        private const string PENDING_TRANSACTIONS_KEY = "sdk_pending_transactions";
        private const string LAST_SYNCED_CHECKPOINT_KEY = "sdk_last_synced_transaction_id";
        private const string HAS_VISITED_STORE_KEY = "sdk_has_visited_store";

        /// <summary>
        /// Check if player has visited the store (local cache)
        /// </summary>
        public static bool HasVisitedStore()
        {
            return PlayerPrefs.GetInt(HAS_VISITED_STORE_KEY, 0) == 1;
        }

        /// <summary>
        /// Mark that player has visited the store (local cache)
        /// </summary>
        public static void SetHasVisitedStore(bool visited)
        {
            PlayerPrefs.SetInt(HAS_VISITED_STORE_KEY, visited ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Get the locally cached last synced transaction ID
        /// </summary>
        public static string GetLastSyncedCheckpoint()
        {
            return PlayerPrefs.GetString(LAST_SYNCED_CHECKPOINT_KEY, null);
        }

        /// <summary>
        /// Update the locally cached checkpoint after successful commit
        /// </summary>
        public static void SetLastSyncedCheckpoint(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
            {
                PlayerPrefs.DeleteKey(LAST_SYNCED_CHECKPOINT_KEY);
            }
            else
            {
                PlayerPrefs.SetString(LAST_SYNCED_CHECKPOINT_KEY, transactionId);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Get pending transactions from local storage
        /// </summary>
        public static List<SdkTransaction> GetPendingTransactions()
        {
            if (!PlayerPrefs.HasKey(PENDING_TRANSACTIONS_KEY))
            {
                return new List<SdkTransaction>();
            }

            string json = PlayerPrefs.GetString(PENDING_TRANSACTIONS_KEY);
            if (string.IsNullOrEmpty(json))
            {
                return new List<SdkTransaction>();
            }

            try
            {
                SdkTransactionListWrapper wrapper = JsonUtility.FromJson<SdkTransactionListWrapper>(json);
                return wrapper?.transactions ?? new List<SdkTransaction>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaySuper] Failed to parse pending transactions: {e.Message}");
                return new List<SdkTransaction>();
            }
        }

        /// <summary>
        /// Save pending transactions to local storage
        /// </summary>
        public static void SavePendingTransactions(List<SdkTransaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
            {
                PlayerPrefs.DeleteKey(PENDING_TRANSACTIONS_KEY);
            }
            else
            {
                SdkTransactionListWrapper wrapper = new SdkTransactionListWrapper(transactions);
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(PENDING_TRANSACTIONS_KEY, json);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Add new transactions to pending list (merges with existing)
        /// </summary>
        public static void AddPendingTransactions(List<SdkTransaction> newTransactions)
        {
            if (newTransactions == null || newTransactions.Count == 0)
            {
                return;
            }

            List<SdkTransaction> existing = GetPendingTransactions();

            // Create a set of existing IDs to avoid duplicates
            HashSet<string> existingIds = new HashSet<string>();
            foreach (var txn in existing)
            {
                existingIds.Add(txn.id);
            }

            // Add only new transactions
            foreach (var txn in newTransactions)
            {
                if (!existingIds.Contains(txn.id))
                {
                    existing.Add(txn);
                }
            }

            SavePendingTransactions(existing);
        }

        /// <summary>
        /// Remove transactions up to and including the given ID after successful processing
        /// </summary>
        public static void RemoveProcessedTransactions(string upToTransactionId)
        {
            List<SdkTransaction> pending = GetPendingTransactions();
            if (pending.Count == 0)
            {
                return;
            }

            // Find the index of the committed transaction
            int removeUpToIndex = -1;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].id == upToTransactionId)
                {
                    removeUpToIndex = i;
                    break;
                }
            }

            if (removeUpToIndex >= 0)
            {
                // Remove all transactions up to and including this one
                pending.RemoveRange(0, removeUpToIndex + 1);
                SavePendingTransactions(pending);
            }
        }

        /// <summary>
        /// Check if there are pending transactions to process
        /// </summary>
        public static bool HasPendingTransactions()
        {
            return GetPendingTransactions().Count > 0;
        }

        /// <summary>
        /// Clear all pending transactions and checkpoint (used for logout/reset)
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(PENDING_TRANSACTIONS_KEY);
            PlayerPrefs.DeleteKey(LAST_SYNCED_CHECKPOINT_KEY);
            PlayerPrefs.DeleteKey(HAS_VISITED_STORE_KEY);
            PlayerPrefs.Save();
        }
    }
}
