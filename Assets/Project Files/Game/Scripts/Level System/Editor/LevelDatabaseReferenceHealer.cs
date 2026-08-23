using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public static class LevelDatabaseReferenceHealer
    {
        private const string BEHAVIOR_PROPERTY_NAME = "behavior";

        private static readonly string[] BEHAVIOR_ARRAY_PROPERTIES =
        {
            "effects",
            "gateEffects",
            "interactableObjects"
        };

        public struct HealResult
        {
            public int Relinked;
            public int Unresolved;
        }

        public static bool HasStaleReferences(LevelDatabase database)
        {
            if (!database)
                return false;

            foreach (string arrayName in BEHAVIOR_ARRAY_PROPERTIES)
            {
                bool[] staleFlags = GetStaleFlags(database, arrayName);

                foreach (bool isStale in staleFlags)
                {
                    if (isStale)
                        return true;
                }
            }

            return false;
        }

        public static HealResult Heal(LevelDatabase database, bool logResult = true)
        {
            HealResult result = default;

            if (!database)
                return result;

            SerializedObject serializedDatabase = new SerializedObject(database);

            foreach (string arrayName in BEHAVIOR_ARRAY_PROPERTIES)
            {
                SerializedProperty arrayProperty = serializedDatabase.FindProperty(arrayName);

                if (arrayProperty == null || !arrayProperty.isArray)
                    continue;

                bool[] staleFlags = GetStaleFlags(database, arrayName);
                int count = Mathf.Min(arrayProperty.arraySize, staleFlags.Length);

                for (int i = 0; i < count; i++)
                {
                    if (!staleFlags[i])
                        continue;

                    SerializedProperty behaviorProperty = arrayProperty
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative(BEHAVIOR_PROPERTY_NAME);

                    if (behaviorProperty == null)
                        continue;

                    UnityEngine.Object resolved = behaviorProperty.objectReferenceValue;

                    if (!resolved)
                    {
                        result.Unresolved++;
                        Debug.LogWarning(
                            $"LevelDatabase.{arrayName}[{i}] points to a prefab that no longer exists on disk. Assign it again on the LevelDatabase asset.",
                            database);
                        continue;
                    }

                    behaviorProperty.objectReferenceValue = null;
                    behaviorProperty.objectReferenceValue = resolved;
                    result.Relinked++;
                }
            }

            if (result.Relinked > 0)
            {
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();

                if (logResult)
                    Debug.Log($"LevelDatabase: relinked {result.Relinked} stale prefab reference(s).", database);
            }

            return result;
        }

        private static bool[] GetStaleFlags(LevelDatabase database, string arrayName)
        {
            switch (arrayName)
            {
                case "effects":
                    return MapStale(database.Effects, entry => entry.Behavior);
                case "gateEffects":
                    return MapStale(database.GateEffects, entry => entry.Behavior);
                case "interactableObjects":
                    return MapStale(database.InteractableObjects, entry => entry.Behavior);
                default:
                    return System.Array.Empty<bool>();
            }
        }

        private static bool[] MapStale<T>(T[] entries, System.Func<T, UnityEngine.Object> behaviorSelector)
            where T : class
        {
            if (entries == null)
                return System.Array.Empty<bool>();

            bool[] flags = new bool[entries.Length];

            for (int i = 0; i < entries.Length; i++)
                flags[i] = entries[i] != null && IsStale(behaviorSelector(entries[i]));

            return flags;
        }

        private static bool IsStale(UnityEngine.Object reference)
            => !ReferenceEquals(reference, null) && !reference;
    }
}
