using System;
using System.Collections.Generic;
using System.Linq;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>Outcome of running level validation rules (editor tooling).</summary>
    public sealed class LevelValidationResult
    {
        public LevelValidationResult(string levelName)
        {
            LevelName = levelName ?? string.Empty;
            Errors = new List<string>();
            FailedRuleTags = new HashSet<string>();
        }

        public string LevelName { get; }
        public List<string> Errors { get; }
        public HashSet<string> FailedRuleTags { get; }
        public bool IsValid => Errors.Count == 0;

        /// <summary>Suffix for level list labels, e.g. <c> [!Color,Overlap]</c>.</summary>
        public string GetLabelSuffix()
        {
            if (FailedRuleTags.Count == 0)
                return string.Empty;

            string joined = string.Join(",", FailedRuleTags.OrderBy(t => t));
            return $" [!{joined}]";
        }
    }

    /// <summary>Pluggable validation step for a level grid (<see cref="LevelData.Elements"/>).</summary>
    public interface ILevelValidationRule
    {
        /// <summary>Short token shown in <see cref="LevelValidationResult.GetLabelSuffix"/>.</summary>
        string Tag { get; }

        IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize);
    }

    public static class LevelValidator
    {
        public static List<ILevelValidationRule> CreateDefaultRules()
        {
            return new List<ILevelValidationRule>
            {
                new CheckEmptyLevelRule(),
                new GridCoverageRule(),
                new ColorBalanceRule(),
                new KeyColorLockedGatePairingRule(),
                new ContainerBoxBoundsMemberRule(),
                new BlockEffectCompatibilityRule(),
                new GrinderShapeRule(),
                new LevelDurationRule(),
            };
        }

        public static LevelValidationResult Validate(
            string levelName,
            SerializedProperty itemsProperty,
            Vector2Int gridSize,
            IEnumerable<ILevelValidationRule> rules)
        {
            LevelValidationResult result = new LevelValidationResult(levelName);
            if (rules == null)
                return result;

            foreach (ILevelValidationRule rule in rules)
            {
                if (rule == null)
                    continue;

                bool any = false;
                foreach (string message in rule.Validate(itemsProperty, gridSize))
                {
                    any = true;
                    result.Errors.Add(message);
                }

                if (any)
                    result.FailedRuleTags.Add(rule.Tag);
            }

            return result;
        }

        /// <param name="logPassedLevel">When true, logs a single info line for valid levels (e.g. Global Validation). Populate uses false to avoid console spam.</param>
        public static LevelValidationResult ValidateAndLog(
            string levelName,
            int displayIndex,
            SerializedProperty itemsProperty,
            Vector2Int gridSize,
            IEnumerable<ILevelValidationRule> rules,
            bool logPassedLevel = false)
        {
            LevelValidationResult result = Validate(levelName, itemsProperty, gridSize, rules);
            string prefix = $"[Level #{displayIndex} {levelName}]";

            if (result.IsValid)
            {
                if (logPassedLevel)
                    Debug.Log($"{prefix} passed validation.", null);
                return result;
            }

            foreach (string error in result.Errors)
                Debug.LogError($"{prefix} {error}", null);

            return result;
        }
    }
}
