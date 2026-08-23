using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Shutter Block Effect Config")]
    public class ShutterBlockEffectConfig : BaseBlockEffectConfig
    {
        [SerializeField] private float transitionDuration = 0.25f;
        [SerializeField] private ShutterVisualsData[] shutterVisualsDatas;

#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button("Auto Generate Shutter Visuals Datas")]
        private void AutoGenerateShutterVisualsDatas()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty shutterVisualsDatasProp = serializedObject.FindProperty("shutterVisualsDatas");

            Dictionary<BlockType, ShutterVisualsSerializedData> existingMap = new();

            for (int i = 0; i < shutterVisualsDatasProp.arraySize; i++)
            {
                SerializedProperty element = shutterVisualsDatasProp.GetArrayElementAtIndex(i);
                BlockType blockType = (BlockType)element.FindPropertyRelative("blockType").enumValueIndex;
                existingMap.TryAdd(blockType, ShutterVisualsSerializedData.FromProperty(element));
            }

            BlockType[] allBlockTypes = (BlockType[])Enum.GetValues(typeof(BlockType));
            shutterVisualsDatasProp.arraySize = allBlockTypes.Length;

            for (int i = 0; i < allBlockTypes.Length; i++)
            {
                SerializedProperty element = shutterVisualsDatasProp.GetArrayElementAtIndex(i);
                BlockType blockType = allBlockTypes[i];

                element.FindPropertyRelative("blockType").enumValueIndex = (int)blockType;

                if (existingMap.TryGetValue(blockType, out ShutterVisualsSerializedData oldData))
                {
                    oldData.ApplyToProperty(element);
                }
                else
                {
                    ShutterVisualsSerializedData.ClearProperty(element);
                }
            }

            serializedObject.ApplyModifiedProperties();
            RuntimeEditorUtils.SetDirty(this);

            Debug.Log("[ShutterBlockEffect] Auto generated shutter visuals datas");
        }

        private readonly struct ShutterVisualsSerializedData
        {
            private readonly UnityEngine.Object visualsPrefab;
            private readonly float minValue;
            private readonly float maxValue;
            private readonly UnityEngine.Object texture;

            private ShutterVisualsSerializedData(
                UnityEngine.Object visualsPrefab,
                float minValue,
                float maxValue,
                UnityEngine.Object texture)
            {
                this.visualsPrefab = visualsPrefab;
                this.minValue = minValue;
                this.maxValue = maxValue;
                this.texture = texture;
            }

            public static ShutterVisualsSerializedData FromProperty(SerializedProperty element)
            {
                return new ShutterVisualsSerializedData(
                    element.FindPropertyRelative("visualsPrefab").objectReferenceValue,
                    element.FindPropertyRelative("minValue").floatValue,
                    element.FindPropertyRelative("maxValue").floatValue,
                    element.FindPropertyRelative("texture").objectReferenceValue);
            }

            public void ApplyToProperty(SerializedProperty element)
            {
                element.FindPropertyRelative("visualsPrefab").objectReferenceValue = visualsPrefab;
                element.FindPropertyRelative("minValue").floatValue = minValue;
                element.FindPropertyRelative("maxValue").floatValue = maxValue;
                element.FindPropertyRelative("texture").objectReferenceValue = texture;
            }

            public static void ClearProperty(SerializedProperty element)
            {
                element.FindPropertyRelative("visualsPrefab").objectReferenceValue = null;
                element.FindPropertyRelative("minValue").floatValue = 0f;
                element.FindPropertyRelative("maxValue").floatValue = 0f;
                element.FindPropertyRelative("texture").objectReferenceValue = null;
            }
        }
#endif

        public float TransitionDuration => transitionDuration;

        public bool TryGetShutterVisualsData(BlockType blockType, out ShutterVisualsData data)
        {
            if (shutterVisualsDatas != null)
            {
                for (int i = 0; i < shutterVisualsDatas.Length; i++)
                {
                    if (shutterVisualsDatas[i].BlockType == blockType)
                    {
                        data = shutterVisualsDatas[i];
                        return true;
                    }
                }
            }

            data = null;
            return false;
        }
    }

    [Serializable]
    public class ShutterVisualsData
    {
        [SerializeField] private BlockType blockType;
        [SerializeField] private GameObject visualsPrefab;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue;
        [SerializeField] private Texture2D texture;

        public BlockType BlockType => blockType;
        public GameObject VisualsPrefab => visualsPrefab;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public Texture2D Texture => texture;
    }
}
