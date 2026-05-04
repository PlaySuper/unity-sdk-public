using UnityEngine;
using System.Collections.Generic;

namespace PlaySuperUnity
{
    internal class TransactionsManager
    {
        private const string saveKey = "transactions";
        private static List<Transaction> transactionList = new List<Transaction>();
        private static bool isInitialized = false;

        /// <summary>
        /// Ensures transactions are loaded from PlayerPrefs on first access.
        /// This fixes offline transactions being lost after app restart.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            string json = PlayerPrefs.GetString(saveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<TransactionListWrapper>(json);
                    transactionList = wrapper?.transactions ?? new List<Transaction>();
                    Debug.Log($"[PlaySuper] Loaded {transactionList.Count} pending offline transactions from storage");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PlaySuper] Failed to load offline transactions: {e.Message}");
                    transactionList = new List<Transaction>();
                }
            }
        }

        public static void AddTransaction(string coinId, int amount, string type = "distribute")
        {
            EnsureInitialized();
            Transaction t = new Transaction(coinId, amount, type);
            transactionList.Add(t);
            SaveTransactions();
        }

        public static void SaveTransactions()
        {
            TransactionListWrapper wrapper = new TransactionListWrapper(transactionList);
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefsSaveManager.ScheduleSave();
        }

        public static void ClearTransactions()
        {
            transactionList.Clear();
            PlayerPrefs.DeleteKey(saveKey);
        }

        public static List<Transaction> GetTransactions()
        {
            EnsureInitialized();
            return transactionList;
        }

        public static bool HasTransactions()
        {
            EnsureInitialized();
            return transactionList.Count > 0;
        }
    }

    [System.Serializable]
    internal class TransactionListWrapper
    {
        public List<Transaction> transactions;

        public TransactionListWrapper(List<Transaction> transactions)
        {
            this.transactions = transactions;
        }
    }

    [System.Serializable]
    public class Transaction
    {
        public string coinId;
        public int amount;
        public string type;

        public Transaction(string _coinId, int _amount, string _type = "distribute")
        {
            this.coinId = _coinId;
            this.amount = _amount;
            this.type = _type;
        }
    }
}