using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using UnityEngine;

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
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/player/game");

                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                    request.Headers.Add("x-api-key", apiKey);
                    HttpResponseMessage response = await client.SendAsync(request);

                    response.EnsureSuccessStatusCode();
                    string gameJson = await response.Content.ReadAsStringAsync();
                    GameResponse gameData = JsonUtility.FromJson<GameResponse>(gameJson);
                    return gameData.data;
                }
                catch (HttpRequestException e)
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
    internal class Studio
    {
        public string id, organizationId, createdAt, updatedAt;
        public StudioDetails studioDetails;
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

