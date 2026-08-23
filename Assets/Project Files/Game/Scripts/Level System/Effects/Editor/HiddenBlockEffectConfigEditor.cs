using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(HiddenBlockEffectConfig))]
    public class HiddenBlockEffectConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty hiddenGlassVisualsDatasProp;

        private void OnEnable()
        {
            hiddenGlassVisualsDatasProp = serializedObject.FindProperty("hiddenGlassVisualsDatas");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Auto Generate Hidden Glass Visuals Datas"))
            {
                AutoGenerateHiddenGlassVisualsDatas();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoGenerateHiddenGlassVisualsDatas()
        {
            Dictionary<BlockType, SerializedProperty> existingMap =
                new Dictionary<BlockType, SerializedProperty>();

            for (int i = 0; i < hiddenGlassVisualsDatasProp.arraySize; i++)
            {
                SerializedProperty element = hiddenGlassVisualsDatasProp.GetArrayElementAtIndex(i);
                BlockType blockType =
                    (BlockType)element.FindPropertyRelative("blockType").enumValueIndex;

                existingMap.TryAdd(blockType, element);
            }

            BlockType[] allBlockTypes = (BlockType[])Enum.GetValues(typeof(BlockType));

            hiddenGlassVisualsDatasProp.arraySize = allBlockTypes.Length;

            for (int i = 0; i < allBlockTypes.Length; i++)
            {
                SerializedProperty element = hiddenGlassVisualsDatasProp.GetArrayElementAtIndex(i);
                SerializedProperty blockTypeProp = element.FindPropertyRelative("blockType");
                SerializedProperty tilingProp = element.FindPropertyRelative("tiling");
                SerializedProperty offsetProp = element.FindPropertyRelative("offset");

                blockTypeProp.enumValueIndex = (int)allBlockTypes[i];

                if (existingMap.TryGetValue(allBlockTypes[i], out SerializedProperty oldElement))
                {
                    tilingProp.vector2Value = oldElement.FindPropertyRelative("tiling").vector2Value;
                    offsetProp.vector2Value = oldElement.FindPropertyRelative("offset").vector2Value;
                }
                else
                {
                    tilingProp.vector2Value = Vector2.one;
                    offsetProp.vector2Value = Vector2.zero;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Debug.Log("[HiddenBlockEffectConfig] Auto generated hidden glass visuals datas");
        }
    }
}
