using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    /// <summary>
    /// Responsible for evaluating feature rules against game data
    /// </summary>
    internal class FeatureRuleEvaluator
    {
        private GameData gameData;

        // Attribute mapping dictionary
        private static readonly Dictionary<string, Func<GameData, string>> AttributeGetters = new Dictionary<string, Func<GameData, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "game_id", g => g?.id },
            { "gameid", g => g?.id },
            { "studio_id", g => g?.studioId },
            { "studioid", g => g?.studioId },
            { "game_name", g => g?.name },
            { "gamename", g => g?.name },
            { "organization_id", g => g?.studio?.organizationId },
            { "organizationid", g => g?.studio?.organizationId },
            { "studioorganizationid", g => g?.studio?.organizationId },
            { "organizationhandle", g => g?.studio?.organization?.handle },
            { "platform", g => g?.platform != null ? string.Join(",", g.platform) : null }
        };

        public FeatureRuleEvaluator(GameData gameData)
        {
            this.gameData = gameData;
        }

        /// <summary>
        /// Evaluate a feature to get its effective value based on rules and game data
        /// </summary>
        /// <typeparam name="T">Type of the feature value</typeparam>
        /// <param name="key">Feature key</param>
        /// <param name="defaultValue">Default value if no rules match</param>
        /// <param name="featureDefinition">Feature definition with rules</param>
        /// <param name="parser">Function to parse string value to target type</param>
        /// <returns>Evaluated feature value</returns>
        public T EvaluateFeature<T>(string key, T defaultValue, FeatureDefinition featureDefinition, Func<string, T> parser)
        {
            Debug.Log($"[PlaySuper][FeatureFlags] Evaluating feature '{key}' with default value '{defaultValue}'");

            if (featureDefinition == null)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Feature definition is null for {key}, using default value");
                return defaultValue;
            }

            // Try rules first
            var ruleValue = TryGetRuleValue(featureDefinition, key, parser);
            if (ruleValue.Success)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Feature '{key}' resolved to rule value: '{ruleValue.Value}'");
                return ruleValue.Value;
            }

            // Try default value
            var defaultFeatureValue = TryGetDefaultValue(featureDefinition, key, parser);
            if (defaultFeatureValue.Success)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Feature '{key}' resolved to default value: '{defaultFeatureValue.Value}'");
                return defaultFeatureValue.Value;
            }

            Debug.Log($"[PlaySuper][FeatureFlags] Feature '{key}' using provided default: '{defaultValue}'");
            return defaultValue;
        }

        /// <summary>
        /// Try to get a value from matching rules
        /// </summary>
        private ParseResult<T> TryGetRuleValue<T>(FeatureDefinition featureDefinition, string key, Func<string, T> parser)
        {
            if (featureDefinition.Rules == null || featureDefinition.Rules.Count == 0)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] No rules found for feature '{key}'");
                return new ParseResult<T> { Success = false };
            }

            Debug.Log($"[PlaySuper][FeatureFlags] Evaluating {featureDefinition.Rules.Count} rules for feature '{key}'");

            foreach (var rule in featureDefinition.Rules)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Evaluating rule '{rule.Id}' for feature '{key}'");

                if (rule.Condition == null)
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Rule '{rule.Id}' has no condition, skipping");
                    continue;
                }

                // Evaluate the condition against game data
                bool conditionMatched = EvaluateCondition(rule.Condition);
                Debug.Log($"[PlaySuper][FeatureFlags] Rule '{rule.Id}' condition result: {conditionMatched}");

                if (conditionMatched)
                {
                    if (!string.IsNullOrEmpty(rule.ForceValue))
                    {
                        try
                        {
                            var parsedValue = parser(rule.ForceValue);
                            Debug.Log($"[PlaySuper][FeatureFlags] Rule '{rule.Id}' matched! Using force value: '{parsedValue}' for feature '{key}'");
                            return new ParseResult<T> { Success = true, Value = parsedValue };
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlaySuper][FeatureFlags] Invalid value in rule '{rule.Id}' for feature '{key}': '{rule.ForceValue}', Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.Log($"[PlaySuper][FeatureFlags] Rule '{rule.Id}' matched but has no force value for feature '{key}'");
                    }
                }
                else
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Rule '{rule.Id}' condition not met for feature '{key}'");
                }
            }

            Debug.Log($"[PlaySuper][FeatureFlags] No rules matched for feature '{key}'");
            return new ParseResult<T> { Success = false };
        }

        /// <summary>
        /// Try to get a value from the feature's default value
        /// </summary>
        private ParseResult<T> TryGetDefaultValue<T>(FeatureDefinition featureDefinition, string key, Func<string, T> parser)
        {
            if (string.IsNullOrEmpty(featureDefinition.DefaultValue))
            {
                return new ParseResult<T> { Success = false };
            }

            try
            {
                var parsedValue = parser(featureDefinition.DefaultValue);
                Debug.Log($"[PlaySuper][FeatureFlags] Using default value for {key}: {parsedValue}");
                return new ParseResult<T> { Success = true, Value = parsedValue };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaySuper][FeatureFlags] Invalid default value for {key}: {featureDefinition.DefaultValue}, Error: {ex.Message}");
                return new ParseResult<T> { Success = false };
            }
        }

        /// <summary>
        /// Evaluate a condition against the current game data
        /// </summary>
        /// <param name="condition">Condition to evaluate</param>
        /// <returns>True if condition matches game data</returns>
        public bool EvaluateCondition(ParsedCondition condition)
        {
            Debug.Log($"[PlaySuper][FeatureFlags] Evaluating condition with {condition?.Attributes?.Count ?? 0} attributes");

            if (condition == null || condition.Attributes.Count == 0)
            {
                Debug.Log("[PlaySuper][FeatureFlags] No condition specified, evaluating as true");
                return true;
            }

            if (gameData == null)
            {
                Debug.LogWarning("[PlaySuper][FeatureFlags] No game data available for condition evaluation");
                return false;
            }

            // Log game data for debugging
            Debug.Log($"[PlaySuper][FeatureFlags] Game data - ID: {gameData.id}, Name: {gameData.name}, StudioID: {gameData.studioId}");

            // All conditions must match (AND logic)
            foreach (var attribute in condition.Attributes)
            {
                var attributeName = attribute.Key;
                var operatorValue = attribute.Value;

                Debug.Log($"[PlaySuper][FeatureFlags] Evaluating attribute '{attributeName}'");

                // Get actual game data value
                if (!AttributeGetters.TryGetValue(attributeName, out var getter))
                {
                    Debug.LogWarning($"[PlaySuper][FeatureFlags] Unknown attribute: {attributeName}");
                    return false;
                }

                string actualValue = getter(gameData);
                Debug.Log($"[PlaySuper][FeatureFlags] Actual value for '{attributeName}': '{actualValue}'");

                bool attributeMatched = EvaluateOperator(operatorValue, actualValue);
                Debug.Log($"[PlaySuper][FeatureFlags] Attribute '{attributeName}' match result: {attributeMatched}");

                if (!attributeMatched)
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Condition failed for attribute '{attributeName}' with value '{actualValue}'");
                    return false;
                }
            }

            Debug.Log("[PlaySuper][FeatureFlags] All attributes matched, condition evaluates to TRUE");
            return true;
        }

        /// <summary>
        /// Evaluate a condition operator against an actual value
        /// </summary>
        /// <param name="op">Condition operator</param>
        /// <param name="actualValue">Actual value from game data</param>
        /// <returns>True if the operator matches the actual value</returns>
        private bool EvaluateOperator(ConditionOperator op, string actualValue)
        {
            Debug.Log($"[PlaySuper][FeatureFlags] Evaluating operator with actual value: '{actualValue}'");

            // Handle simple string comparison
            if (!string.IsNullOrEmpty(op.SimpleValue))
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Evaluating direct value condition: '{actualValue}' == '{op.SimpleValue}'");
                bool result = string.Equals(actualValue, op.SimpleValue, StringComparison.OrdinalIgnoreCase);
                Debug.Log($"[PlaySuper][FeatureFlags] Direct value comparison result: {result}");
                return result;
            }

            // Handle $in operator ($in)
            if (op.InArray != null && op.InArray.Length > 0)
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $in condition: checking if '{actualValue}' is in [{string.Join(", ", op.InArray)}]");
                bool result = Array.Exists(op.InArray, value =>
                    string.Equals(value, actualValue, StringComparison.OrdinalIgnoreCase));
                Debug.Log($"[PlaySuper][FeatureFlags] $in operator result: {result}");
                return result;
            }

            // Handle comparison operators for numeric values
            if (double.TryParse(actualValue, out double actualNumber))
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Actual value '{actualValue}' is numeric ({actualNumber}), checking numeric operators");

                // Greater than
                if (!string.IsNullOrEmpty(op.GreaterThan) && double.TryParse(op.GreaterThan, out double gtValue))
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $gt condition: {actualNumber} > {gtValue}");
                    bool result = actualNumber > gtValue;
                    Debug.Log($"[PlaySuper][FeatureFlags] $gt condition result: {result}");
                    if (!result) return false;
                }

                // Less than
                if (!string.IsNullOrEmpty(op.LessThan) && double.TryParse(op.LessThan, out double ltValue))
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $lt condition: {actualNumber} < {ltValue}");
                    bool result = actualNumber < ltValue;
                    Debug.Log($"[PlaySuper][FeatureFlags] $lt condition result: {result}");
                    if (!result) return false;
                }

                // Greater than or equal
                if (!string.IsNullOrEmpty(op.GreaterThanEqual) && double.TryParse(op.GreaterThanEqual, out double gteValue))
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $gte condition: {actualNumber} >= {gteValue}");
                    bool result = actualNumber >= gteValue;
                    Debug.Log($"[PlaySuper][FeatureFlags] $gte condition result: {result}");
                    if (!result) return false;
                }

                // Less than or equal
                if (!string.IsNullOrEmpty(op.LessThanEqual) && double.TryParse(op.LessThanEqual, out double lteValue))
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $lte condition: {actualNumber} <= {lteValue}");
                    bool result = actualNumber <= lteValue;
                    Debug.Log($"[PlaySuper][FeatureFlags] $lte condition result: {result}");
                    if (!result) return false;
                }
            }
            else
            {
                Debug.Log($"[PlaySuper][FeatureFlags] Actual value '{actualValue}' is not numeric, checking string operators");

                // String comparison for non-numeric values
                // Not equal
                if (!string.IsNullOrEmpty(op.NotEqual))
                {
                    Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $ne condition: '{actualValue}' != '{op.NotEqual}'");
                    bool result = !string.Equals(actualValue, op.NotEqual, StringComparison.OrdinalIgnoreCase);
                    Debug.Log($"[PlaySuper][FeatureFlags] $ne condition result: {result}");
                    if (!result) return false;
                }
            }

            // Exists check
            if (!string.IsNullOrEmpty(op.Exists))
            {
                bool shouldExist = op.Exists.ToLower() == "true";
                bool exists = !string.IsNullOrEmpty(actualValue);
                Debug.Log($"[PlaySuper][FeatureFlags] Evaluating $exists condition: value exists ({exists}) should be ({shouldExist})");
                bool result = exists == shouldExist;
                Debug.Log($"[PlaySuper][FeatureFlags] $exists condition result: {result}");
                return result;
            }

            // If no specific conditions were specified, return true
            Debug.Log("[PlaySuper][FeatureFlags] No specific operators matched, returning TRUE");
            return true;
        }

        private class ParseResult<T>
        {
            public bool Success { get; set; }
            public T Value { get; set; }
        }
    }
}