using System;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Validates JSON strings
    /// </summary>
    internal static class JsonValidator
    {
        /// <summary>
        /// Validates if a string is valid JSON
        /// </summary>
        /// <param name="strInput">String to validate</param>
        /// <returns>True if valid JSON</returns>
        public static bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput)) return false;

            try
            {
                // Simple validation - try to parse as JSON
                // Create a wrapper object to test if the input is valid JSON
                string prefix = "{";
                string dataKey = "\"data\":";
                string suffix = "}";

                string wrapper = prefix + dataKey + strInput + suffix;

                var testObject = JsonUtility.FromJson<JsonTestWrapper>(wrapper);
                return testObject != null && !string.IsNullOrEmpty(testObject.data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlaySuper][FeatureFlags] JSON validation failed: " + ex.Message);
                return false;
            }
        }

        [Serializable]
        private class JsonTestWrapper
        {
            public string data;
        }
    }
}