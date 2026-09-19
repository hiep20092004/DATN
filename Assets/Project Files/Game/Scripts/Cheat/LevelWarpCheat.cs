// Dev tool. Compiled out of release player builds entirely — never ships.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using WaterFlow.Core;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.UserData;
using UnityEngine;
using UnityEngine.InputSystem;
using DOTween = DG.Tweening.DOTween;

namespace WaterFlow.Game
{
    /// <summary>
    /// Dev-only level warp: jump to any level index without replaying progression.
    ///
    /// Self-bootstraps from <see cref="RuntimeInitializeOnLoadMethod"/> so it needs no scene, prefab or
    /// service wiring — nothing in the shipped data references it, which is what keeps it safe to delete.
    /// Warping reuses the production paths (<see cref="ActiveSession.SetEditorLevelIndex"/> for the save
    /// write, <c>TransitionService.SwitchScene</c> for the reload) instead of poking LevelController
    /// directly, so a warped level boots through exactly the same flow as a normally reached one.
    ///
    /// Keys: F1 toggle panel · ] next · [ prev · \ reload · Ctrl+] / Ctrl+[ jump 10.
    /// </summary>
    public class LevelWarpCheat : MonoBehaviour
    {
        private const int JUMP_STRIDE = 10;
        private const float PANEL_WIDTH = 300f;

        /// <summary>
        /// Virtual screen height the overlay is laid out in. The default IMGUI skin is authored for ~1x
        /// desktop DPI and is unreadable on a phone-aspect game view or a 4K editor window, so everything
        /// is drawn through one scaled matrix rather than restyling each control.
        /// </summary>
        private const float REFERENCE_HEIGHT = 420f;

        private const string SCALE_STEP_PREFS_KEY = "Cheat.LevelWarp.ScaleStep";

        private static readonly float[] SCALE_STEPS = { 0.75f, 1f, 1.35f, 1.8f };
        private const int DEFAULT_SCALE_STEP = 1;

        /// <summary>Levels ported from FlowJam to demo one obstacle each — the DATN capture list.</summary>
        private const int DEMO_FIRST_LEVEL_NUMBER = 31;
        private const int DEMO_LAST_LEVEL_NUMBER = 41;

        private static LevelWarpCheat instance;

        private bool panelVisible;
        private string levelInput = string.Empty;
        private Vector2 demoScroll;
        private GUIStyle labelStyle;
        private int scaleStep = DEFAULT_SCALE_STEP;

        // Obstacle labels are derived from level data once per session: the level set changes often and a
        // hardcoded table would silently rot.
        private readonly Dictionary<int, string> obstacleLabelCache = new Dictionary<int, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance)
                return;

            var host = new GameObject(nameof(LevelWarpCheat));
            instance = host.AddComponent<LevelWarpCheat>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            // Lets SaveLevel() accept a lower level than the saved one, which is the whole point of a warp.
            UserDataService.CheatEnabled = true;

            scaleStep = Mathf.Clamp(
                PlayerPrefs.GetInt(SCALE_STEP_PREFS_KEY, DEFAULT_SCALE_STEP), 0, SCALE_STEPS.Length - 1);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                panelVisible = !panelVisible;

            if (keyboard.f2Key.wasPressedThisFrame)
                CycleScale();

            bool stride = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            int step = stride ? JUMP_STRIDE : 1;

