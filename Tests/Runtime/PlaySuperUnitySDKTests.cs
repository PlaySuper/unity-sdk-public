using NUnit.Framework;
using PlaySuperUnity;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;


namespace PlaySuperUnity.Tests
{
    public class PlaySuperUnitySDKTests
    {
        private string testApiKey;
        private string testCoinId;

        private string testToken;

        private PlaySuperUnitySDK ps;

        [SetUp]
        public void Setup()
        {
            testApiKey = System.Environment.GetEnvironmentVariable("TEST_API_KEY");
            testCoinId = System.Environment.GetEnvironmentVariable("TEST_COIN_ID");
            testToken = System.Environment.GetEnvironmentVariable("TEST_TOKEN");
            PlaySuperUnitySDK.Initialize(testApiKey);
            ps = PlaySuperUnitySDK.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            if (ps != null && ps.gameObject != null)
            {
                Object.DestroyImmediate(ps.gameObject);
            }
        }

        [Test]
        public void Initialize_CreatesSingletonInstance()
        {
            Assert.IsNotNull(ps);
            Assert.AreEqual("PlaySuper", ps.gameObject.name);
        }

        [UnityTest]
        public IEnumerator DistributeCoins_ShouldStoreTransaction_WhenAuthTokenIsNull()
        {
            ps.OnTokenReceive(null);
            yield return ps.DistributeCoins(testCoinId, 10);

            string storedTransaction = PlayerPrefs.GetString("transactions");
            Assert.IsNotEmpty(storedTransaction);
        }

        [UnityTest]
        public IEnumerator DistributeCoins_ShouldSendRequest_AndReceiveSuccess()
        {
            ps.OnTokenReceive(testToken);

            yield return ps.DistributeCoins(testCoinId, 10);
            yield return new WaitForSeconds(1);

            LogAssert.Expect(LogType.Log, "Response received successfully:");
        }
    }
}