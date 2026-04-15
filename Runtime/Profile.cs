using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
                    using (var webRequest = UnityWebRequest.Get($"{baseUrl}/player/profile"))
                    {
                        webRequest.SetRequestHeader("x-api-key", apiKey);
                        webRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                        var operation = webRequest.SendWebRequest();
                        while (!operation.isDone)
                            await Task.Yield();
                        if (webRequest.result != UnityWebRequest.Result.Success)
                            throw new System.Exception($"HTTP {webRequest.responseCode}: {webRequest.error}");
                        string profileJson = webRequest.downloadHandler.text;
                        ProfileResponse profileData = JsonUtility.FromJson<ProfileResponse>(profileJson);
                        return profileData.data;
                    }
                }
                catch (System.Exception e)
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