            if (keyboard.rightBracketKey.wasPressedThisFrame)
                Warp(CurrentLevelIndex + step);
            else if (keyboard.leftBracketKey.wasPressedThisFrame)
                Warp(CurrentLevelIndex - step);
            else if (keyboard.backslashKey.wasPressedThisFrame)
                Warp(CurrentLevelIndex);
        }

        private void CycleScale()
        {
            scaleStep = (scaleStep + 1) % SCALE_STEPS.Length;
            PlayerPrefs.SetInt(SCALE_STEP_PREFS_KEY, scaleStep);
        }

        private void OnGUI()
        {
            // Closed panel draws nothing at all: the idle "[F1] level warp" hint sat on top of the
            // gameplay UI in dev builds, so the shortcut is documented on the class instead.
            if (!panelVisible)
                return;

            labelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = false };

            float scale = Screen.height / REFERENCE_HEIGHT * SCALE_STEPS[scaleStep];
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // Layout runs in virtual units; the matrix maps them onto real pixels.
            float virtualHeight = Screen.height / scale;

            GUILayout.BeginArea(new Rect(8f, 8f, PANEL_WIDTH, virtualHeight - 16f), GUI.skin.box);
            DrawHeader();
            DrawStepControls();
            DrawDirectJump();
            DrawDemoList();
            GUILayout.EndArea();

            GUI.matrix = previousMatrix;
        }

        private void DrawHeader()
        {
            int levelIndex = CurrentLevelIndex;
            GUILayout.Label($"LEVEL WARP — Level {levelIndex + 1:000} / {LevelCount}", labelStyle);
            GUILayout.Label(ObstacleLabel(levelIndex), labelStyle);
            GUILayout.Label($"F1 hide · F2 size {SCALE_STEPS[scaleStep]:0.##}x · ] [ warp · \\ reload",
                labelStyle);
            GUILayout.Space(4f);
        }

        private void DrawStepControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-10")) Warp(CurrentLevelIndex - JUMP_STRIDE);
            if (GUILayout.Button("-1")) Warp(CurrentLevelIndex - 1);
            if (GUILayout.Button("Reload")) Warp(CurrentLevelIndex);
            if (GUILayout.Button("+1")) Warp(CurrentLevelIndex + 1);
            if (GUILayout.Button("+10")) Warp(CurrentLevelIndex + JUMP_STRIDE);
            GUILayout.EndHorizontal();
        }

        private void DrawDirectJump()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Level nr", labelStyle, GUILayout.Width(52f));
            levelInput = GUILayout.TextField(levelInput, 4);
            if (GUILayout.Button("Go", GUILayout.Width(40f)) && int.TryParse(levelInput, out int levelNumber))
                Warp(levelNumber - 1);
            GUILayout.EndHorizontal();
        }

        private void DrawDemoList()
        {
            GUILayout.Space(6f);
            GUILayout.Label($"Obstacle demo levels {DEMO_FIRST_LEVEL_NUMBER}-{DEMO_LAST_LEVEL_NUMBER}", labelStyle);

            demoScroll = GUILayout.BeginScrollView(demoScroll);
            for (int levelNumber = DEMO_FIRST_LEVEL_NUMBER; levelNumber <= DEMO_LAST_LEVEL_NUMBER; levelNumber++)
            {
                int levelIndex = levelNumber - 1;
                if (levelIndex >= LevelCount)
                    break;

                if (GUILayout.Button($"{levelNumber:000}  {ObstacleLabel(levelIndex)}"))
                    Warp(levelIndex);
            }
            GUILayout.EndScrollView();
        }

        private static int CurrentLevelIndex => ActiveSession.Current.DisplayLevelIndex;

        private static int LevelCount
        {
            get
            {
                LevelDatabase database = ResolveLevelDatabase();
                return database ? Mathf.Max(database.AmountOfLevels, 1) : 1;
            }
        }

        private static LevelDatabase ResolveLevelDatabase()
        {
            LevelDatabase database = Services.GameplayConfig?.LevelDatabase;
            if (database)
                return database;

            LevelController levelController = FindFirstObjectByType<LevelController>();
            return levelController ? levelController.LevelDatabase : null;
        }

        private void Warp(int levelIndex)
        {
            int clamped = Mathf.Clamp(levelIndex, 0, LevelCount - 1);

            ActiveSession.SetEditorLevelIndex(clamped);
            SaveController.Save(forceSave: true);

            levelInput = (clamped + 1).ToString();
            Debug.Log($"[Cheat] Warp to Level {clamped + 1:000} ({ObstacleLabel(clamped)})");

            // Mirrors GameController.Replay(): kill tweens owned by the dying scene, then reload gameplay.
            DOTween.KillAll();
            Services.TransitionService.SwitchScene(GamePlacement.Game);
        }

        private string ObstacleLabel(int levelIndex)
        {
            if (obstacleLabelCache.TryGetValue(levelIndex, out string cached))
                return cached;

            string label = BuildObstacleLabel(levelIndex);
            obstacleLabelCache[levelIndex] = label;
            return label;
        }

        private static string BuildObstacleLabel(int levelIndex)
        {
#if !UNITY_EDITOR
            // Player builds stream levels through Addressables; loading a whole LevelData just to draw a
            // label is not worth it, and LevelDatabase.GetLevelDirectly is editor-only anyway.
            return string.Empty;
#else
            LevelDatabase database = ResolveLevelDatabase();
            if (!database || levelIndex >= database.AmountOfLevels)
                return "?";

            LevelData levelData = database.GetLevelDirectly(levelIndex);
            if (!levelData || levelData.Elements == null)
                return "?";

            var names = new SortedSet<string>();
            foreach (LevelElementData element in levelData.Elements)
            {
                switch (element)
                {
                    case BlockLevelElementData block when block.BlockEffects != null:
                        foreach (BlockEffectData effect in block.BlockEffects)
                            if (effect != null && effect.Type != BlockEffectType.None)
                                names.Add(effect.Type.ToString());
                        break;

                    case GateLevelElementData gate when gate.GateEffects != null:
                        foreach (GateEffectData effect in gate.GateEffects)
                            if (effect != null && effect.Type != GateEffectType.None)
                                names.Add(effect.Type.ToString());
                        break;

                    case InteractableObjectLevelElementData interactable
                        when interactable.InteractableObjectData != null &&
                             interactable.InteractableObjectData.Type != InteractableObjectType.None:
                        names.Add(interactable.InteractableObjectData.Type.ToString());
                        break;
                }
            }

            if (names.Count == 0)
                return "no obstacle";

            var builder = new StringBuilder();
            foreach (string name in names)
            {
                if (builder.Length > 0)
                    builder.Append('+');
                builder.Append(name);
            }

            return builder.ToString();
#endif
        }
    }
}
#endif
