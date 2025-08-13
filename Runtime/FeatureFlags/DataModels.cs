using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Data models for feature flags
    /// </summary>

    [Serializable]
    internal class ParsedFeatures
    {
        public Dictionary<string, FeatureDefinition> Features { get; set; } = new Dictionary<string, FeatureDefinition>();
    }

    [Serializable]
    internal class FeatureDefinition
    {
        public string DefaultValue { get; set; }
        public List<FeatureRule> Rules { get; set; } = new List<FeatureRule>();
    }

    [Serializable]
    internal class FeatureRule
    {
        public string Id { get; set; }
        public string ForceValue { get; set; }
        public ParsedCondition Condition { get; set; }
    }

    [Serializable]
    internal class ParsedCondition
    {
        public Dictionary<string, ConditionOperator> Attributes { get; set; } = new Dictionary<string, ConditionOperator>();
    }

    [Serializable]
    internal class ConditionOperator
    {
        public string SimpleValue { get; set; }           // "Test Game"
        public string[] InArray { get; set; }             // {"$in": ["a", "b"]}
        public string NotEqual { get; set; }              // {"$ne": "value"}
        public string GreaterThan { get; set; }           // {"$gt": "10"}
        public string LessThan { get; set; }              // {"$lt": "10"}
        public string GreaterThanEqual { get; set; }      // {"$gte": "10"}
        public string LessThanEqual { get; set; }         // {"$lte": "10"}
        public string Exists { get; set; }                // {"$exists": "true"}
    }

    [Serializable]
    internal class CachedFeature
    {
        public string Value { get; set; }
        public DateTime CachedAt { get; set; }
        public bool IsValid => DateTime.UtcNow - CachedAt < TimeSpan.FromMinutes(5);
    }
}