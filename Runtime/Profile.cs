using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;

namespace PlaySuperUnity
{

    public class ProfileManager
    {
        internal static async Task<ProfileData> GetProfileData()
        {
            string token = PlaySuperUnitySDK.Instance.GetAuthToken();
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.Instance.GetApiKey();
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/player/profile");
                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                    request.Headers.Add("x-api-key", apiKey);
                    request.Headers.Add("Authorization", $"Bearer {token}");
                    HttpResponseMessage response = await client.SendAsync(request);

                    response.EnsureSuccessStatusCode();
                    string profileJson = await response.Content.ReadAsStringAsync();
                    ProfileResponse profileData = JsonUtility.FromJson<ProfileResponse>(profileJson);
                    return profileData.data;
                }
                catch (HttpRequestException e)
                {
                    Debug.LogError($"Error GetProfileData: {e.Message}");
                    return null;
                }
            }
            else return null;
        }
    }

    [System.Serializable]
    internal class ProfileData
    {
        public string id, name, phone, username, createdAt, updatedAt;
        public List<string> playerIdentifier;
    }

    [System.Serializable]
    internal class ProfileResponse
    {
        public ProfileData data;
        public string message;
        public int statusCode;
    }

}