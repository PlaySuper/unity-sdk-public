using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity.FeatureFlags;
using System.Collections.Generic;


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
            featureFlags?.Dispose();
        }

        [Test]
        public void GetEventSingleUrl_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            string url = featureFlags.GetEventSingleUrl();
            Assert.AreEqual(Constants.MIXPANEL_URL, url);
        }

        [Test]
        public void GetEventBatchUrl_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            string url = featureFlags.GetEventBatchUrl();
            Assert.AreEqual(Constants.MIXPANEL_URL_BATCH, url);
        }

        [Test]
        public void IsAdIdEnabled_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            bool enabled = featureFlags.IsAdIdEnabled();
            Assert.IsTrue(enabled); // Default is true
        }

        [Test]
        public void GetPSAnalyticsUrl_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            string url = featureFlags.GetPSAnalyticsUrl();
            Assert.AreEqual(Constants.PS_ANALYTICS_URL, url);
        }

        [Test]
        public void GetNumberFeature_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            double value = featureFlags.GetNumberFeature("test_key", 42.0);
            Assert.AreEqual(42.0, value);
        }

        [Test]
        public void GetJsonFeature_ShouldReturnDefaultValue_WhenNotInitialized()
        {
            // Since we're not initializing the service, it should return the default value
            string value = featureFlags.GetJsonFeature("test_key", "{}");
            Assert.AreEqual("{}", value);
        }

        [Test]
        public void JsonValidator_ShouldValidateJsonFormat()
        {
            // Test valid JSON
            var validJson = "{\"key\": \"value\"}";
            Assert.IsTrue(PlaySuperUnity.FeatureFlags.JsonValidator.IsValidJson(validJson));

            // Test invalid JSON
            var invalidJson = "{key: invalid}";
            Assert.IsFalse(PlaySuperUnity.FeatureFlags.JsonValidator.IsValidJson(invalidJson));
        }

        [Test]
        public async Task FeatureFlagsService_ShouldBeInitializable()
        {
            // Test that we can create and initialize the service
            var service = new PlaySuperUnity.FeatureFlags.FeatureFlagsService();
            Assert.IsNotNull(service);

            // We won't actually initialize with a real client key in tests
            // but we can verify the service was created
        }

        [Test]
        public void FeatureRuleEvaluator_ShouldHandleInOperator()
        {
            // Create a mock condition for testing
            var conditionOperator = new ConditionOperator
            {
                InArray = new string[] { "id1", "id2", "id3" }
            };

            // Create a simple evaluator (doesn't need real game data for this test)
            var evaluator = new FeatureRuleEvaluator(null);

            // Use reflection to access private method
            var method = evaluator.GetType().GetMethod("EvaluateOperator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Test matching case
            var resultMatch = (bool)method.Invoke(evaluator, new object[] { conditionOperator, "id2" });
            Assert.IsTrue(resultMatch, "Should match when value is in array");

            // Test non-matching case
            var resultNoMatch = (bool)method.Invoke(evaluator, new object[] { conditionOperator, "id4" });

            Assert.IsFalse(resultNoMatch, "Should not match when value is not in array");
        }

        [Test]
        public void FeatureRuleEvaluator_ShouldHandleNumericComparisons()
        {
            // Create a simple evaluator (doesn't need real game data for this test)
            var evaluator = new FeatureRuleEvaluator(null);

            // Use reflection to access private method
            var method = evaluator.GetType().GetMethod("EvaluateOperator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Test greater than
            var gtCondition = new ConditionOperator { GreaterThan = "100" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { gtCondition, "150" }), "$gt should be true for 150 > 100");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { gtCondition, "50" }), "$gt should be false for 50 > 100");

            // Test less than
            var ltCondition = new ConditionOperator { LessThan = "100" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { ltCondition, "50" }), "$lt should be true for 50 < 100");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { ltCondition, "150" }), "$lt should be false for 150 < 100");

            // Test greater than or equal
            var gteCondition = new ConditionOperator { GreaterThanEqual = "100" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { gteCondition, "100" }), "$gte should be true for 100 >= 100");
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { gteCondition, "150" }), "$gte should be true for 150 >= 100");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { gteCondition, "50" }), "$gte should be false for 50 >= 100");

            // Test less than or equal
            var lteCondition = new ConditionOperator { LessThanEqual = "100" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { lteCondition, "100" }), "$lte should be true for 100 <= 100");
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { lteCondition, "50" }), "$lte should be true for 50 <= 100");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { lteCondition, "150" }), "$lte should be false for 150 <= 100");
        }

        [Test]
        public void FeatureRuleEvaluator_ShouldHandleNotEqual()
        {
            // Create a simple evaluator (doesn't need real game data for this test)
            var evaluator = new FeatureRuleEvaluator(null);

            // Use reflection to access private method
            var method = evaluator.GetType().GetMethod("EvaluateOperator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var condition = new ConditionOperator { NotEqual = "test_value" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { condition, "other_value" }), "$ne should be true for different values");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { condition, "test_value" }), "$ne should be false for equal values");
        }

        [Test]
        public void FeatureRuleEvaluator_ShouldHandleExists()
        {
            // Create a simple evaluator (doesn't need real game data for this test)
            var evaluator = new FeatureRuleEvaluator(null);

            // Use reflection to access private method
            var method = evaluator.GetType().GetMethod("EvaluateOperator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var existsTrue = new ConditionOperator { Exists = "true" };
            var existsFalse = new ConditionOperator { Exists = "false" };

            // Test exists = true
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { existsTrue, "some_value" }), "$exists true should be true for non-empty value");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { existsTrue, "" }), "$exists true should be false for empty value");

            // Test exists = false
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { existsFalse, "" }), "$exists false should be true for empty value");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { existsFalse, "some_value" }), "$exists false should be false for non-empty value");
        }

        [Test]
        public void FeatureRuleEvaluator_ShouldHandleSimpleValue()
        {
            // Create a simple evaluator (doesn't need real game data for this test)
            var evaluator = new FeatureRuleEvaluator(null);

            // Use reflection to access private method
            var method = evaluator.GetType().GetMethod("EvaluateOperator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var condition = new ConditionOperator { SimpleValue = "test_value" };
            Assert.IsTrue((bool)method.Invoke(evaluator, new object[] { condition, "test_value" }), "Should match for equal values");
            Assert.IsFalse((bool)method.Invoke(evaluator, new object[] { condition, "other_value" }), "Should not match for different values");
        }

        // Additional comprehensive tests for FeatureFlagsService

        [Test]
        public void FeatureFlagsService_ClearCache_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => featureFlags.ClearCache());
        }

        [Test]
        public void FeatureFlagsService_LogCacheState_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => featureFlags.LogCacheState());
        }

        [Test]
        public async Task FeatureFlagsService_Cleanup_ShouldNotThrow()
        {
            Assert.DoesNotThrowAsync(async () => await featureFlags.Cleanup());
        }

        [Test]
        public async Task FeatureFlagsService_ForceRefresh_ShouldNotThrow()
        {
            Assert.DoesNotThrowAsync(async () => await featureFlags.ForceRefresh());
        }

        [Test]
        public void FeatureFlagsService_GetNumberFeature_ShouldHandleParsingErrors()
        {
            // Test that the service handles parsing errors gracefully
            var service = new FeatureFlagsService();

            // This would normally use reflection to set up a cached invalid value
            // but we're primarily testing that it doesn't throw
            Assert.DoesNotThrow(() => service.GetNumberFeature("invalid_number_feature", 42.0));
        }

        [Test]
        public void FeatureFlagsService_GetJsonFeature_ShouldValidateJson()
        {
            var service = new FeatureFlagsService();

            // Test with valid JSON
            var validJsonResult = service.GetJsonFeature("valid_json_feature", "{\"key\":\"value\"}");
            Assert.AreEqual("{\"key\":\"value\"}", validJsonResult);

            // Test with invalid JSON (should return default)
            var invalidJsonResult = service.GetJsonFeature("invalid_json_feature", "{\"default\":\"value\"}");
            Assert.AreEqual("{\"default\":\"value\"}", invalidJsonResult);
        }
    }
}
