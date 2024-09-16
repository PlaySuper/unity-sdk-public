using NUnit.Framework;
using PlaySuperUnity;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using System;


namespace PlaySuperUnity.Tests
{
    public class PlaySuperUnitySDKTests
    {
        private const string TEST_API_KEY_ENV = "TEST_API_KEY", TEST_COIN_ID_ENV = "TEST_COIN_ID", TEST_TOKEN_ENV = "TEST_TOKEN";
        private string testApiKey, testCoinId, testToken;

        private PlaySuperUnitySDK ps;

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            Environment.SetEnvironmentVariable("PROJECT_ENV", "development");

            Dictionary<string, string> envVars = GetEnvironmentVariables();
            testApiKey = envVars.ContainsKey(TEST_API_KEY_ENV) ? envVars[TEST_API_KEY_ENV] : null;
            testCoinId = envVars.ContainsKey(TEST_COIN_ID_ENV) ? envVars[TEST_COIN_ID_ENV] : null;
            testToken = envVars.ContainsKey(TEST_TOKEN_ENV) ? envVars[TEST_TOKEN_ENV] : null;
        }

        [SetUp]
        public void Setup()
        {
            PlaySuperUnitySDK.Initialize(testApiKey);
            ps = PlaySuperUnitySDK.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            if (ps != null && ps.gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(ps.gameObject);
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
            yield return new WaitForSeconds(2);

            LogAssert.Expect(LogType.Log, "Response received successfully:");
        }

        private static Dictionary<string, string> GetEnvironmentVariables()
        {
            Dictionary<string, string> envVariables = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-e" && i + 1 < args.Length)
                {
                    string secretString = args[i + 1];
                    string[] pair = secretString.Split('=');
                    envVariables[pair[0]] = pair[1];
                }
            }

            return envVariables;
        }
    }
}