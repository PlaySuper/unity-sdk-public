using NUnit.Framework;
using System.Collections.Generic;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class FeatureRuleEvaluatorTests
    {
        private GameData mockGameData;
        private FeatureRuleEvaluator evaluator;

        [SetUp]
        public void Setup()
        {
            // Create mock game data for testing
            mockGameData = new GameData
            {
                id = "test-game-id",
                name = "Test Game",
                studioId = "test-studio-id",
                platform = new string[] { "iOS", "Android" },
                studio = new Studio
                {
                    organizationId = "test-org-id",
                    organization = new Organization
                    {
                        handle = "test-org-handle"
                    }
                }
            };

            evaluator = new FeatureRuleEvaluator(mockGameData);
        }

        [Test]
        public void EvaluateFeature_ShouldReturnDefaultValue_WhenFeatureDefinitionIsNull()
        {
            var result = evaluator.EvaluateFeature("test-feature", "default", null, s => s);
            Assert.AreEqual("default", result);
        }

        [Test]
        public void EvaluateFeature_ShouldReturnDefaultValue_WhenNoRulesMatch()
        {
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "default-value",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "never-matching-rule",
                        ForceValue = "forced-value",
                        Condition = new ParsedCondition
                        {
                            Attributes = new Dictionary<string, ConditionOperator>
                            {
                                { "gamename", new ConditionOperator { SimpleValue = "Non-matching Game" } }
                            }
                        }
                    }
                }
            };

            var result = evaluator.EvaluateFeature("test-feature", "fallback-default", featureDefinition, s => s);
            Assert.AreEqual("default-value", result);
        }

        [Test]
        public void EvaluateFeature_ShouldReturnRuleValue_WhenRuleMatches()
        {
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "default-value",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "matching-rule",
                        ForceValue = "forced-value",
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

            var result = evaluator.EvaluateFeature("test-feature", "fallback-default", featureDefinition, s => s);
            Assert.AreEqual("forced-value", result);
        }

        [Test]
        public void EvaluateFeature_ShouldReturnDefaultValue_WhenRuleHasNoForceValue()
        {
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "default-value",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "matching-rule",
                        ForceValue = null, // No force value
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

            var result = evaluator.EvaluateFeature("test-feature", "fallback-default", featureDefinition, s => s);
            Assert.AreEqual("default-value", result);
        }

        [Test]
        public void EvaluateFeature_ShouldHandleNumericValues()
        {
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "42.5",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "matching-rule",
                        ForceValue = "99.9",
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

            var result = evaluator.EvaluateFeature("test-feature", 10.0, featureDefinition, double.Parse);
            Assert.AreEqual(99.9, result);
        }

        [Test]
        public void EvaluateFeature_ShouldHandleBooleanValues()
        {
            var featureDefinition = new FeatureDefinition
            {
                DefaultValue = "false",
                Rules = new List<FeatureRule>
                {
                    new FeatureRule
                    {
                        Id = "matching-rule",
                        ForceValue = "true",
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

            var result = evaluator.EvaluateFeature("test-feature", false, featureDefinition, bool.Parse);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldReturnTrue_WhenNoConditionSpecified()
        {
            var result = evaluator.EvaluateCondition(null);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldReturnFalse_WhenNoGameDataAvailable()
        {
            var evaluatorWithoutData = new FeatureRuleEvaluator(null);
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { SimpleValue = "Test Game" } }
                }
            };

            var result = evaluatorWithoutData.EvaluateCondition(condition);
            Assert.IsFalse(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleSimpleValueMatch()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { SimpleValue = "Test Game" } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleSimpleValueNoMatch()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { SimpleValue = "Wrong Game" } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsFalse(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleInOperatorMatch()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gameid", new ConditionOperator { InArray = new string[] { "other-id", "test-game-id", "another-id" } } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleInOperatorNoMatch()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gameid", new ConditionOperator { InArray = new string[] { "other-id", "another-id" } } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsFalse(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleNotEqualOperator()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { NotEqual = "Wrong Game" } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleExistsOperator()
        {
            // Test exists = true with existing value
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { Exists = "true" } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsTrue(result);

            // Test exists = false with non-existing value (using unknown attribute)
            var condition2 = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "unknown_attr", new ConditionOperator { Exists = "false" } }
                }
            };

            var result2 = evaluator.EvaluateCondition(condition2);
            Assert.IsTrue(result2);
        }

        [Test]
        public void EvaluateCondition_ShouldHandleNumericComparisons()
        {
            // Create mock game data with numeric values
            var numericGameData = new GameData
            {
                id = "150", // Using ID as a numeric value for testing
                name = "Test Game"
            };

            var numericEvaluator = new FeatureRuleEvaluator(numericGameData);

            // Test greater than
            var gtCondition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gameid", new ConditionOperator { GreaterThan = "100" } }
                }
            };

            Assert.IsTrue(numericEvaluator.EvaluateCondition(gtCondition));

            // Test less than
            var ltCondition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gameid", new ConditionOperator { LessThan = "200" } }
                }
            };

            Assert.IsTrue(numericEvaluator.EvaluateCondition(ltCondition));
        }

        [Test]
        public void EvaluateCondition_ShouldHandleMultipleConditions_AndLogic()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { SimpleValue = "Test Game" } }, // Should match
                    { "gameid", new ConditionOperator { SimpleValue = "test-game-id" } }  // Should match
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvaluateCondition_ShouldFailWhenAnyConditionDoesNotMatch()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "gamename", new ConditionOperator { SimpleValue = "Test Game" } },      // Should match
                    { "gameid", new ConditionOperator { SimpleValue = "wrong-game-id" } }    // Should NOT match
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsFalse(result);
        }

        [Test]
        public void EvaluateOperator_ShouldHandleUnknownAttribute()
        {
            var condition = new ParsedCondition
            {
                Attributes = new Dictionary<string, ConditionOperator>
                {
                    { "unknown_attribute", new ConditionOperator { SimpleValue = "some-value" } }
                }
            };

            var result = evaluator.EvaluateCondition(condition);
            Assert.IsFalse(result);
        }
    }
}