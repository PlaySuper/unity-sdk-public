using NUnit.Framework;
using System.Collections.Generic;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    public class DataModelsTests
    {
        [Test]
        public void ParsedFeatures_ShouldInitializeWithEmptyDictionary()
        {
            var parsedFeatures = new ParsedFeatures();
            Assert.IsNotNull(parsedFeatures.Features);
            Assert.IsEmpty(parsedFeatures.Features);
        }

        [Test]
        public void FeatureDefinition_ShouldInitializeWithEmptyRulesList()
        {
            var featureDefinition = new FeatureDefinition();
            Assert.IsNotNull(featureDefinition.Rules);
            Assert.IsEmpty(featureDefinition.Rules);
            Assert.IsNull(featureDefinition.DefaultValue);
        }

        [Test]
        public void FeatureRule_ShouldInitializeCorrectly()
        {
            var featureRule = new FeatureRule();
            Assert.IsNull(featureRule.Id);
            Assert.IsNull(featureRule.ForceValue);
            Assert.IsNull(featureRule.Condition);
        }

        [Test]
        public void ParsedCondition_ShouldInitializeWithEmptyAttributes()
        {
            var parsedCondition = new ParsedCondition();
            Assert.IsNotNull(parsedCondition.Attributes);
            Assert.IsEmpty(parsedCondition.Attributes);
        }

        [Test]
        public void ConditionOperator_ShouldInitializeWithNullValues()
        {
            var conditionOperator = new ConditionOperator();
            Assert.IsNull(conditionOperator.SimpleValue);
            Assert.IsNull(conditionOperator.InArray);
            Assert.IsNull(conditionOperator.NotEqual);
            Assert.IsNull(conditionOperator.GreaterThan);
            Assert.IsNull(conditionOperator.LessThan);
            Assert.IsNull(conditionOperator.GreaterThanEqual);
            Assert.IsNull(conditionOperator.LessThanEqual);
            Assert.IsNull(conditionOperator.Exists);
        }

        [Test]
        public void CachedFeature_ShouldInitializeWithDefaultValues()
        {
            var cachedFeature = new CachedFeature();
            Assert.IsNull(cachedFeature.Value);
            Assert.AreEqual(default(DateTime), cachedFeature.CachedAt);
        }

        [Test]
        public void CachedFeature_IsValid_ShouldReturnFalseForExpiredCache()
        {
            var cachedFeature = new CachedFeature
            {
                Value = "test",
                CachedAt = DateTime.UtcNow.AddMinutes(-6) // 6 minutes ago, expired
            };

            Assert.IsFalse(cachedFeature.IsValid);
        }

        [Test]
        public void CachedFeature_IsValid_ShouldReturnTrueForValidCache()
        {
            var cachedFeature = new CachedFeature
            {
                Value = "test",
                CachedAt = DateTime.UtcNow.AddMinutes(-4) // 4 minutes ago, still valid
            };

            Assert.IsTrue(cachedFeature.IsValid);
        }
    }
}