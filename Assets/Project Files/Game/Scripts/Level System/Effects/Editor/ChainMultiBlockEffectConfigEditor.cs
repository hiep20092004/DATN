using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(ChainMultiBlockEffectConfig))]
    public class ChainMultiBlockEffectConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty chainVisualsDatasProp;

        private void OnEnable()
        {
            chainVisualsDatasProp = serializedObject.FindProperty("chainVisualsDatas");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Auto Generate Visuals Datas"))
            {
                AutoGenerateChainVisualsDatas();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoGenerateChainVisualsDatas()
        {
            Dictionary<BlockType, SerializedProperty> existingMap =
                new Dictionary<BlockType, SerializedProperty>();

            for (int i = 0; i < chainVisualsDatasProp.arraySize; i++)
            {
                SerializedProperty element = chainVisualsDatasProp.GetArrayElementAtIndex(i);
                BlockType blockType =
                    (BlockType)element.FindPropertyRelative("blockType").enumValueIndex;

                existingMap.TryAdd(blockType, element);
            }

            BlockType[] allBlockTypes =
                (BlockType[])Enum.GetValues(typeof(BlockType));

            chainVisualsDatasProp.arraySize = allBlockTypes.Length;

            for (int i = 0; i < allBlockTypes.Length; i++)
            {
                SerializedProperty element = chainVisualsDatasProp.GetArrayElementAtIndex(i);
                SerializedProperty blockTypeProp = element.FindPropertyRelative("blockType");
                SerializedProperty prefabProp = element.FindPropertyRelative("visualsPrefab");

                blockTypeProp.enumValueIndex = (int)allBlockTypes[i];

                if (existingMap.TryGetValue(allBlockTypes[i], out SerializedProperty oldElement))
                {
                    prefabProp.objectReferenceValue =
                        oldElement.FindPropertyRelative("visualsPrefab").objectReferenceValue;
                }
                else
                {
                    prefabProp.objectReferenceValue = null;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Debug.Log("[ChainMultiBlockEffectConfig] Auto generated chain visuals datas");
        }
    }
}
