using UnityEngine;
using System.Collections.Generic;

namespace PlaySuperUnity
{
    internal class TransactionsManager
    {
        private const string saveKey = "transactions";
        public static List<Transaction> transactionList = new List<Transaction>();

        public static void AddTransaction(string coinId, int amount)
        {
            Transaction t = new Transaction(coinId, amount);
            transactionList.Add(t);
            SaveTransactions();
        }

        public static void SaveTransactions()
        {
            TransactionListWrapper wrapper = new TransactionListWrapper(transactionList);
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }

        public static void ClearTransactions()
        {
            transactionList.Clear();
            PlayerPrefs.DeleteKey(saveKey);
        }

        public static List<Transaction> GetTransactions()
        {
            if (PlayerPrefs.HasKey(saveKey))
            {
                return transactionList;
            }
            else
            {
                return null;
            }
        }

        public static bool HasTransactions()
        {
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

        public Transaction(string _coinId, int _amount)
        {
            this.coinId = _coinId;
            this.amount = _amount;
        }
    }
}