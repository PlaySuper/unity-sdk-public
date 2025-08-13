using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;

namespace PlaySuperUnity.Tests
{
    public class FeatureFlagsTests
    {
        private IFeatureFlags featureFlags;

        [SetUp]
        public void Setup()
        {
            featureFlags = new PlaySuperUnity.FeatureFlags.FeatureFlagsService();
        }

        [TearDown]
        public void TearDown()
        {
            featureFlags.Dispose();
        }

        [Test]
        public async Task GetRequestTimeout_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            double timeout = featureFlags.GetRequestTimeout();
            Assert.AreEqual(10.0, timeout);
        }

        [Test]
        public async Task GetSdkConfig_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            string config = featureFlags.GetSdkConfig();
            Assert.AreEqual("{}", config);
        }

        [Test]
        public void GetSdkConfig_ShouldValidateJsonFormat()
        {
            // Test valid JSON
            var validJson = "{\"key\": \"value\"}";
            Assert.IsTrue(featureFlags.GetType()
                .GetMethod("IsValidJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { validJson }) as bool?);

            // Test invalid JSON
            var invalidJson = "{key: invalid}";
            Assert.IsFalse(featureFlags.GetType()
                .GetMethod("IsValidJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { invalidJson }) as bool?);
        }

        [Test]
        public void GetRequestTimeout_ShouldHandleInvalidValues()
        {
            // Create a feature with invalid number
            var feature = new Feature { defaultValue = "not_a_number" };

            // Use reflection to access private method
            var result = featureFlags.GetType()
                .GetMethod("EvaluateFeature", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .MakeGenericMethod(typeof(double))
                .Invoke(featureFlags, new object[] {
                    "test_key",
                    10.0,
                    new Features { sdk_request_timeout_seconds = feature },
                    new System.Func<string, double>(double.Parse),
                    "Number"
                });

            Assert.AreEqual(10.0, result);
        }

        [Test]
        public void EvaluateConditionValue_ShouldHandleInOperator()
        {
            var condition = new ConditionValue { inArray = new string[] { "id1", "id2", "id3" } };

            // Use reflection to access private method
            var method = featureFlags.GetType().GetMethod("EvaluateConditionValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Test matching case
            var resultMatch = (bool)method.Invoke(featureFlags, new object[] { condition, "id2" });
            Assert.IsTrue(resultMatch, "Should match when value is in array");

            // Test non-matching case
            var resultNoMatch = (bool)method.Invoke(featureFlags, new object[] { condition, "id4" });
            Assert.IsFalse(resultNoMatch, "Should not match when value is not in array");
        }

        [Test]
        public void EvaluateConditionValue_ShouldHandleNumericComparisons()
        {
            // Test greater than
            var gtCondition = new ConditionValue { greaterThan = "100" };
            var method = featureFlags.GetType().GetMethod("EvaluateConditionValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { gtCondition, "150" }), "$gt should be true for 150 > 100");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { gtCondition, "50" }), "$gt should be false for 50 > 100");

            // Test less than
            var ltCondition = new ConditionValue { lessThan = "100" };
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { ltCondition, "50" }), "$lt should be true for 50 < 100");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { ltCondition, "150" }), "$lt should be false for 150 < 100");

            // Test greater than or equal
            var gteCondition = new ConditionValue { greaterThanEqual = "100" };
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { gteCondition, "100" }), "$gte should be true for 100 >= 100");
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { gteCondition, "150" }), "$gte should be true for 150 >= 100");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { gteCondition, "50" }), "$gte should be false for 50 >= 100");

            // Test less than or equal
            var lteCondition = new ConditionValue { lessThanEqual = "100" };
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { lteCondition, "100" }), "$lte should be true for 100 <= 100");
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { lteCondition, "50" }), "$lte should be true for 50 <= 100");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { lteCondition, "150" }), "$lte should be false for 150 <= 100");
        }

        [Test]
        public void EvaluateConditionValue_ShouldHandleNotEqual()
        {
            var condition = new ConditionValue { notEqual = "test_value" };
            var method = featureFlags.GetType().GetMethod("EvaluateConditionValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { condition, "other_value" }), "$ne should be true for different values");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { condition, "test_value" }), "$ne should be false for equal values");
        }

        [Test]
        public void EvaluateConditionValue_ShouldHandleExists()
        {
            var existsTrue = new ConditionValue { exists = "true" };
            var existsFalse = new ConditionValue { exists = "false" };
            var method = featureFlags.GetType().GetMethod("EvaluateConditionValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Test exists = true
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { existsTrue, "some_value" }), "$exists true should be true for non-empty value");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { existsTrue, "" }), "$exists true should be false for empty value");

            // Test exists = false
            Assert.IsTrue((bool)method.Invoke(featureFlags, new object[] { existsFalse, "" }), "$exists false should be true for empty value");
            Assert.IsFalse((bool)method.Invoke(featureFlags, new object[] { existsFalse, "some_value" }), "$exists false should be false for non-empty value");
        }
    }
}
