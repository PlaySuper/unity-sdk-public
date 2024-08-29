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
        private string testApiKey, testCoinId, testToken;

        private PlaySuperUnitySDK ps;

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            Environment.SetEnvironmentVariable("PROJECT_ENV", "development");

            var envVars = GetEnvironmentVariables();
            string testApiKey = envVars.ContainsKey("TEST_API_KEY") ? envVars["TEST_API_KEY"] : null;
            string testCoinId = envVars.ContainsKey("TEST_COIN_ID") ? envVars["TEST_COIN_ID"] : null;
            string testToken = envVars.ContainsKey("TEST_TOKEN") ? envVars["TEST_TOKEN"] : null;
        }

        [SetUp]
        public void Setup()
        {
            Debug.Log("testApiKey: " + testApiKey);
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
            yield return new WaitForSeconds(1);

            LogAssert.Expect(LogType.Log, "Response received successfully:");
        }

        private static Dictionary<string, string> GetEnvironmentVariables()
        {
            Dictionary<string, string> envVariables = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();
            Debug.Log("args: " + args[0] + " " + args[1] + " " + args[2]);
            foreach (var arg in args)
            {
                if (arg.StartsWith("-e "))
                {
                    string[] splitArg = arg.Substring(3).Split('=');
                    if (splitArg.Length == 2)
                    {
                        envVariables[splitArg[0]] = splitArg[1];
                    }
                }
            }

            Debug.Log("envVariables: " + envVariables["TEST_API_KEY"] + " " + envVariables["TEST_COIN_ID"] + " " + envVariables["TEST_TOKEN"]);

            return envVariables;
        }
    }
}