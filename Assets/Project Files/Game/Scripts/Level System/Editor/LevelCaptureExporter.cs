using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Exports PNG captures of levels framed to their bounds, reusing the Level Editor scene
    /// spawn path (<see cref="EditorSceneController.LoadLevel"/>) and the gameplay
    /// <see cref="CameraController"/> so the framing matches what players see.
    /// Output aspect ratio follows each level's bounds (auto aspect). Batch range support
    /// is built in for later Figma export.
    /// </summary>
    public class LevelCaptureExporter : EditorWindow
    {
        private const string EDITOR_SCENE_PATH = "Assets/Project Files/Game/Scenes/Level Editor.unity";

        // 1-based in UI to match how designers refer to levels.
        private int fromLevel = 1;
        private int toLevel = 1;
        private int baseHeight = 1024;
        private bool transparentBackground;
        // Extra space around the board, as a fraction of the tight fit (0 = edge-to-edge).
        private float marginPercent = 15f;
        private string outputFolder = "LevelCaptures";

        [MenuItem("Tools/Level Extension/Level Capture Exporter")]
        private static void Open()
        {
            GetWindow<LevelCaptureExporter>("Level Capture").minSize = new Vector2(320, 220);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Capture levels framed to their bounds. Requires the 'Level Editor' scene " +
                "(opens it on demand). Output aspect follows each level's bounds.",
                MessageType.Info);

            EditorGUILayout.Space();
            fromLevel = EditorGUILayout.IntField("From Level (1-based)", fromLevel);
            toLevel = EditorGUILayout.IntField("To Level (1-based)", toLevel);
            baseHeight = EditorGUILayout.IntField("Output Height (px)", baseHeight);
            marginPercent = EditorGUILayout.Slider("Margin (%)", marginPercent, 0f, 30f);
            transparentBackground = EditorGUILayout.Toggle("Transparent Background", transparentBackground);

            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFolderPanel("Output Folder", ResolveOutputFolder(), "");
                if (!string.IsNullOrEmpty(picked)) outputFolder = picked;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Export", GUILayout.Height(32)))
            {
                int count = ExportRange(fromLevel - 1, toLevel - 1, baseHeight, ResolveOutputFolder(), transparentBackground, marginPercent / 100f);
                if (count > 0)
                    EditorUtility.RevealInFinder(ResolveOutputFolder());
            }
        }

        private string ResolveOutputFolder()
        {
            if (Path.IsPathRooted(outputFolder)) return outputFolder;
            // Project root = parent of Assets/.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, outputFolder);
        }

        /// <summary>
        /// Loads each level in [fromIndex, toIndex] (0-based, inclusive) into the Level Editor scene,
        /// repositions the gameplay camera to the level bounds, and writes a PNG per level.
        /// Returns the number of captures written.
        /// </summary>
        public static int ExportRange(int fromIndex, int toIndex, int baseHeight, string folder, bool transparent, float margin = 0.05f)
        {
            if (!EnsureEditorScene()) return 0;

            EditorSceneController controller = EditorSceneController.Instance;
            LevelDatabase database = controller.LevelDatabase;
            if (database == null)
            {
                Debug.LogError("LevelCaptureExporter: LevelDatabase is not assigned on EditorSceneController.");
                return 0;
            }

            int total = database.AmountOfLevels;
            fromIndex = Mathf.Clamp(fromIndex, 0, total - 1);
            toIndex = Mathf.Clamp(toIndex, fromIndex, total - 1);

            var requests = new List<(LevelData Level, string Path)>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                LevelData levelData = database.GetLevel(index);
                if (levelData == null) continue;
                requests.Add((levelData, Path.Combine(folder, $"level_{index + 1:D4}.png")));
            }

            return ExportLevels(requests, baseHeight, transparent, margin);
        }

        /// <summary>
        /// Captures each level in <paramref name="requests"/> to its output PNG path, loading levels into the
        /// Level Editor scene (opens it on demand, prompting to save the current scene). Used by the range export
        /// above and by the Build Asset Bundle zip export. Returns the number of captures written.
        /// </summary>
        public static int ExportLevels(IReadOnlyList<(LevelData Level, string Path)> requests, int baseHeight, bool transparent, float margin = 0.05f)
        {
            if (requests == null || requests.Count == 0) return 0;
            if (!EnsureEditorScene()) return 0;

            EditorSceneController controller = EditorSceneController.Instance;
            Camera camera = controller.EditorMainCamera;
            CameraController cameraController = camera != null ? camera.GetComponent<CameraController>() : null;
            if (camera == null || cameraController == null)
            {
                Debug.LogError("LevelCaptureExporter: Editor main camera / CameraController not found in scene.");
                return 0;
            }

            int written = 0;
            try
            {
                for (int i = 0; i < requests.Count; i++)
                {
                    (LevelData levelData, string path) = requests[i];
                    if (levelData == null || string.IsNullOrEmpty(path)) continue;

                    EditorUtility.DisplayProgressBar("Level Capture",
                        $"Capture {i + 1} / {requests.Count}", i / (float)requests.Count);

                    // Reuse the proven editor spawn path; null callbacks are safe because they are
                    // only invoked during interactive scene editing, not during load/spawn.
                    controller.LoadLevel(levelData, null, null, null, null, null, null, null, null);

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    CaptureToFile(camera, cameraController, controller.CurrentLevelBounds, baseHeight, path, transparent, margin);
                    written++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"LevelCaptureExporter: exported {written} level(s).");
            return written;
        }

        private static void CaptureToFile(Camera camera, CameraController cameraController,
            Bounds bounds, int baseHeight, string path, bool transparent, float margin)
        {
            // Reposition gives us the correct gameplay angle/position. The pitched camera means the
            // board does NOT project to a bounds.size.x by bounds.size.z rectangle, so we fit using
            // the actual screen-space projection of the bounds corners (this also bypasses the
            // gameplay UI/banner padding baked into the config).
            cameraController.Reposition(bounds);

            int height = Mathf.Max(8, baseHeight);
            int width;
            if (camera.orthographic)
            {
                Transform camTransform = camera.transform;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                Vector3 ext = bounds.extents;
                // GetBounds() is the board grid only (gates/connectors spill ~half a cell past it),
                // so margin doubles as the connector allowance, applied symmetrically about the
                // projected center below.
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + new Vector3(
                        (i & 1) == 0 ? -ext.x : ext.x,
                        0f,
                        (i & 2) == 0 ? -ext.z : ext.z);
                    Vector3 local = camTransform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

                float halfW = (max.x - min.x) * 0.5f;
                float halfH = (max.y - min.y) * 0.5f;
                float projAspect = halfH > 0.001f ? halfW / halfH : 1f;
                projAspect = Mathf.Clamp(projAspect, 0.2f, 5f);
                width = Mathf.Max(8, Mathf.RoundToInt(height * projAspect));
                camera.aspect = (float)width / height;

                float fit = Mathf.Max(halfH, halfW / camera.aspect) * (1f + Mathf.Max(0f, margin));
                camera.orthographicSize = fit;

                // Recenter on the projected center so a pitched view does not clip top/bottom.
                Vector2 localCenter = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
                camTransform.position += camTransform.right * localCenter.x + camTransform.up * localCenter.y;
            }
            else
            {
                float aspect = bounds.size.z > 0.001f ? bounds.size.x / bounds.size.z : 1f;
                aspect = Mathf.Clamp(aspect, 0.2f, 5f);
                width = Mathf.Max(8, Mathf.RoundToInt(height * aspect));
                camera.aspect = (float)width / height;
            }

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture prevTarget = camera.targetTexture;
            CameraClearFlags prevClear = camera.clearFlags;
            Color prevBg = camera.backgroundColor;

            if (transparent)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = prevTarget;
                camera.clearFlags = prevClear;
                camera.backgroundColor = prevBg;
                camera.ResetAspect();
                RenderTexture.active = prevActive;

                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static bool EnsureEditorScene()
        {
            if (EditorSceneController.Instance != null) return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(EDITOR_SCENE_PATH, OpenSceneMode.Single);
            if (EditorSceneController.Instance == null)
            {
                Debug.LogError($"LevelCaptureExporter: EditorSceneController not found after opening {EDITOR_SCENE_PATH}.");
                return false;
            }
            return true;
        }
    }
}
