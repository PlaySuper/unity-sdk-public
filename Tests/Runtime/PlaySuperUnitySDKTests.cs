using NUnit.Framework;
using UnityEngine;
using PlaySuperUnity;

namespace PlaySuperUnity.Tests
{
    public class PlaySuperUnitySDKTests
    {
        private const string testApiKey1 = "test-api-key";

        [Test]
        public void Initialize_CreatesSingletonInstance()
        {
            PlaySuperUnitySDK.Initialize(testApiKey1);
            Assert.IsNotNull(PlaySuperUnitySDK.Instance);
            Assert.AreEqual("PlaySuper", PlaySuperUnitySDK.Instance.gameObject.name);
        }

    }
}