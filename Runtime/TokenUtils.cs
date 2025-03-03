using System;
using System.Text;
using UnityEngine;

namespace PlaySuperUnity
{
    /// <summary>
    /// Utility class for JWT token operations and validation
    /// </summary>
    public static class TokenUtils
    {
        /// <summary>
        /// Validates a JWT token format and expiration
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>True if the token is valid, otherwise throws an exception</returns>
        public static bool ValidateToken(string token)
        {
            try
            {
                CheckTokenFormat(token);
                string payload = DecodeJwtPayload(token);
                ValidateExpiration(payload);
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Checks if the token has valid JWT format (three parts separated by dots)
        /// </summary>
        /// <param name="token">The token to check</param>
        /// <returns>True if format is valid, otherwise throws an exception</returns>
        public static bool CheckTokenFormat(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token is null or empty");
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                throw new ArgumentException("Invalid JWT format. Token must contain 3 parts.");
            }
            return true;
        }

        /// <summary>
        /// Decodes the JWT payload (middle part)
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>Decoded payload as JSON string</returns>
        public static string DecodeJwtPayload(string token)
        {
            try
            {
                string payload = token.Split('.')[1];
                int padding = 4 - (payload.Length % 4);
                if (padding < 4)
                {
                    payload += new string('=', padding);
                }
                payload = payload.Replace('-', '+').Replace('_', '/');
                byte[] bytes = Convert.FromBase64String(payload);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decode JWT payload: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates if the token has not expired
        /// </summary>
        /// <param name="jsonPayload">The decoded JSON payload</param>
        /// <returns>True if token is not expired, otherwise throws an exception</returns>
        public static bool ValidateExpiration(string jsonPayload)
        {
            int expIndex = jsonPayload.IndexOf("\"exp\":");
            if (expIndex < 0)
            {
                throw new InvalidOperationException("No expiration claim found in token");
            }

            int valueStart = jsonPayload.IndexOf(':', expIndex) + 1;
            int valueEnd = jsonPayload.IndexOf(',', valueStart);
            if (valueEnd < 0) valueEnd = jsonPayload.IndexOf('}', valueStart);

            string expValue = jsonPayload.Substring(valueStart, valueEnd - valueStart).Trim();
            if (!long.TryParse(expValue, out long exp))
            {
                throw new InvalidOperationException("Invalid expiration time format");
            }

            long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (nowSeconds >= exp)
            {
                throw new InvalidOperationException("Token has expired");
            }

            return true;
        }

        /// <summary>
        /// Extracts a specific claim from the token payload
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <param name="claimName">The name of the claim to extract</param>
        /// <returns>The value of the claim as string, or null if not found</returns>
        public static string GetClaim(string token, string claimName)
        {
            try
            {
                string payload = DecodeJwtPayload(token);
                string claimKey = $"\"{claimName}\":";
                int claimIndex = payload.IndexOf(claimKey);

                if (claimIndex < 0)
                {
                    return null;
                }

                int valueStart = payload.IndexOf(':', claimIndex) + 1;
                int valueEnd;

                // Check if the value is a string (starts with quote)
                if (payload[valueStart] == '"')
                {
                    valueStart++; // Skip the opening quote
                    valueEnd = payload.IndexOf('"', valueStart);
                }
                else
                {
                    // For non-string values (numbers, booleans)
                    valueEnd = payload.IndexOf(',', valueStart);
                    if (valueEnd < 0) valueEnd = payload.IndexOf('}', valueStart);
                }

                return payload.Substring(valueStart, valueEnd - valueStart).Trim();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting claim {claimName}: {ex.Message}");
                return null;
            }
        }
    }
}