using UnityEngine;
using UnityEditor;
using System.Linq;

namespace WaterFlow.Game
{
    /// <summary>
    /// Handles all CRUD operations for BlocksVisualsData.
    /// Provides centralized data manipulation with proper serialization and validation.
    /// </summary>
    public class DataOperationsHandler
    {
        #region Block Operations
        
        public bool AddBlock(BlocksVisualsData data, BlockType type, GameObject prefab)
        {
            if (data == null)
            {
                Debug.LogError("Cannot add block: data is null");
                return false;
            }

            if (prefab == null)
            {
                Debug.LogError("Cannot add block: prefab is null");
                return false;
            }

            if (IsBlockTypeExists(data, type))
            {
                Debug.LogWarning($"Block type {type} already exists");
                return false;
            }

            // Validate prefab has required component
            if (prefab.GetComponent<LevelBlockBehavior>() == null)
            {
                Debug.LogError($"Prefab must have LevelBlockBehavior component");
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty blocksProperty = serializedObject.FindProperty("blocks");

            int newIndex = blocksProperty.arraySize;
            blocksProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newBlock = blocksProperty.GetArrayElementAtIndex(newIndex);
            newBlock.FindPropertyRelative("type").enumValueIndex = (int)type;
            newBlock.FindPropertyRelative("prefab").objectReferenceValue = prefab;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            // Auto initialize
            data.Init();

            Debug.Log($"✓ Added block: {type}");
            return true;
        }

        public bool UpdateBlock(BlocksVisualsData data, int index, GameObject prefab)
        {
            if (data == null || index < 0 || index >= (data.Blocks?.Length ?? 0))
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty blocksProperty = serializedObject.FindProperty("blocks");
            SerializedProperty blockProperty = blocksProperty.GetArrayElementAtIndex(index);

            if (prefab != null)
            {
                blockProperty.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            data.Init();

            return true;
        }

        public bool RemoveBlock(BlocksVisualsData data, int index)
        {
            if (data == null || index < 0 || index >= (data.Blocks?.Length ?? 0))
            {
                return false;
            }

            BlockType removedType = data.Blocks[index].Type;

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty blocksProperty = serializedObject.FindProperty("blocks");

            blocksProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"✓ Removed block: {removedType}");
            return true;
        }

        public bool IsBlockTypeExists(BlocksVisualsData data, BlockType type)
        {
            if (data?.Blocks == null) return false;
            return data.Blocks.Any(b => b.Type == type);
        }
        
        #endregion

        #region Color Operations
        
        public bool AddColor(
            BlocksVisualsData data, 
            BlockColor type, 
            Material material, 
            Material pipeMaterial,
            Material glassMaterial,
            Material waterMaterial,
            Material flowMaterial,
            Color color)
        {
            if (data == null)
            {
                Debug.LogError("Cannot add color: data is null");
                return false;
            }

            if (IsColorTypeExists(data, type))
            {
                Debug.LogWarning($"Color type {type} already exists");
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty colorsProperty = serializedObject.FindProperty("colors");

            int newIndex = colorsProperty.arraySize;
            colorsProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newColor = colorsProperty.GetArrayElementAtIndex(newIndex);
            newColor.FindPropertyRelative("type").enumValueIndex = (int)type;
            newColor.FindPropertyRelative("material").objectReferenceValue = material;
            newColor.FindPropertyRelative("pipeMaterial").objectReferenceValue = pipeMaterial;
            newColor.FindPropertyRelative("glassMaterial").objectReferenceValue = glassMaterial;
            newColor.FindPropertyRelative("waterMaterial").objectReferenceValue = waterMaterial;
            newColor.FindPropertyRelative("flowMaterial").objectReferenceValue = flowMaterial;
            newColor.FindPropertyRelative("color").colorValue = color;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"✓ Added color: {type}");
            return true;
        }

        public bool UpdateColor(
            BlocksVisualsData data, 
            int index,
            Material material,
            Material pipeMaterial,
            Material glassMaterial,
            Material waterMaterial,
            Material flowMaterial,
            Color color)
        {
            if (data == null || index < 0 || index >= (data.Colors?.Length ?? 0))
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty colorsProperty = serializedObject.FindProperty("colors");
            SerializedProperty colorProperty = colorsProperty.GetArrayElementAtIndex(index);

            colorProperty.FindPropertyRelative("material").objectReferenceValue = material;
            colorProperty.FindPropertyRelative("pipeMaterial").objectReferenceValue = pipeMaterial;
            colorProperty.FindPropertyRelative("glassMaterial").objectReferenceValue = glassMaterial;
            colorProperty.FindPropertyRelative("waterMaterial").objectReferenceValue = waterMaterial;
            colorProperty.FindPropertyRelative("flowMaterial").objectReferenceValue = flowMaterial;
            colorProperty.FindPropertyRelative("color").colorValue = color;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            return true;
        }

        public bool RemoveColor(BlocksVisualsData data, int index)
        {
            if (data == null || index < 0 || index >= (data.Colors?.Length ?? 0))
            {
                return false;
            }

            BlockColor removedType = data.Colors[index].Type;

            SerializedObject serializedObject = new SerializedObject(data);
            SerializedProperty colorsProperty = serializedObject.FindProperty("colors");

            colorsProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"✓ Removed color: {removedType}");
            return true;
        }

        public bool IsColorTypeExists(BlocksVisualsData data, BlockColor type)
        {
            if (data?.Colors == null) return false;
            return data.Colors.Any(c => c.Type == type);
        }
        
        #endregion

        #region Validation
        
        public ValidationResult ValidateBlock(GameObject prefab)
        {
            var result = new ValidationResult();

            if (prefab == null)
            {
                result.AddError("Prefab is required");
                return result;
            }

            var behavior = prefab.GetComponent<LevelBlockBehavior>();
            if (behavior == null)
            {
                result.AddError("Prefab must have LevelBlockBehavior component");
            }

            return result;
        }

        public ValidationResult ValidateColor(Material material)
        {
            var result = new ValidationResult();

            if (material == null)
            {
                result.AddWarning("Material is not assigned");
            }

            return result;
        }
        
        #endregion
    }

    #region Validation Result
    
    public class ValidationResult
    {
        public System.Collections.Generic.List<string> Errors { get; private set; }
        public System.Collections.Generic.List<string> Warnings { get; private set; }

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

        public ValidationResult()
        {
            Errors = new System.Collections.Generic.List<string>();
            Warnings = new System.Collections.Generic.List<string>();
        }

        public void AddError(string error)
        {
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public string GetMessage()
        {
            if (Errors.Count > 0)
            {
                return string.Join("\n", Errors);
            }
            if (Warnings.Count > 0)
            {
                return string.Join("\n", Warnings);
            }
            return "Valid";
        }
    }
    
    #endregion
}