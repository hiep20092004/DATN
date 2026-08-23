using System.Collections.Generic;
using System.Text;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>Editor-only representation of a <see cref="LevelData"/> asset (SerializedProperty based).</summary>
    public class LevelAssetRepresentation : LevelAssetRepresentationBase
    {
        private const string SIZE_PROPERTY_NAME = "size";
        private const string ITEMS_PROPERTY_NAME = "elements";
        private const string EXTRA_LAYER_ITEMS_PROPERTY_NAME = "extraLayerElements";
        private const string HAS_EXTRA_LAYER_PROPERTY_NAME = "hasExtraLayer";
        private const string LEVEL_DATA_TYPE_PROPERTY_NAME = "type";
        private const string DURATION_PROPERTY_NAME = "duration";
        private const string USE_IN_RANDOMIZER_PROPERTY_NAME = "useInRandomizer";
        private const string RANDOM_DURATION_PROPERTY_NAME = "randomDuration";
        private const string RANDOM_DURATION_LABEL = "Override Duration";

        // LevelElement
        public const string TYPE_PROPERTY_NAME = "type";
        public const string POSITION_PROPERTY_NAME = "position";
        public const string BLOCK_TYPE_PROPERTY_NAME = "blockType";
        public const string BLOCK_COLOR_PROPERTY_NAME = "blockColor";
        public const string IS_EXTENDABLE_PROPERTY_NAME = "isExtendable";
        public const string GATE_DATA_PROPERTY_NAME = "gateData";

        public const string BLOCK_ID_PROPERTY_NAME = "blockId";
        public const string BLOCK_EFFECTS_PROPERTY_NAME = "blockEffects";
        public const string GATE_EFFECTS_PROPERTY_NAME = "gateEffects";
        public const string INTERACTABLE_OBJECT_DATA_PROPERTY_NAME = "interactableObjectData";
        public const string INTERACTABLE_OBSTACLE_COLOR_PROPERTY_NAME = "obstacleColor";
        public const string INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME = "grinderConfig";
        public const string GENERATOR_QUEUE_PROPERTY_NAME = "generatorQueue";

        public SerializedProperty sizeProperty;
        public SerializedProperty itemsProperty;
        public SerializedProperty hasExtraLayerProperty;
        public SerializedProperty extraLayerItemsProperty;
        private SerializedProperty baseItemsProperty;
        public SerializedProperty BaseItemsProperty => baseItemsProperty;
        private SerializedProperty tempElement;
        private Vector2Int tempPosition;
        private bool isEditingExtraLayer;

        public bool IsEditingExtraLayer => isEditingExtraLayer;

        public LevelAssetRepresentation(Object levelObject) : base(levelObject) { }

        protected override void ReadFields()
        {
            sizeProperty = serializedLevelObject.FindProperty(SIZE_PROPERTY_NAME);
            baseItemsProperty = serializedLevelObject.FindProperty(ITEMS_PROPERTY_NAME);
            itemsProperty = baseItemsProperty;
            hasExtraLayerProperty = serializedLevelObject.FindProperty(HAS_EXTRA_LAYER_PROPERTY_NAME);
            extraLayerItemsProperty = serializedLevelObject.FindProperty(EXTRA_LAYER_ITEMS_PROPERTY_NAME);
        }

        /// <summary>Switches the active editing layer. When <paramref name="editExtraLayer"/> is true,
        /// all grid read/write operations target <c>extraLayerElements</c>; otherwise they target <c>elements</c>.</summary>
        public void SwitchActiveLayer(bool editExtraLayer)
        {
            serializedLevelObject.Update();
            baseItemsProperty = serializedLevelObject.FindProperty(ITEMS_PROPERTY_NAME);
            extraLayerItemsProperty = serializedLevelObject.FindProperty(EXTRA_LAYER_ITEMS_PROPERTY_NAME);
            hasExtraLayerProperty = serializedLevelObject.FindProperty(HAS_EXTRA_LAYER_PROPERTY_NAME);
            isEditingExtraLayer = editExtraLayer && hasExtraLayerProperty is { boolValue: true } && extraLayerItemsProperty != null;
            itemsProperty = isEditingExtraLayer ? extraLayerItemsProperty : baseItemsProperty;
        }

        public override string GetLevelLabel(int index, StringBuilder stringBuilder)
        {
            stringBuilder.Clear();
            stringBuilder.Append(NUMBER);
            stringBuilder.Append(index + 1);
            stringBuilder.Append(SEPARATOR);

            if (NullLevel)
            {
                stringBuilder.Append(NULL_FILE);
                return stringBuilder.ToString();
            }

            stringBuilder.Append(GetLevelName());

            if (LEVEL_CHECK_ENABLED)
            {
                ValidateLevel();

                if (!IsLevelCorrect)
                {
                    stringBuilder.Append(SEPARATOR);
                    stringBuilder.Append(INCORRECT);
                }
            }
            return stringBuilder.ToString();
        }

        private string GetLevelName()
        {
            string levelObjectName = levelObject.name;

            SerializedProperty levelTypeProp =
                serializedLevelObject.FindProperty(LEVEL_DATA_TYPE_PROPERTY_NAME);
            if (levelTypeProp is not { propertyType: SerializedPropertyType.Enum })
                return levelObjectName;

            var levelType = (LevelType)levelTypeProp.enumValueIndex;
            switch (levelType)
            {
                case LevelType.Hard:
                    return $"<color={Color.softRed.ToHex()}>{levelObjectName}</color>";
                case LevelType.VeryHard:
                    return $"<color={Color.mediumPurple.ToHex()}>{levelObjectName}</color>";
            }
            return levelObjectName;
        }

        public override void DisplayProperties()
        {
            foreach (SerializedProperty item in unmarkedProperties)
            {
                if (item == null || item.name == RANDOM_DURATION_PROPERTY_NAME)
                    continue;

                if (item.name == USE_IN_RANDOMIZER_PROPERTY_NAME)
                {
                    DrawRandomizerRow(item);
                    continue;
                }

                EditorGUILayout.PropertyField(item);
            }
        }

        private void DrawRandomizerRow(SerializedProperty useInRandomizerProperty)
        {
            SerializedProperty randomDurationProperty =
                serializedLevelObject.FindProperty(RANDOM_DURATION_PROPERTY_NAME);
            SerializedProperty durationProperty =
                serializedLevelObject.FindProperty(DURATION_PROPERTY_NAME);

            if (randomDurationProperty == null || durationProperty == null)
            {
                EditorGUILayout.PropertyField(useInRandomizerProperty);
                return;
            }

            const float toggleWidth = 18f;
            const float spacing = 8f;
            const float overrideLabelWidth = 116f;
            const float overrideFieldMinWidth = 48f;

            Rect rowRect = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth, rowRect.height);
            Rect toggleRect = new Rect(labelRect.xMax, rowRect.y, toggleWidth, rowRect.height);

            EditorGUI.LabelField(labelRect, useInRandomizerProperty.displayName);
            useInRandomizerProperty.boolValue = EditorGUI.Toggle(toggleRect, useInRandomizerProperty.boolValue);

            if (!useInRandomizerProperty.boolValue)
                return;

            float overrideX = toggleRect.xMax + spacing;
            float overrideWidth = rowRect.xMax - overrideX;
            if (overrideWidth < overrideLabelWidth + overrideFieldMinWidth)
                return;

            Rect overrideLabelRect = new Rect(overrideX, rowRect.y, overrideLabelWidth, rowRect.height);
            Rect overrideFieldRect = new Rect(overrideLabelRect.xMax, rowRect.y,
                overrideWidth - overrideLabelWidth, rowRect.height);

            EditorGUI.LabelField(overrideLabelRect, RANDOM_DURATION_LABEL);

            float storedOverride = randomDurationProperty.floatValue;
            float displayedOverride = storedOverride > 0f ? storedOverride : durationProperty.floatValue;

            EditorGUI.BeginChangeCheck();
            float nextOverride = EditorGUI.FloatField(overrideFieldRect, displayedOverride);
            if (EditorGUI.EndChangeCheck())
                randomDurationProperty.floatValue = nextOverride > 0f ? nextOverride : 0f;
        }

        public override void Clear()
        {
            sizeProperty.vector2IntValue = Vector2Int.one;
            itemsProperty.arraySize = 1;
            ApplyChanges();
        }

        public int GetItemsValue(int index1, int index2)
        {
            int index = GetIndex(index1, index2);
            if (index < 0)
                return (int)ElementType.InnerTile;

            return (int)GetElementType(itemsProperty.GetArrayElementAtIndex(index));
        }

        /// <summary>Extracts the element type from a grid cell SerializedProperty.</summary>
        public static ElementType GetElementType(SerializedProperty elementProp)
        {
            if (elementProp == null)
                return ElementType.InnerTile;

            if (elementProp.propertyType == SerializedPropertyType.ManagedReference)
            {
                return (elementProp.managedReferenceValue as LevelElementData)?.Type ?? ElementType.InnerTile;
            }

            SerializedProperty typeProp = elementProp.FindPropertyRelative(TYPE_PROPERTY_NAME);
            if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
                return (ElementType)typeProp.enumValueIndex;

            return ElementType.InnerTile;
        }

        /// <summary>Returns the primary editor color value for blocks (blockColor) or gates (first gateData color).</summary>
        public static int GetPrimaryColorValue(SerializedProperty elementProp)
        {
            if (elementProp == null)
                return 0;

            if (GetElementType(elementProp) == ElementType.Gate)
            {
                SerializedProperty gateDataArray = elementProp.FindPropertyRelative(GATE_DATA_PROPERTY_NAME);
                if (gateDataArray == null || gateDataArray.arraySize == 0)
                    return 0;

                SerializedProperty colorProp = gateDataArray.GetArrayElementAtIndex(0).FindPropertyRelative("color");
                return colorProp != null ? colorProp.intValue : 0;
            }

            SerializedProperty blockColorProp = elementProp.FindPropertyRelative(BLOCK_COLOR_PROPERTY_NAME);
            return blockColorProp != null ? blockColorProp.intValue : 0;
        }

        public void SetItemsValue(int index1, int index2, int newCellTypeValue, int newGateTypeValue,
            bool newBorderExtendable, InteractableObjectType newInteractableType)
        {
            Vector2Int cellPosition = new Vector2Int(index1, index2);
            int index = GetIndex(index1, index2);

            // Levels whose array lost coverage of a cell (duplicate/stale positions) are unpaintable and used to
            // throw on GetArrayElementAtIndex(-1); rebuild the layer once and paint on the recovered cell.
            if (index < 0)
            {
                if (RepairGridPositions())
                    index = GetIndex(index1, index2);

                if (index < 0)
                {
                    Debug.LogError($"[LevelEditor] Cell {cellPosition} is missing from the grid data of " +
                                   $"'{LevelObjectName}' and could not be recovered.");
                    return;
                }
            }

            SerializedProperty element = itemsProperty.GetArrayElementAtIndex(index);

            LevelElementData currentElement = element.managedReferenceValue as LevelElementData;
            ElementType currentType = currentElement?.Type ?? ElementType.InnerTile;
            ElementType newType = (ElementType)newCellTypeValue;

            int currentColorValue = currentElement switch
            {
                BlockLevelElementData b => (int)b.BlockColor,
                GateLevelElementData g when g.GateData.Count > 0 => (int)g.GateData[0].color,
                InteractableObjectLevelElementData io when io.InteractableObjectData != null =>
                    (int)io.InteractableObjectData.ObstacleColor,
                _ => 0,
            };

            bool willBecomeGenerator;
            if (currentType == newType)
                willBecomeGenerator = currentColorValue != newGateTypeValue && newType == ElementType.Generator;
            else
                willBecomeGenerator = newType == ElementType.Generator;

            if (willBecomeGenerator && !ValidateGeneratorCell(this, index1, index2))
                return;

            if (IsSameSelection(currentType, newType, currentColorValue, newGateTypeValue,
                    currentElement as InteractableObjectLevelElementData, newInteractableType))
            {
                // Toggle off → InnerTile
                element.managedReferenceValue = CreatePaintedElement(ElementType.InnerTile, cellPosition, 0, false);
                return;
            }

            // Generators carry a queue that a repaint must not wipe; only a type switch rebuilds the cell.
            if (currentType == newType && newType == ElementType.Generator)
                return;

            // A grinder repaint keeps an authored tape layout; only a grinder without a tape resets to default.
            Vector3Int currentGrinderConfig =
                (currentElement as InteractableObjectLevelElementData)?.InteractableObjectData?.GrinderConfig
                ?? Vector3Int.zero;

            element.managedReferenceValue = CreatePaintedElement(newType, cellPosition, newGateTypeValue,
                newBorderExtendable, newInteractableType, currentGrinderConfig);
        }

        /// <summary>Builds a fully initialized element for a paint action.</summary>
        /// <remarks>Every field — position included — is set on the instance <i>before</i> it is assigned to the
        /// array: relative properties of a freshly assigned managed reference are not reliable until the
        /// SerializedObject re-reads the object, and writing <c>position</c> through them corrupted grid data
        /// (duplicate positions + unreachable cells).</remarks>
        private static LevelElementData CreatePaintedElement(ElementType type, Vector2Int cellPosition,
            int colorValue, bool borderExtendable,
            InteractableObjectType interactableType = InteractableObjectData.EditorPaintedType,
            Vector3Int grinderConfig = default)
        {
            LevelElementData element = LevelElementData.CreateForType(type);
            element.SetPosition(cellPosition);

            switch (element)
            {
                case BlockLevelElementData block:
                    block.SetBlockType(BlockType.Single);
                    block.SetBlockColor((BlockColor)colorValue);
                    block.SetBlockEffects(null);
                    break;
                case BorderLevelElementData border:
                    border.SetExtendable(borderExtendable);
                    break;
                case GateLevelElementData gate:
                    gate.SetGateData(new List<ColorData>
                    {
                        new ColorData { color = (BlockColor)colorValue, colorCount = 1 },
                    });
                    gate.SetGateEffects(null);
                    break;
                case InteractableObjectLevelElementData interactable:
                    InteractableObjectData interactableData = new InteractableObjectData();
                    interactableData.SetType(interactableType);

                    if (InteractableObjectData.UsesObstacleColor(interactableType))
                    {
                        interactableData.SetObstacleColor((BlockColor)colorValue);
                    }
                    else if (interactableType == InteractableObjectType.Grinder)
                    {
                        interactableData.SetGrinderConfig(
                            GrinderLayout.From(grinderConfig).HasTape ? grinderConfig : Vector3Int.zero);
                    }

                    interactable.SetInteractableObjectData(interactableData);
                    break;
            }

            return element;
        }

        /// <summary>
        /// Repainting a cell with the exact same selection toggles it back to InnerTile. Interactables also
        /// compare their type, and only compare color for the types that are authored by color.
        /// </summary>
        private static bool IsSameSelection(ElementType currentType, ElementType newType,
            int currentColorValue, int newColorValue,
            InteractableObjectLevelElementData currentInteractable, InteractableObjectType newInteractableType)
        {
            if (currentType != newType)
                return false;

            if (newType != ElementType.InteractableObject)
                return currentColorValue == newColorValue;

            InteractableObjectType currentInteractableType =
                currentInteractable?.InteractableObjectData?.Type ?? InteractableObjectType.None;

            if (currentInteractableType != newInteractableType)
                return false;

            return !InteractableObjectData.UsesObstacleColor(newInteractableType)
                || currentColorValue == newColorValue;
        }

        /// <summary>Rebuilds the active layer so every cell inside <c>size</c> exists exactly once, keeping existing
        /// elements by position. Recovers levels whose grid data went out of sync (duplicate or stale positions).</summary>
        public bool RepairGridPositions()
        {
            if (NullLevel || sizeProperty == null || itemsProperty == null || !itemsProperty.isArray)
                return false;

            Vector2Int gridSize = sizeProperty.vector2IntValue;
            if (gridSize.x < 2 || gridSize.y < 2)
                return false;

            ResizeElementsArray(itemsProperty, gridSize);

            Debug.LogWarning($"[LevelEditor] Grid data of '{LevelObjectName}' was out of sync with size {gridSize} " +
                             "(duplicate or stale cell positions) and has been rebuilt.");
            return true;
        }

        private string LevelObjectName => levelObject == null ? "<null level>" : levelObject.name;

        public bool ValidateGeneratorCell(LevelAssetRepresentation rep, int x, int y)
        {
            Vector2Int gridSize = rep.sizeProperty.vector2IntValue;
            Vector2Int pos = new Vector2Int(x, y);

            if (!GeneratorPlacementRules.TryGetGeneratorGateDirection(pos, gridSize, p =>
                {
                    if (p.x < 0 || p.y < 0 || p.x >= gridSize.x || p.y >= gridSize.y)
                        return null;
                    return (ElementType)rep.GetItemsValue(p.x, p.y);
                }, out _))
            {
                Debug.LogError(
                    "Generator layout must match Gate rules: behind = Empty (or outside map); " +
                    "in front = InnerTile, Block, or InteractableObject; both flank cells = Border.");
                return false;
            }

            return true;
        }

        
        public int GetExtraPropsValue(int index1, int index2)
        {
            int index = GetIndex(index1, index2);
            if (index < 0)
                return (int)BlockType.Single;

            return itemsProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(BLOCK_TYPE_PROPERTY_NAME).intValue;
        }

        public void SetExtraPropsValue(int index1, int index2, int newValue)
        {
            int index = GetIndex(index1, index2);
            if (index < 0)
                return;

            itemsProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(BLOCK_TYPE_PROPERTY_NAME).intValue = newValue;
        }

        /// <summary>Array index of the cell at the given grid position, or -1 when the layer does not contain it.</summary>
        public int GetIndex(int index1, int index2)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                return -1;

            tempPosition.Set(index1, index2);

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                tempElement = itemsProperty.GetArrayElementAtIndex(i);

                // Position is null for unresolved managed references (e.g. after an undo of a [SerializeReference] edit).
                SerializedProperty positionProperty = tempElement.FindPropertyRelative(POSITION_PROPERTY_NAME);
                if (positionProperty != null && positionProperty.vector2IntValue.Equals(tempPosition))
                    return i;
            }

            return -1;
        }

        public void HandleSizePropertyChange()
        {
            if (sizeProperty.vector2IntValue.x < 2)
                sizeProperty.vector2IntValue = new Vector2Int(2, sizeProperty.vector2IntValue.y);

            if (sizeProperty.vector2IntValue.y < 2)
                sizeProperty.vector2IntValue = new Vector2Int(sizeProperty.vector2IntValue.x, 2);

            ResizeElementsArray(baseItemsProperty, sizeProperty.vector2IntValue);

            if (hasExtraLayerProperty != null && hasExtraLayerProperty.boolValue
                && extraLayerItemsProperty != null)
            {
                ResizeElementsArray(extraLayerItemsProperty, sizeProperty.vector2IntValue);
            }

            // Keep itemsProperty pointing at the correct layer after resize
            itemsProperty = isEditingExtraLayer ? extraLayerItemsProperty : baseItemsProperty;
        }

        private void ResizeElementsArray(SerializedProperty targetProp, Vector2Int newSize)
        {
            var cached = new Dictionary<Vector2Int, LevelElementData>(targetProp.arraySize);
            for (int i = 0; i < targetProp.arraySize; i++)
            {
                LevelElementData oldData = targetProp.GetArrayElementAtIndex(i).managedReferenceValue as LevelElementData;
                if (oldData != null)
                    cached[oldData.Position] = oldData;
            }

            targetProp.arraySize = 0;

            for (int y = 0; y < newSize.y; y++)
            {
                for (int x = 0; x < newSize.x; x++)
                {
                    targetProp.arraySize++;
                    SerializedProperty element = targetProp.GetArrayElementAtIndex(targetProp.arraySize - 1);
                    Vector2Int position = new Vector2Int(x, y);

                    if (!cached.TryGetValue(position, out LevelElementData elementData))
                    {
                        bool isBorder = x == 0 || y == 0 || x == newSize.x - 1 || y == newSize.y - 1;
                        elementData = isBorder
                            ? (LevelElementData)new BorderLevelElementData()
                            : new InnerTileLevelElementData();
                    }

                    // Stamp the position on the instance instead of through the just-assigned managed reference:
                    // see CreatePaintedElement remarks.
                    elementData.SetPosition(position);
                    element.managedReferenceValue = elementData;
                }
            }
        }

        public string GetBlockEffectLabel(SerializedProperty arrayProperty)
        {
            if (arrayProperty.arraySize > 1)
            {
                string[] effects = new string[arrayProperty.arraySize];
                for (int i = 0; i < arrayProperty.arraySize; i++)
                {
                    var effectData = arrayProperty.GetArrayElementAtIndex(i).managedReferenceValue as BlockEffectData;
                    effects[i] = effectData?.GetEditorLabel() ?? "?";
                }
                return string.Join(",", effects);
            }

            var singleEffect = arrayProperty.GetArrayElementAtIndex(0).managedReferenceValue as BlockEffectData;
            return singleEffect?.GetEditorLabel() ?? "?";
        }

        public void CopyEffects(SerializedProperty oldArrayProperty, SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = oldArrayProperty.arraySize;
            for (int i = 0; i < oldArrayProperty.arraySize; i++)
            {
                BlockEffectData src = oldArrayProperty.GetArrayElementAtIndex(i).managedReferenceValue as BlockEffectData;
                arrayProperty.GetArrayElementAtIndex(i).managedReferenceValue = src?.Clone();
            }
        }

        public void CopyGateData(SerializedProperty sourceArray, SerializedProperty destArray)
        {
            destArray.arraySize = sourceArray.arraySize;
            for (int i = 0; i < sourceArray.arraySize; i++)
                destArray.GetArrayElementAtIndex(i).boxedValue = sourceArray.GetArrayElementAtIndex(i).boxedValue;
        }

        public void CopyGateEffects(SerializedProperty sourceArray, SerializedProperty destArray)
        {
            destArray.arraySize = sourceArray.arraySize;
            for (int i = 0; i < sourceArray.arraySize; i++)
            {
                GateEffectData src = sourceArray.GetArrayElementAtIndex(i).managedReferenceValue as GateEffectData;
                destArray.GetArrayElementAtIndex(i).managedReferenceValue = src?.Clone();
            }
        }

        public void RecordUndo(string actionName)
        {
            if (levelObject == null) return;
            Undo.RecordObject(levelObject, actionName);
        }
    }
}

