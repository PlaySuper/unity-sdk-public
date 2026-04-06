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
            string token = PlaySuperUnitySDK.GetAuthToken();
            string baseUrl = PlaySuperUnitySDK.GetBaseUrl();
            string apiKey = PlaySuperUnitySDK.GetApiKey();
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

    /// <summary>
    /// Extended player profile data with additional demographics/contact fields
    /// </summary>
    [System.Serializable]
    public class PlayerProfileData
    {
        public string playerId;
        public string firstName;
        public string lastName;
        public string gender; // "MALE", "FEMALE", "OTHER"
        public string dateOfBirth; // ISO 8601 date string
        public string email;
        public string phoneNumber;
        public string profilePicUrl;
        public string createdAt;
        public string updatedAt;
    }

    /// <summary>
    /// Response from PATCH /player/gcommerce/profile
    /// </summary>
    [System.Serializable]
    internal class PlayerProfileResponse
    {
        public PlayerProfileData data;
        public string message;
        public int statusCode;
    }

    /// <summary>
    /// Request body for updating player profile
    /// </summary>
    [System.Serializable]
    internal class UpdatePlayerProfileRequest
    {
        public string firstName;
        public string lastName;
        public string gender;
        public string dateOfBirth;
        public string email;
        public string phoneNumber;

        // Constructor to handle optional fields - only include non-null fields in JSON
        public UpdatePlayerProfileRequest(
            string firstName = null,
            string lastName = null,
            string gender = null,
            string dateOfBirth = null,
            string email = null,
            string phoneNumber = null)
        {
            this.firstName = firstName;
            this.lastName = lastName;
            this.gender = gender;
            this.dateOfBirth = dateOfBirth;
            this.email = email;
            this.phoneNumber = phoneNumber;
        }
    }

}