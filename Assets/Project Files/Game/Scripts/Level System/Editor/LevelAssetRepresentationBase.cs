using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class LevelAssetRepresentationBase
    {
        protected const string NUMBER = "#";
        protected const string SEPARATOR = " | ";
        protected const string NULL_FILE = "[Null file]";
        protected const string INCORRECT = "[Incorrect]";

        protected SerializedObject serializedLevelObject;
        protected Object levelObject;
        private readonly bool nullLevel;
        public List<string> errorLabels;
        protected readonly IEnumerable<SerializedProperty> unmarkedProperties;
        protected int selectedEditorList;

        public bool NullLevel => nullLevel;

        /// <summary>ScriptableObject currently being edited in the level editor (base level or a variant asset).</summary>
        public Object EditedLevelObject => levelObject;

        protected virtual bool LEVEL_CHECK_ENABLED => false;

        public bool IsLevelCorrect => errorLabels.Count == 0;

        protected LevelAssetRepresentationBase(Object levelObject)
        {
            this.levelObject = levelObject;
            nullLevel = (levelObject == null);
            errorLabels = new List<string>();

            if (!nullLevel)
            {
                serializedLevelObject = new SerializedObject(levelObject);
                ReadFields();
                unmarkedProperties = LevelEditorUtils.GetUnmarkedProperties(serializedLevelObject);
            }
        }

        protected abstract void ReadFields();

        public abstract void Clear();

        public void ApplyChanges()
        {
            if (!NullLevel)
                serializedLevelObject.ApplyModifiedProperties();
        }

        public void RefreshSerializedObject()
        {
            if (!NullLevel)
                serializedLevelObject.Update();
        }

        public virtual string GetLevelLabel(int index, StringBuilder stringBuilder)
        {
            stringBuilder.Clear();
            stringBuilder.Append(NUMBER);
            stringBuilder.Append(index + 1);
            stringBuilder.Append(SEPARATOR);

            if (NullLevel)
            {
                stringBuilder.Append(NULL_FILE);
            }
            else
            {
                stringBuilder.Append(levelObject.name);

                if (LEVEL_CHECK_ENABLED)
                {
                    ValidateLevel();

                    if (!IsLevelCorrect)
                    {
                        stringBuilder.Append(SEPARATOR);
                        stringBuilder.Append(INCORRECT);
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public virtual void ValidateLevel() { }

        public virtual void DisplayProperties()
        {
            foreach (SerializedProperty item in unmarkedProperties)
            {
                if (item == null) continue;
                EditorGUILayout.PropertyField(item);
            }
        }
    }
}
