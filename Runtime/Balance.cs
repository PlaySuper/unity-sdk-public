using PlaySuperUnity;
using System.Collections.Generic;

namespace PlaySuperUnity
{
    [System.Serializable]
    public class Coin
    {
        public string id;
        public string name;
        public string url;

        public Coin(string _id, string _name, string _url)
        {
            this.id = _id;
            this.name = _name;
            this.url = _url;
        }
    }

    [System.Serializable]
    public class CoinBalance : Coin
    {
        public int amount;
        public CoinBalance(string _id, string _name, string _url, int _amount)
        : base(_id, _name, _url)
        {
            this.amount = _amount;
        }
    }

    [System.Serializable]
    internal class CoinResponse
    {
        public List<Coin> data;
        public int statusCode;
        public string message;

        public CoinResponse(List<Coin> _data, int _statusCode, string _message)
        {
            this.data = _data;
            this.statusCode = _statusCode;
            this.message = _message;
        }
    }

    [System.Serializable]
    internal class CoinDetails
    {
        public string name;
        public int convertionRate;
        public string pictureUrl;
        public string expiry;
        public bool neverExpire;
        public bool isOrgWide;
    }

    [System.Serializable]
    internal class PlayerCoin
    {
        public string id;
        public string playerId;
        public string coinId;
        public int balance;
        public CoinDetails coin;
    }

    [System.Serializable]
    internal class FundResponse
    {
        public List<PlayerCoin> data;
        public int statusCode;
        public string message;
    }
}