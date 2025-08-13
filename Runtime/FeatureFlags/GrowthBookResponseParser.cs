using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Responsible for parsing GrowthBook response into structured data
    /// </summary>
    internal class GrowthBookResponseParser
    {
        /// <summary>
        /// Parse raw JSON response into structured feature definitions
        /// </summary>
        /// <param name="rawJson">Raw JSON response from GrowthBook API</param>
        /// <returns>Parsed features or null if parsing failed</returns>
        public ParsedFeatures ParseResponse(string rawJson)
        {
            if (string.IsNullOrEmpty(rawJson))
            {
                Debug.LogWarning("[PlaySuper][FeatureFlags] Cannot parse empty response");
                return null;
            }

            try
            {
                var response = JsonUtility.FromJson<GrowthBookResponse>(rawJson);
                if (response?.features == null)
                {
                    Debug.LogWarning("[PlaySuper][FeatureFlags] Response or features are null");
                    return null;
                }

                var parsedFeatures = new ParsedFeatures();

                // Parse each feature
                if (response.features.sdk_event_single_url != null)
                {
                    parsedFeatures.Features["sdk_event_single_url"] = ParseFeature(response.features.sdk_event_single_url, rawJson);
                }

                if (response.features.sdk_event_batch_url != null)
                {
                    parsedFeatures.Features["sdk_event_batch_url"] = ParseFeature(response.features.sdk_event_batch_url, rawJson);
                }

                if (response.features.sdk_enable_ad_id != null)
                {
                    parsedFeatures.Features["sdk_enable_ad_id"] = ParseFeature(response.features.sdk_enable_ad_id, rawJson);
                }

                if (response.features.sdk_request_timeout_seconds != null)
                {
                    parsedFeatures.Features["sdk_request_timeout_seconds"] = ParseFeature(response.features.sdk_request_timeout_seconds, rawJson);
                }

                if (response.features.sdk_config != null)
                {
                    parsedFeatures.Features["sdk_config"] = ParseFeature(response.features.sdk_config, rawJson);
                }

                return parsedFeatures;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaySuper][FeatureFlags] Failed to parse GrowthBook response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse a single feature with its rules and default value
        /// </summary>
        /// <param name="feature">Feature to parse</param>
        /// <param name="rawJson">Full raw JSON for extracting rule conditions</param>
        /// <returns>Parsed feature definition</returns>
        private FeatureDefinition ParseFeature(Feature feature, string rawJson)
        {
            var definition = new FeatureDefinition
            {
                DefaultValue = feature.defaultValue
            };

            if (feature.rules != null)
            {
                foreach (var rule in feature.rules)
                {
                    var parsedRule = new FeatureRule
                    {
                        Id = rule.id,
                        ForceValue = rule.force,
                        Condition = ParseCondition(rule.condition)
                    };
                    definition.Rules.Add(parsedRule);
                }
            }

            return definition;
        }

        /// <summary>
        /// Parse a condition into structured format
        /// </summary>
        /// <param name="condition">Condition to parse</param>
        /// <returns>Parsed condition</returns>
        private ParsedCondition ParseCondition(Condition condition)
        {
            if (condition == null) return null;

            var parsedCondition = new ParsedCondition();

            // Handle each possible condition attribute
            AddConditionAttribute(parsedCondition, "gamename", condition.gamename);
            AddConditionAttribute(parsedCondition, "gameid", condition.gameid);
            AddConditionAttribute(parsedCondition, "studioid", condition.studioid);
            AddConditionAttribute(parsedCondition, "platform", condition.platform);
            AddConditionAttribute(parsedCondition, "organizationid", condition.organizationid);
            AddConditionAttribute(parsedCondition, "organizationhandle", condition.organizationhandle);

            return parsedCondition;
        }

        /// <summary>
        /// Add a condition attribute to the parsed condition
        /// </summary>
        /// <param name="parsedCondition">Condition to add to</param>
        /// <param name="attributeName">Name of the attribute</param>
        /// <param name="conditionValue">Condition value to parse</param>
        private void AddConditionAttribute(ParsedCondition parsedCondition, string attributeName, ConditionValue conditionValue)
        {
            if (conditionValue == null) return;

            var op = new ConditionOperator
            {
                SimpleValue = conditionValue.value,
                InArray = conditionValue.inArray,
                NotEqual = conditionValue.notEqual,
                GreaterThan = conditionValue.greaterThan,
                LessThan = conditionValue.lessThan,
                GreaterThanEqual = conditionValue.greaterThanEqual,
                LessThanEqual = conditionValue.lessThanEqual,
                Exists = conditionValue.exists
            };

            parsedCondition.Attributes[attributeName] = op;
        }

        // These models are needed for JsonUtility parsing
        [Serializable]
        private class GrowthBookResponse
        {
            public Features features;
            public string status;
        }

        [Serializable]
        private class Features
        {
            public Feature sdk_event_single_url;
            public Feature sdk_event_batch_url;
            public Feature sdk_enable_ad_id;
            public Feature sdk_request_timeout_seconds;
            public Feature sdk_config;
        }

        [Serializable]
        private class Feature
        {
            public string defaultValue;
            public Rule[] rules;
        }

        [Serializable]
        private class Rule
        {
            public string id;
            public string force;
            public Condition condition;
        }

        [Serializable]
        private class Condition
        {
            public ConditionValue gamename;
            public ConditionValue gameid;
            public ConditionValue studioid;
            public ConditionValue platform;
            public ConditionValue organizationid;
            public ConditionValue organizationhandle;
        }

        [Serializable]
        private class ConditionValue
        {
            public string[] inArray;
            public string greaterThan;
            public string lessThan;
            public string greaterThanEqual;
            public string lessThanEqual;
            public string notEqual;
            public string exists;
            public string value;
        }
    }
}