using NUnit.Framework;
using System.Threading.Tasks;
using PlaySuperUnity.FeatureFlags;
using System.Collections.Generic;

namespace PlaySuperUnity.Tests
{
    public class FeatureFlagsIntegrationTests
    {
        [Test]
        public void FeatureFlagsIntegration_FullFlowTest()
        {
            // This test simulates a complete flow through the feature flags system
            // 1. Create a feature definition with rules
            // 2. Create an evaluator with game data
            // 3. Evaluate the feature

            // Create mock game data
            var gameData = new GameData
            {
                id = "test-game-id",
                name = "Test Game",
                studioId = "test-studio-id"
            };

            // Create a feature definition with rules
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "default_value",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "test-rule",
                        ForceValue = "forced_value",
                        Condition = new ParsedCondition
                        {
                            Attributes = new Dictionary<string, ConditionOperator>
                            {
                                { "gamename", new ConditionOperator { SimpleValue = "Test Game" } }
                            }
                        }
                    }
                }
            };

            // Create evaluator
            var evaluator = new FeatureRuleEvaluator(gameData);

            // Evaluate feature
            var result = evaluator.EvaluateFeature("test_feature", "fallback_value", featureDefinition, s => s);

            // Should return the forced value from the matching rule
            Assert.AreEqual("forced_value", result);
        }

        [Test]
        public void FeatureFlagsIntegration_CacheIntegrationTest()
        {
            // Test the integration between FeatureFlagsService and FeatureFlagsCache
            var cache = new FeatureFlagsCache();

            // Test caching a value
            cache.UpdateCache("test_feature", "cached_value");

            // Retrieve the cached value
            var result = cache.GetCachedValue("test_feature", "default_value", s => s);

            Assert.AreEqual("cached_value", result);
        }

        [Test]
        public void FeatureFlagsIntegration_ParserIntegrationTest()
        {
            // Test that the parser can handle a complete JSON response
            var parser = new GrowthBookResponseParser();

            var json = @"{
                ""status"": ""success"",
                ""features"": {
                    ""test_feature"": {
                        ""defaultValue"": ""default_value"",
                        ""rules"": [
                            {
                                ""id"": ""rule1"",
                                ""force"": ""forced_value"",
                                ""condition"": {
                                    ""gamename"": {
                                        ""value"": ""Test Game""
                                    }
                                }
                            }
                        ]
                    }
                }
            }";

            var parsedFeatures = parser.ParseResponse(json);

            Assert.IsNotNull(parsedFeatures);
            Assert.IsTrue(parsedFeatures.Features.ContainsKey("test_feature"));

            var feature = parsedFeatures.Features["test_feature"];
            Assert.AreEqual("default_value", feature.DefaultValue);
            Assert.AreEqual(1, feature.Rules.Count);

            var rule = feature.Rules[0];
            Assert.AreEqual("rule1", rule.Id);
            Assert.AreEqual("forced_value", rule.ForceValue);
            Assert.IsNotNull(rule.Condition);
        }

        [Test]
        public void FeatureFlagsIntegration_EvaluatorWithComplexConditions()
        {
            // Test evaluator with complex conditions
            var gameData = new GameData
            {
                id = "150",
                name = "Test Game",
                studioId = "test-studio-id"
            };

            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "default_value",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "complex-rule",
                        ForceValue = "matched_value",
                        Condition = new ParsedCondition
                        {
                            Attributes = new Dictionary<string, ConditionOperator>
                            {
                                { "gamename", new ConditionOperator { SimpleValue = "Test Game" } },
                                { "gameid", new ConditionOperator { GreaterThan = "100" } }
                            }
                        }
                    }
                }
            };

            var evaluator = new FeatureRuleEvaluator(gameData);
            var result = evaluator.EvaluateFeature("complex_feature", "fallback_value", featureDefinition, s => s);

            Assert.AreEqual("matched_value", result);
        }

        [Test]
        public void FeatureFlagsIntegration_StaticWrapperTest()
        {
            // Test that the static wrapper can be instantiated
            // Note: We can't fully test initialization without mocking dependencies
            Assert.DoesNotThrow(() =>
            {
                var instanceField = typeof(FeatureFlags).GetField("instance",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                instanceField.SetValue(null, null);
            });

            // Test that static methods return default values when not initialized
            Assert.AreEqual(Constants.MIXPANEL_URL, FeatureFlags.GetEventSingleUrl());
            Assert.AreEqual(Constants.MIXPANEL_URL_BATCH, FeatureFlags.GetEventBatchUrl());
            Assert.IsTrue(FeatureFlags.IsAdIdEnabled());
            Assert.AreEqual(Constants.PS_ANALYTICS_URL, FeatureFlags.GetPSAnalyticsUrl());
            Assert.AreEqual(42.0, FeatureFlags.GetNumberFeature("test", 42.0));
            Assert.AreEqual("{}", FeatureFlags.GetJsonFeature("test", "{}"));
        }
    }
}