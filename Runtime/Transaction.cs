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
            string json = JsonUtility.ToJson(transactionList);
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
                string json = JsonUtility.ToJson(transactionList);
                transactionList = JsonUtility.FromJson<List<Transaction>>(json);
                return transactionList;
            }
            else
            {
                return null;
            }
        }

    }
    [System.Serializable]
    internal class Transaction
    {
        private string coinId;
        private int amount;

        public Transaction(string _coinId, int _amount)
        {
            coinId = _coinId;
            amount = _amount;
        }
    }
}