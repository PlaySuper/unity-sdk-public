using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PlaySuperUnity
{
    internal class GameManager
    {
        internal async static Task<GameData> GetGameData()
        {
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    using (var webRequest = UnityWebRequest.Get($"{baseUrl}/player/game"))
                    {
                        webRequest.SetRequestHeader("x-api-key", apiKey);
                        var operation = webRequest.SendWebRequest();
                        while (!operation.isDone)
                            await Task.Yield();
                        if (webRequest.result != UnityWebRequest.Result.Success)
                            throw new System.Exception($"HTTP {webRequest.responseCode}: {webRequest.error}");
                        string gameJson = webRequest.downloadHandler.text;
                        GameResponse gameData = JsonUtility.FromJson<GameResponse>(gameJson);
                        return gameData.data;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error GetGameData: {e.Message}");
                    return null;
                }
            }
            else return null;
        }
    }

    [System.Serializable]
    internal class StudioDetails
    {
        public string foundingYear;
        public string primaryGenre;
    }
    [System.Serializable]
    internal class Organization
    {
        public string id, name, type, profilePicture, companyUrl, createdAt, updatedAt, handle;
    }

    [System.Serializable]
    internal class Studio
    {
        public string id, organizationId, createdAt, updatedAt;
        public StudioDetails studioDetails;
        public Organization organization;
    }

    [System.Serializable]
    internal class GameData
    {
        public string id, studioId, name, createdAt, updatedAt, pictureUrl;
        public List<string> platform;
        public Studio studio;
    }

    [System.Serializable]
    internal class GameResponse
    {
        public GameData data;
        public int statusCode;
        public string message;
    }

}

