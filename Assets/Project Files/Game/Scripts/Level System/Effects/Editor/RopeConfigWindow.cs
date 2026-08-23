using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class RopeConfigWindow : EditorWindow
    {
        private const string PREVIEW_ROOT_NAME = "[ROPE CONFIG PREVIEW]";
        private const string ROPES_CONTAINER_NAME = "[ROPES]";

        private RopeEffectConfig config;
        private BlockType blockType;
        private Vector2 scroll;

        private RopeEffectEditorHandler preview;
        private bool IsPreviewActive => preview != null;

        private bool pendingSave;
        private bool pendingClear;
        private bool pendingOpen;
        private bool pendingGenerate;

        [MenuItem("WaterFlow/Level System/Rope Config")]
        public static void Open()
        {
            RopeConfigWindow window = GetWindow<RopeConfigWindow>("Rope Config");
            window.minSize = new Vector2(320f, 340f);
            window.Show();
        }

        private void OnEnable()
        {
            if (config == null)
                config = FindConfig();

            preview = FindFirstObjectByType<RopeEffectEditorHandler>();
            if (preview != null)
                blockType = preview.BlockType;
        }

        private void OnDisable() => ClearAllPreviews();

        private void OnGUI()
        {
            HandleQuickTypeShortcuts();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawConfigSection();
            EditorGUILayout.Space(6f);
            DrawBlockTypeSection();
            EditorGUILayout.Space(6f);
            DrawPreviewSection();

            EditorGUILayout.EndScrollView();

            ExecutePendingActions();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            config = (RopeEffectConfig)EditorGUILayout.ObjectField(
                new GUIContent("Rope Effect Config", "The ScriptableObject holding rope anchors per block type. Auto-located on open; assign manually to target a different asset."),
                config, typeof(RopeEffectConfig), false);
            if (EditorGUI.EndChangeCheck())
                pendingClear = true;

            if (config == null)
            {
                EditorGUILayout.HelpBox("No RopeEffectConfig assigned. Create one via Assets > Create > WaterFlow > Block Effects > Rope Block Effect Config, then assign it.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Try Auto-Locate", "Search the project for a RopeEffectConfig asset.")))
                    config = FindConfig();
            }
        }

        private void DrawBlockTypeSection()
        {
            using (new EditorGUI.DisabledScope(config == null))
            {
                EditorGUILayout.LabelField("Block Type", EditorStyles.boldLabel);

                blockType = (BlockType)EditorGUILayout.EnumPopup(
                    new GUIContent("Block Type", "Shape whose rope anchors you are editing."), blockType);

                if (GUILayout.Button(new GUIContent("Sync All Block Types",
                        "Ensure the config has exactly one entry per BlockType. Preserves authored anchors, appends empty entries for new shapes.")))
                {
                    config.Editor_SyncAllBlockTypes();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[RopeConfig] Synced all block types.", config);
                }
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUI.DisabledScope(config == null))
            {
                EditorGUILayout.LabelField("Preview & Authoring", EditorStyles.boldLabel);

                if (!IsPreviewActive)
                {
                    if (GUILayout.Button(new GUIContent("Open Preview",
                            "Spawn the selected block + its saved ropes in the scene for editing.")))
                        pendingOpen = true;
                    return;
                }

                if (preview.BlockType != blockType)
                {
                    EditorGUILayout.HelpBox(
                        $"A preview for {preview.BlockType} is open. Unsaved rope changes to {preview.BlockType} will be lost unless you save first.",
                        MessageType.Warning);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent($"Save & Switch to {blockType}",
                                $"Save the current {preview.BlockType} ropes to the config, then open {blockType}.")))
                        {
                            pendingSave = true;
                            pendingClear = true;
                            pendingOpen = true;
                        }

                        if (GUILayout.Button(new GUIContent($"Discard & Switch to {blockType}",
                                $"Drop the current {preview.BlockType} preview without saving, then open {blockType}.")))
                        {
                            pendingClear = true;
                            pendingOpen = true;
                        }
                    }
                    return;
                }

                EditorGUILayout.HelpBox(
                    $"Editing: {preview.BlockType}. Move/rotate ropes with Scene handles, then Save. " +
                    "Select rope(s) and set their length below (or press 1/2/3 while this window is focused).",
                    MessageType.Info);

                EditorGUILayout.LabelField(new GUIContent("Spawn Rope",
                    "Add a new rope of the chosen length (selected for you), then position it in the Scene view."), EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Spawn 1 · Single", "Spawn a SingleBlock-length rope.")))
                        SpawnRope(RopeType.SingleBlock);
                    if (GUILayout.Button(new GUIContent("Spawn 2 · Double", "Spawn a DoubleBlock-length rope.")))
                        SpawnRope(RopeType.DoubleBlock);
                    if (GUILayout.Button(new GUIContent("Spawn 3 · Triple", "Spawn a TripleBlock-length rope.")))
                        SpawnRope(RopeType.TripleBlock);
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(new GUIContent("Set Selected Rope Length",
                    "Applies to the currently selected rope(s). Rope has only 3 lengths."), EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("1 · Single", "SingleBlock length (shortcut: 1)")))
                        ApplyLengthToSelection(RopeType.SingleBlock);
                    if (GUILayout.Button(new GUIContent("2 · Double", "DoubleBlock length (shortcut: 2)")))
                        ApplyLengthToSelection(RopeType.DoubleBlock);
                    if (GUILayout.Button(new GUIContent("3 · Triple", "TripleBlock length (shortcut: 3)")))
                        ApplyLengthToSelection(RopeType.TripleBlock);
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(new GUIContent("Generate From Figure",
                    "Auto-spawn a grid of wrap ropes derived from this block's figure (cell runs → rope axis/length/position). Replaces the current preview ropes. Verify in Scene, tweak, then Save."), EditorStyles.miniBoldLabel);
                if (GUILayout.Button(new GUIContent("Generate",
                        "Clear the current preview ropes and spawn a fresh generated layout from the figure.")))
                    pendingGenerate = true;

                EditorGUILayout.Space(6f);
                if (GUILayout.Button(new GUIContent("Save To Config",
                        "Write the current ropes' transforms and lengths into the config for this block type.")))
                    pendingSave = true;

                if (GUILayout.Button(new GUIContent("Clear Preview", "Discard the preview objects (does not undo saved data).")))
                    pendingClear = true;
            }
        }

        private void ExecutePendingActions()
        {
            if (!pendingSave && !pendingClear && !pendingOpen && !pendingGenerate)
                return;

            if (pendingSave)
                SaveToConfig();
            if (pendingClear)
                ClearAllPreviews();
            if (pendingOpen)
                OpenPreview();
            if (pendingGenerate)
                GenerateFromFigure();

            pendingSave = pendingClear = pendingOpen = pendingGenerate = false;
            Repaint();
        }

        private void OpenPreview()
        {
            ClearAllPreviews();

            if (config == null)
                return;

            BlocksVisualsData blocksVisualsData = FindBlocksVisualsData();
            if (blocksVisualsData == null)
            {
                Debug.LogError("[RopeConfig] Failed to find BlocksVisualsData.");
                return;
            }

            BlockData blockData = blocksVisualsData.GetBlockData(blockType);
            if (blockData == null || blockData.Prefab == null)
            {
                Debug.LogError($"[RopeConfig] No block prefab registered for {blockType}.");
                return;
            }

            if (config.RopePrefab == null)
            {
                Debug.LogError("[RopeConfig] Config has no rope prefab assigned.");
                return;
            }

            GameObject root = new GameObject(PREVIEW_ROOT_NAME);
            root.hideFlags = HideFlags.DontSaveInEditor;
            preview = root.AddComponent<RopeEffectEditorHandler>();
            preview.BlockType = blockType;

            GameObject blockObject = (GameObject)PrefabUtility.InstantiatePrefab(blockData.Prefab);
            blockObject.transform.SetParent(root.transform, false);
            preview.BlockObject = blockObject;

            LevelBlockBehavior levelBlockBehavior = blockObject.GetComponent<LevelBlockBehavior>();
            if (levelBlockBehavior != null)
            {
                Bounds bounds = levelBlockBehavior.Figure.GetHorizontalCenterBounds();
                blockObject.transform.localPosition = -bounds.center;
            }

            GameObject ropesContainer = new GameObject(ROPES_CONTAINER_NAME);
            ropesContainer.transform.SetParent(root.transform, false);
            preview.RopesObject = ropesContainer;

            RopePositionData positionData = config.GetPositionData(blockType);
            if (positionData != null && positionData.TransformDatas != null)
            {
                foreach (RopeTransform transformData in positionData.TransformDatas)
                    InstantiateRope(ropesContainer.transform, transformData);
            }

            FramePreview(root.transform);
        }

        private void SpawnRope(RopeType ropeType)
        {
            if (!IsPreviewActive || config.RopePrefab == null)
                return;

            GameObject ropeObject = (GameObject)PrefabUtility.InstantiatePrefab(config.RopePrefab);
            ropeObject.transform.SetParent(preview.RopesObject.transform, false);
            ropeObject.transform.localPosition = new Vector3(0f, 1f, 0f);

            RopeBehavior ropeBehavior = ropeObject.GetComponent<RopeBehavior>();
            if (ropeBehavior != null)
                SetRopeType(ropeBehavior, ropeType);

            Selection.activeGameObject = ropeObject;
        }

        /// <summary>
        /// Spawns a grid of wrap ropes derived from the previewed block's figure, replacing any
        /// current preview ropes. Ropes are placed only on interior grid lines — each filled cell
        /// row/column gets a center-line rope, and boundaries between two filled cells get one too;
        /// outer-perimeter lines are skipped so no rope lies on the figure's outline. Each rope runs
        /// along its run (length = run length, capped at Triple). Structured starting layout — nudge
        /// offsets/Y then Save.
        /// </summary>
        private void GenerateFromFigure()
        {
            if (!IsPreviewActive || config.RopePrefab == null)
                return;

            LevelBlockBehavior block = preview.BlockObject != null
                ? preview.BlockObject.GetComponent<LevelBlockBehavior>()
                : null;
            if (block == null || block.Figure == null)
            {
                Debug.LogError("[RopeConfig] Preview block has no LevelBlockBehavior/figure; cannot generate.");
                return;
            }

            LevelFigure figure = block.Figure;
            Vector2Int size = figure.Size;
            PointData[] points = figure.Points;
            if (points == null || points.Length == 0)
            {
                Debug.LogWarning($"[RopeConfig] Figure for {preview.BlockType} has no points; nothing to generate.");
                return;
            }

            // Origin matches the offset applied to the block in OpenPreview, so ropes line up.
            Vector3 center = figure.GetHorizontalCenterBounds().center;

            Transform container = preview.RopesObject.transform;
            for (int i = container.childCount - 1; i >= 0; i--)
                DestroyImmediate(container.GetChild(i).gameObject);

            List<GameObject> spawned = new();
            bool[] line = new bool[Mathf.Max(size.x, size.y)];

            // Ropes running along X (rotation.y = 0): one per filled cell row + interior row boundaries.
            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                    line[x] = IsFilled(points, size, x, z);
                SpawnRunRopes(spawned, line, size.x, alongX: true, lineCoord: z - center.z, runCenterOffset: center.x);
            }
            for (int z = 0; z < size.y - 1; z++)
            {
                for (int x = 0; x < size.x; x++)
                    line[x] = IsFilled(points, size, x, z) && IsFilled(points, size, x, z + 1);
                SpawnRunRopes(spawned, line, size.x, alongX: true, lineCoord: z + 0.5f - center.z, runCenterOffset: center.x);
            }

            // Ropes running along Z (rotation.y = 90): one per filled cell column + interior column boundaries.
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                    line[z] = IsFilled(points, size, x, z);
                SpawnRunRopes(spawned, line, size.y, alongX: false, lineCoord: x - center.x, runCenterOffset: center.z);
            }
            for (int x = 0; x < size.x - 1; x++)
            {
                for (int z = 0; z < size.y; z++)
                    line[z] = IsFilled(points, size, x, z) && IsFilled(points, size, x + 1, z);
                SpawnRunRopes(spawned, line, size.y, alongX: false, lineCoord: x + 0.5f - center.x, runCenterOffset: center.z);
            }

            if (spawned.Count > 0)
                Selection.objects = spawned.ToArray();

            Debug.Log($"[RopeConfig] Generated {spawned.Count} rope(s) for {preview.BlockType} from figure. Adjust positions/Y in the Scene, then Save.");
        }

        private static bool IsFilled(PointData[] points, Vector2Int size, int x, int z)
        {
            int index = x + z * size.x;
            return index >= 0 && index < points.Length && points[index] != null && points[index].IsFilled;
        }

        /// <summary>
        /// Spawns one rope per maximal true-run in <paramref name="line"/>. <paramref name="lineCoord"/>
        /// is the fixed axis (z for along-X ropes, x for along-Z); each run spans the varying axis.
        /// </summary>
        private void SpawnRunRopes(List<GameObject> spawned, bool[] line, int length, bool alongX, float lineCoord, float runCenterOffset)
        {
            int i = 0;
            while (i < length)
            {
                if (!line[i]) { i++; continue; }

                int start = i;
                while (i < length && line[i]) i++;
                int end = i - 1;

                int runLength = end - start + 1;
                float runCenter = (start + end) * 0.5f - runCenterOffset;
                float x = alongX ? runCenter : lineCoord;
                float z = alongX ? lineCoord : runCenter;
                SpawnGeneratedRope(spawned, alongX, x, z, runLength);
            }
        }

        // Authored config consistently nudges each rope mesh's X scale slightly past 1 per length
        // (mesh doesn't perfectly span its nominal cell length); matches hand-tuned values across shapes.
        private static readonly float[] GENERATE_BASE_SCALE_X = { 0.97f, 1.04f, 1.09f };

        private void SpawnGeneratedRope(List<GameObject> spawned, bool alongX, float x, float z, int runLength)
        {
            // SingleBlock=1, DoubleBlock=2, TripleBlock=3 cells; longer runs cap at Triple + stretch.
            RopeType ropeType = (RopeType)Mathf.Clamp(runLength - 1, 0, 2);

            GameObject ropeObject = (GameObject)PrefabUtility.InstantiatePrefab(config.RopePrefab);
            Transform ropeTransform = ropeObject.transform;
            ropeTransform.SetParent(preview.RopesObject.transform, false);
            ropeTransform.localPosition = new Vector3(x, 0f, z);
            ropeTransform.localEulerAngles = new Vector3(0f, alongX ? 0f : 90f, 0f);
            float stretch = runLength > 3 ? runLength / 3f : 1f;
            ropeTransform.localScale = new Vector3(GENERATE_BASE_SCALE_X[(int)ropeType] * stretch, 1f, 1f);

            RopeBehavior ropeBehavior = ropeObject.GetComponent<RopeBehavior>();
            if (ropeBehavior != null)
                SetRopeType(ropeBehavior, ropeType);

            spawned.Add(ropeObject);
        }

        private void InstantiateRope(Transform parent, RopeTransform transformData)
        {
            GameObject ropeObject = (GameObject)PrefabUtility.InstantiatePrefab(config.RopePrefab);
            Transform ropeTransform = ropeObject.transform;
            ropeTransform.SetParent(parent, false);
            ropeTransform.localPosition = transformData.Position;
            ropeTransform.localEulerAngles = transformData.Rotation;
            ropeTransform.localScale = transformData.Scale;

            RopeBehavior ropeBehavior = ropeObject.GetComponent<RopeBehavior>();
            if (ropeBehavior != null)
                SetRopeType(ropeBehavior, transformData.RopeType);
        }

        private void ApplyLengthToSelection(RopeType ropeType)
        {
            if (!IsPreviewActive)
                return;

            int applied = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                RopeBehavior ropeBehavior = go.GetComponentInParent<RopeBehavior>();
                if (ropeBehavior == null)
                    continue;

                SetRopeType(ropeBehavior, ropeType);
                applied++;
            }

            if (applied == 0)
                Debug.LogWarning("[RopeConfig] No rope selected. Select a rope in the Scene/Hierarchy first.");
        }

        private static void SetRopeType(RopeBehavior ropeBehavior, RopeType ropeType)
        {
            SerializedObject so = new SerializedObject(ropeBehavior);
            so.FindProperty("ropeType").enumValueIndex = (int)ropeType;
            so.ApplyModifiedProperties();

            ropeBehavior.Editor_OnRopeTypeChanged();
            EditorUtility.SetDirty(ropeBehavior);
        }

        private void SaveToConfig()
        {
            if (!IsPreviewActive || config == null)
                return;

            RopeBehavior[] ropes = preview.RopesObject.GetComponentsInChildren<RopeBehavior>();

            SerializedObject so = new SerializedObject(config);
            SerializedProperty datas = so.FindProperty("ropePositionDatas");

            int index = FindBlockTypeIndex(datas, preview.BlockType);
            if (index == -1)
            {
                index = datas.arraySize;
                datas.arraySize++;
                datas.GetArrayElementAtIndex(index).FindPropertyRelative("blockType").intValue = (int)preview.BlockType;
            }

            SerializedProperty transformDatas = datas.GetArrayElementAtIndex(index).FindPropertyRelative("transformDatas");
            transformDatas.arraySize = ropes.Length;

            for (int i = 0; i < ropes.Length; i++)
            {
                SerializedProperty element = transformDatas.GetArrayElementAtIndex(i);
                Transform ropeTransform = ropes[i].transform;

                element.FindPropertyRelative("position").vector3Value = ropeTransform.localPosition;
                element.FindPropertyRelative("rotation").vector3Value = ropeTransform.localEulerAngles;
                element.FindPropertyRelative("scale").vector3Value = ropeTransform.localScale;
                element.FindPropertyRelative("ropeType").enumValueIndex = (int)ropes[i].RopeType;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RopeConfig] Saved {ropes.Length} rope(s) for {preview.BlockType}.", config);
        }

        private void ClearAllPreviews()
        {
            RopeEffectEditorHandler[] handlers = Resources.FindObjectsOfTypeAll<RopeEffectEditorHandler>();
            foreach (RopeEffectEditorHandler handler in handlers)
            {
                if (handler == null)
                    continue;

                GameObject go = handler.gameObject;
                if (EditorUtility.IsPersistent(go) || !go.scene.IsValid())
                    continue;

                DestroyImmediate(go);
            }

            preview = null;
        }

        private static void FramePreview(Transform root)
        {
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(root.position, Vector3.one * 4f), false);
        }

        private void HandleQuickTypeShortcuts()
        {
            if (!IsPreviewActive)
                return;

            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            RopeType? type = e.keyCode switch
            {
                KeyCode.Alpha1 or KeyCode.Keypad1 => RopeType.SingleBlock,
                KeyCode.Alpha2 or KeyCode.Keypad2 => RopeType.DoubleBlock,
                KeyCode.Alpha3 or KeyCode.Keypad3 => RopeType.TripleBlock,
                _ => null
            };

            if (type.HasValue)
            {
                ApplyLengthToSelection(type.Value);
                e.Use();
                Repaint();
            }
        }

        private static int FindBlockTypeIndex(SerializedProperty datas, BlockType type)
        {
            int target = (int)type;
            for (int i = 0; i < datas.arraySize; i++)
            {
                if (datas.GetArrayElementAtIndex(i).FindPropertyRelative("blockType").intValue == target)
                    return i;
            }
            return -1;
        }

        private static RopeEffectConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(RopeEffectConfig)}");
            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<RopeEffectConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static BlocksVisualsData FindBlocksVisualsData()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BlocksVisualsData)}");
            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<BlocksVisualsData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
