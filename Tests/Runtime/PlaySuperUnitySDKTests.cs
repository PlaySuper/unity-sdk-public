using NUnit.Framework;
using UnityEngine;
using PlaySuperUnity;

namespace PlaySuperUnity.Tests
{
    public class PlaySuperUnitySDKTests
    {
        [Test]
        public void Initialize_CreatesSingletonInstance()
        {
            // Arrange
            string testApiKey = "test-api-key";

            // Act
            PlaySuperUnitySDK.Initialize(testApiKey);

            // Assert
            Assert.IsNotNull(PlaySuperUnitySDK.Instance);
        }
    }
}