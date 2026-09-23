using System.Collections.Generic;
using System.IO;
using WaterFlow.Core;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ConfigManagement;
using WaterFlow.Framework.Systems.LoadObject;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class WaterFlowClassicSetup
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string LoadingScenePath = "Assets/Scenes/Loading.unity";
    private const string HomeScenePath = "Assets/Scenes/Home.unity";
    private const string ServiceManagerPath = "Assets/WaterFlowFramework/Templates/ServicesSO/[WATERFLOW] SERVICE MANAGER.asset";
    private const string ConfigServicePath = "Assets/WaterFlowFramework/Templates/ServicesSO/DefaultConfigService.asset";
    private const string PanelLoadServicePath = "Assets/WaterFlowFramework/Templates/ServicesSO/SaveLoadObject/Load Resources Async  [Panel].asset";
    private const string LevelDatabasePath = "Assets/Project Files/Level System/LevelDatabase.asset";
    private const string EnvironmentDataPath = "Assets/Project Files/Level System/EnvironmentData.asset";
    private const string BlockConfigPath = "Assets/Project Files/Data/Block/BlockConfig.asset";
    private const string CameraConfigPath = "Assets/Project Files/Data/CameraControllerConfig.asset";

    [MenuItem("WaterFlow/Setup/Create Classic Bootstrap Scene")]
    public static void CreateClassicBootstrapScene()
    {
        Scene scene = OpenGameScene();

        EnsureMainCamera();
        EnsureEventSystem();
        EnsureGameSystem();
        EnsurePanelCanvas();
        EnsureTransitionCanvas();
        UIController uiController = EnsureGameUiCanvas();
        EnsureGameplayHolder(uiController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WaterFlow Setup] Classic bootstrap scene created. Run WaterFlow/Setup/Validate Classic Setup for the remaining art/prefab checklist.");
    }

    [MenuItem("WaterFlow/Setup/Validate Classic Setup")]
    public static void ValidateClassicSetup()
    {
        var issues = new List<string>();

        RequireAsset<ServicesManager>(ServiceManagerPath, issues);
        RequireAsset<ConfigService>(ConfigServicePath, issues);
        RequireAsset<LoadObjectServiceAsync>(PanelLoadServicePath, issues);
        RequireAsset<LevelDatabase>(LevelDatabasePath, issues);
        RequireAsset<EnvironmentData>(EnvironmentDataPath, issues);
        RequireAsset<BlockConfig>(BlockConfigPath, issues);

        ValidateServiceManager(issues);
        ValidateEnvironmentData(issues);
        ValidateBlockVisuals(issues);
        ValidateSceneObjects(issues);

        if (issues.Count == 0)
        {
            Debug.Log("[WaterFlow Setup] Classic setup validation passed for foundation objects. Next validation target: imported block/gate/popup art prefabs.");
            return;
        }

        Debug.LogWarning("[WaterFlow Setup] Classic setup still needs attention:\n- " + string.Join("\n- ", issues));
    }

    private static Scene OpenGameScene()
    {
        if (!File.Exists(GameScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameScenePath) ?? "Assets/Scenes");
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, GameScenePath);
            return newScene;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == GameScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }

    private static Camera EnsureMainCamera()
    {
        Camera camera = Camera.main;
        if (!camera)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.17f, 0.22f, 1f);

        CameraController cameraController = camera.GetComponent<CameraController>();
        if (!cameraController)
        {
            cameraController = Undo.AddComponent<CameraController>(camera.gameObject);
        }

        Object cameraConfig = AssetDatabase.LoadAssetAtPath<Object>(CameraConfigPath);
        if (cameraConfig)
        {
            SetObjectReference(cameraController, "config", cameraConfig);
        }

        return camera;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (!eventSystem)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

#if MODULE_INPUT_SYSTEM
        if (!eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
        {
            Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(eventSystem.gameObject);
        }
#else
        if (!eventSystem.GetComponent<StandaloneInputModule>())
        {
            Undo.AddComponent<StandaloneInputModule>(eventSystem.gameObject);
        }
#endif
    }

    private static void EnsureGameSystem()
    {
        GameSystem gameSystem = Object.FindFirstObjectByType<GameSystem>();
        if (!gameSystem)
        {
            GameObject gameSystemObject = new GameObject("[WaterFlow] GameSystem");
            Undo.RegisterCreatedObjectUndo(gameSystemObject, "Create GameSystem");
            gameSystem = gameSystemObject.AddComponent<GameSystem>();
        }

        ServicesManager serviceManager = AssetDatabase.LoadAssetAtPath<ServicesManager>(ServiceManagerPath);
        ConfigService configService = AssetDatabase.LoadAssetAtPath<ConfigService>(ConfigServicePath);

        var serializedObject = new SerializedObject(gameSystem);
        serializedObject.FindProperty("serviceManager").objectReferenceValue = serviceManager;
        serializedObject.FindProperty("autoInit").boolValue = true;
        serializedObject.FindProperty("delayToInit").floatValue = 0.1f;
        SetServiceReference(serializedObject.FindProperty("configService"), configService);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameSystem);
    }

    private static PanelManager EnsurePanelCanvas()
    {
        PanelManager panelManager = Object.FindFirstObjectByType<PanelManager>();
        GameObject panelCanvasObject = GetOrCreateUiRoot("[WaterFlow] Panel Canvas", panelManager);

        EnsureCanvasStack(panelCanvasObject, 100);
        panelManager = panelCanvasObject.GetComponent<PanelManager>();
        if (!panelManager)
        {
            panelManager = Undo.AddComponent<PanelManager>(panelCanvasObject);
        }

        LoadObjectServiceAsync panelLoadService = AssetDatabase.LoadAssetAtPath<LoadObjectServiceAsync>(PanelLoadServicePath);
        var serializedObject = new SerializedObject(panelManager);
        SetServiceReference(serializedObject.FindProperty("loadObjectServiceAsync"), panelLoadService);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panelManager);

        return panelManager;
    }

    private static void EnsureTransitionCanvas()
    {
        BackgroundTransition transition = Object.FindFirstObjectByType<BackgroundTransition>();
        GameObject root = GetOrCreateUiRoot("[WaterFlow] Transition Canvas", transition);

        EnsureCanvasStack(root, 500);
        transition = root.GetComponent<BackgroundTransition>();
        if (!transition)
        {
            transition = Undo.AddComponent<BackgroundTransition>(root);
        }

        GameObject panel = root.transform.Find("Transition Panel")?.gameObject;
        if (!panel)
        {
            panel = new GameObject("Transition Panel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panel, "Create Transition Panel");
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Stretch(panelRect);
            panel.AddComponent<Image>().color = Color.black;
            panel.AddComponent<CanvasGroup>();
        }

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (!canvasGroup)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        var serializedObject = new SerializedObject(transition);
        serializedObject.FindProperty("transitionPanel").objectReferenceValue = panel;
        serializedObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedObject.FindProperty("tooltipProviders").arraySize = 0;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(transition);
    }

    private static UIController EnsureGameUiCanvas()
    {
        UIController uiController = Object.FindFirstObjectByType<UIController>();
        GameObject root = GetOrCreateUiRoot("[WaterFlow] Game UI Canvas", uiController);

        EnsureCanvasStack(root, 0);
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (!canvasGroup)
        {
            canvasGroup = root.AddComponent<CanvasGroup>();
        }

        uiController = root.GetComponent<UIController>();
        if (!uiController)
        {
            uiController = Undo.AddComponent<UIController>(root);
        }

        var serializedObject = new SerializedObject(uiController);
        SerializedProperty safePanels = serializedObject.FindProperty("notchSaveArea").FindPropertyRelative("safePanels");
        safePanels.arraySize = 0;
        SerializedProperty pages = serializedObject.FindProperty("cachedPages").FindPropertyRelative("pages");
        pages.arraySize = 0;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(uiController);

        return uiController;
    }

    private static void EnsureGameplayHolder(UIController uiController)
    {
        GameObject holder = FindSceneObjectByName("[WaterFlow] Gameplay");
        if (!holder)
        {
            holder = new GameObject("[WaterFlow] Gameplay");
            Undo.RegisterCreatedObjectUndo(holder, "Create Gameplay Holder");
        }

        LevelController levelController = holder.GetComponent<LevelController>();
        if (!levelController)
        {
            levelController = Undo.AddComponent<LevelController>(holder);
        }

        GameController gameController = holder.GetComponent<GameController>();
        if (!gameController)
        {
            gameController = Undo.AddComponent<GameController>(holder);
        }

        LevelDatabase levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabasePath);
        EnvironmentData environmentData = AssetDatabase.LoadAssetAtPath<EnvironmentData>(EnvironmentDataPath);
        BlockConfig blockConfig = AssetDatabase.LoadAssetAtPath<BlockConfig>(BlockConfigPath);

        var levelSo = new SerializedObject(levelController);
        levelSo.FindProperty("environmentData").objectReferenceValue = environmentData;
        levelSo.FindProperty("levelDatabase").objectReferenceValue = levelDatabase;
        levelSo.FindProperty("blockConfig").objectReferenceValue = blockConfig;
        levelSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(levelController);

        var gameSo = new SerializedObject(gameController);
        gameSo.FindProperty("uiController").objectReferenceValue = uiController;
        gameSo.FindProperty("levelDatabase").objectReferenceValue = levelDatabase;
        gameSo.FindProperty("MainUI").objectReferenceValue = uiController.GetComponent<CanvasGroup>();
        gameSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameController);

        holder.SetActive(false);
        gameController.enabled = false;
        Debug.LogWarning("[WaterFlow Setup] Gameplay holder was created inactive. Enable it after importing/configuring UIGame, popup prefabs, block visuals, and environment prefabs.");
    }

    private static void ValidateServiceManager(List<string> issues)
    {
        ServicesManager serviceManager = AssetDatabase.LoadAssetAtPath<ServicesManager>(ServiceManagerPath);
        if (!serviceManager)
        {
            return;
        }

        var serializedObject = new SerializedObject(serviceManager);
        SerializedProperty services = serializedObject.FindProperty("servicesObject");
        for (int i = 0; i < services.arraySize; i++)
        {
            if (!services.GetArrayElementAtIndex(i).objectReferenceValue)
            {
                issues.Add($"Service Manager entry #{i} is missing.");
            }
        }
    }

    private static void ValidateEnvironmentData(List<string> issues)
    {
        EnvironmentData environmentData = AssetDatabase.LoadAssetAtPath<EnvironmentData>(EnvironmentDataPath);
        if (!environmentData)
        {
            return;
        }

        var serializedObject = new SerializedObject(environmentData);
        RequireReference(serializedObject, "gatePrefab", issues, "EnvironmentData gate prefab is missing. Import/configure gate model prefab here.");
        RequireReference(serializedObject, "innerObstaclePrefab", issues, "EnvironmentData inner obstacle prefab is missing. Import/configure obstacle/blocker model prefab here.");
        RequireReference(serializedObject, "innerTile1Prefab", issues, "EnvironmentData inner tile prefab is missing. Import/configure board tile art here.");
        RequireReference(serializedObject, "innerTile2Prefab", issues, "EnvironmentData alternate inner tile prefab is missing. Import/configure board tile art here.");
        RequireReference(serializedObject, "borderInnerGround", issues, "EnvironmentData border/ground prefab is missing. Import/configure border model prefab here.");
    }

    private static void ValidateBlockVisuals(List<string> issues)
    {
        string[] visualGuids = AssetDatabase.FindAssets("t:BlocksVisualsData");
        if (visualGuids.Length == 0)
        {
            issues.Add("No BlocksVisualsData asset found. This is the main art/model blocker for spawning visible blocks.");
            return;
        }

        foreach (string guid in visualGuids)
        {
            BlocksVisualsData data = AssetDatabase.LoadAssetAtPath<BlocksVisualsData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!data)
            {
                issues.Add($"BlocksVisualsData at guid {guid} could not be loaded.");
            }
        }
    }

    private static void ValidateSceneObjects(List<string> issues)
    {
        if (!Object.FindFirstObjectByType<GameSystem>())
        {
            issues.Add("Game scene has no GameSystem. Run Create Classic Bootstrap Scene.");
        }

        if (!Object.FindFirstObjectByType<PanelManager>())
        {
            issues.Add("Game scene has no PanelManager. Run Create Classic Bootstrap Scene.");
        }

        GameController gameController = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (!gameController)
        {
            issues.Add("Game scene has no GameController holder. Run Create Classic Bootstrap Scene.");
        }
        else if (!gameController.enabled)
        {
            issues.Add("Gameplay holder/GameController is disabled until UIGame/popup/block visual prefabs are imported and assigned.");
        }
    }

    private static T RequireAsset<T>(string path, List<string> issues) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset)
        {
            issues.Add($"Missing required asset: {path}");
        }

        return asset;
    }

    private static void RequireReference(SerializedObject serializedObject, string propertyName, List<string> issues, string message)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && !property.objectReferenceValue)
        {
            issues.Add(message);
        }
    }

    private static void EnsureCanvasStack(GameObject root, int sortingOrder)
    {
        RectTransform rectTransform = root.GetComponent<RectTransform>();
        if (!rectTransform)
        {
            Debug.LogError($"[WaterFlow Setup] '{root.name}' must be a UI root with RectTransform. Delete it and run setup again.");
            return;
        }

        Stretch(rectTransform);

        Canvas canvas = root.GetComponent<Canvas>();
        if (!canvas)
        {
            canvas = root.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (!scaler)
        {
            scaler = root.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (!root.GetComponent<GraphicRaycaster>())
        {
            root.AddComponent<GraphicRaycaster>();
        }
    }

    private static GameObject GetOrCreateUiRoot(string rootName, Component existingComponent)
    {
        GameObject root = existingComponent ? existingComponent.gameObject : FindSceneObjectByName(rootName);
        if (root && !root.GetComponent<RectTransform>())
        {
            Undo.DestroyObjectImmediate(root);
            root = null;
        }

        if (root)
        {
            return root;
        }

        root = new GameObject(rootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, $"Create {rootName}");
        return root;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.hideFlags != HideFlags.None) continue;
            if (!transform.gameObject.scene.IsValid()) continue;
            if (transform.name == objectName) return transform.gameObject;
        }

        return null;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetObjectReference(Object target, string propertyName, Object reference)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = reference;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetServiceReference(SerializedProperty property, Object reference)
    {
        if (property == null)
        {
            return;
        }

        property.FindPropertyRelative("referenceType").enumValueIndex = (int)ReferenceType.Manual;
        property.FindPropertyRelative("reference").objectReferenceValue = reference;
    }

    /// <summary>
    /// Cleans a scene imported from the shared FlowJam project: strips components whose script was deliberately
    /// not ported (I2 Localization, pre-boosters, tutorials) and drops prefab instances whose asset was left
    /// behind, so the scene stops reporting references this project intentionally does not have.
    /// </summary>
    [MenuItem("WaterFlow/Setup/Fix Imported Loading Scene")]
    public static void FixImportedLoadingScene()
    {
        // Only the Loading scene owns the transition canvas: BaseTransition marks itself
        // DontDestroyOnLoad, so Home and Game reuse that same instance.
        CleanImportedScene(LoadingScenePath, rebuildTransitionCanvas: true);
    }

    [MenuItem("WaterFlow/Setup/Fix Imported Game Scene")]
    public static void FixImportedGameScene()
    {
        CleanImportedScene(GameScenePath, rebuildTransitionCanvas: false);
    }

    private static void CleanImportedScene(string scenePath, bool rebuildTransitionCanvas)
    {
        Scene scene = SceneManager.GetActiveScene().path == scenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int missingScripts = 0;
        var brokenInstances = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject target = child.gameObject;

                if (PrefabUtility.IsPrefabAssetMissing(target))
                {
                    // Only the outermost broken object needs destroying; the rest of the subtree goes with it.
                    if (!IsInsideMissingPrefab(target.transform.parent))
                    {
                        brokenInstances.Add(target);
                    }

                    continue;
                }

                missingScripts += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            }
        }

        foreach (GameObject brokenInstance in brokenInstances)
        {
            Debug.LogWarning($"[WaterFlow Setup] Removing '{brokenInstance.name}': its prefab asset was not ported.");
            Undo.DestroyObjectImmediate(brokenInstance);
        }

        if (rebuildTransitionCanvas)
        {
            EnsureTransitionCanvas();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[WaterFlow Setup] '{scene.name}' cleaned: removed {missingScripts} missing-script component(s) " +
                  $"and {brokenInstances.Count} orphaned prefab instance(s)." +
                  (rebuildTransitionCanvas ? " Transition canvas rebuilt." : string.Empty));
    }

    /// <summary>
    /// Builds the Home scene: panel canvas, background, coin bar, settings button and the centred play button.
    /// PanelManager is a plain scene singleton (no DontDestroyOnLoad), so every scene that opens a popup —
    /// Home included, for the settings popup — needs its own instance.
    /// </summary>
    [MenuItem("WaterFlow/Setup/Create Home Scene")]
    public static void CreateHomeScene()
    {
        Scene scene = SceneManager.GetActiveScene().path == HomeScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);

        EnsurePanelCanvas();

        GameObject canvasRoot = GetOrCreateUiRoot("[WaterFlow] Home UI Canvas", Object.FindFirstObjectByType<UIController>());
        EnsureCanvasStack(canvasRoot, 0);

        UIController uiController = canvasRoot.GetComponent<UIController>();
        if (!uiController)
        {
            uiController = Undo.AddComponent<UIController>(canvasRoot);
        }

        HomeController homeController = canvasRoot.GetComponent<HomeController>();
        if (!homeController)
        {
            homeController = Undo.AddComponent<HomeController>(canvasRoot);
        }

        SetObjectReference(homeController, "uiController", uiController);

        // UIController collects pages from its direct children, and UIPage requires its own canvas.
        GameObject pageObject = EnsureChild(canvasRoot.transform, "UIHome");
        EnsureCanvasStack(pageObject, 0);

        UIHome page = pageObject.GetComponent<UIHome>();
        if (!page)
        {
            page = Undo.AddComponent<UIHome>(pageObject);
        }

        GameObject background = EnsureChild(pageObject.transform, "Background");
        Image backgroundImage = EnsureComponent<Image>(background);
        backgroundImage.color = new Color32(0x1B, 0x2A, 0x41, 0xFF);

        GameObject safeArea = EnsureChild(pageObject.transform, "Safe Area");

        GameObject coinBar = BuildCoinBar(safeArea.transform);
        Button settingButton = BuildSettingButton(safeArea.transform);
        Button playButton = BuildPlayButton(safeArea.transform);
        TextMeshProUGUI levelLabel = BuildLevelLabel(safeArea.transform);

        var pageSerialized = new SerializedObject(page);
        pageSerialized.FindProperty("safeAreaRectTransform").objectReferenceValue = safeArea.GetComponent<RectTransform>();
        pageSerialized.FindProperty("background").objectReferenceValue = backgroundImage;
        pageSerialized.FindProperty("levelText").objectReferenceValue = levelLabel;
        pageSerialized.FindProperty("PlayBtn").objectReferenceValue = playButton;
        pageSerialized.FindProperty("SettingBtn").objectReferenceValue = settingButton;
        pageSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[WaterFlow Setup] Home scene built: background, coin bar ({coinBar.name}), level label, settings and " +
                  "play button. Assign real art to the Image/TMP fields when it is ready.");
    }

    private static GameObject BuildCoinBar(Transform parent)
    {
        GameObject root = EnsureChild(parent, "Coin Bar");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -40f);
        rect.sizeDelta = new Vector2(300f, 96f);

        EnsureComponent<Image>(root).color = new Color32(0x00, 0x00, 0x00, 0x80);

        GameObject icon = EnsureChild(root.transform, "Icon");
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        iconRect.sizeDelta = new Vector2(64f, 64f);
        Image iconImage = EnsureComponent<Image>(icon);
        iconImage.color = new Color32(0xFF, 0xC8, 0x3C, 0xFF);

        GameObject label = EnsureChild(root.transform, "Value");
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(96f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);
        TextMeshProUGUI valueText = EnsureComponent<TextMeshProUGUI>(label);
        valueText.text = "0";
        valueText.fontSize = 48f;
        valueText.alignment = TextAlignmentOptions.Left;

        UICurrency currency = EnsureComponent<UICurrency>(root);
        var serialized = new SerializedObject(currency);
        SerializedProperty resourceProperty = serialized.FindProperty("resource");
        resourceProperty.enumValueIndex = GetEnumIndex(resourceProperty, nameof(GameResource.Coin));
        serialized.FindProperty("icon").objectReferenceValue = iconImage;
        serialized.FindProperty("txtValue").objectReferenceValue = valueText;
        serialized.FindProperty("scaleObject").objectReferenceValue = root.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(currency);

        return root;
    }

    private static Button BuildSettingButton(Transform parent)
    {
        GameObject root = EnsureChild(parent, "Setting Button");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(96f, 96f);

        Image image = EnsureComponent<Image>(root);
        image.color = new Color32(0xFF, 0xFF, 0xFF, 0xCC);

        Button button = EnsureComponent<Button>(root);
        button.targetGraphic = image;

        return button;
    }

    private static Button BuildPlayButton(Transform parent)
    {
        GameObject root = EnsureChild(parent, "Play Button");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(480f, 160f);

        Image image = EnsureComponent<Image>(root);
        image.color = new Color32(0x3C, 0xC8, 0x64, 0xFF);

        Button button = EnsureComponent<Button>(root);
        button.targetGraphic = image;

        GameObject label = EnsureChild(root.transform, "Label");
        Stretch(label.GetComponent<RectTransform>());
        TextMeshProUGUI labelText = EnsureComponent<TextMeshProUGUI>(label);
        labelText.text = "PLAY";
        labelText.fontSize = 72f;
        labelText.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private static TextMeshProUGUI BuildLevelLabel(Transform parent)
    {
        GameObject root = EnsureChild(parent, "Level Label");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // Sits directly above the play button, whose half-height is 80.
        rect.anchoredPosition = new Vector2(0f, 150f);
        rect.sizeDelta = new Vector2(600f, 100f);

        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(root);
        text.text = "Level 1";
        text.fontSize = 56f;
        text.alignment = TextAlignmentOptions.Center;

        return text;
    }

    private static GameObject EnsureChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(childName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
        child.transform.SetParent(parent, false);
        Stretch(child.GetComponent<RectTransform>());

        return child;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        return component ? component : Undo.AddComponent<T>(target);
    }

    /// <summary>Maps an enum value name to its index in the serialized property, which is order-based.</summary>
    private static int GetEnumIndex(SerializedProperty property, string valueName)
    {
        for (int i = 0; i < property.enumNames.Length; i++)
        {
            if (property.enumNames[i] == valueName)
            {
                return i;
            }
        }

        Debug.LogError($"[WaterFlow Setup] Enum value '{valueName}' not found on {property.propertyPath}.");

        return property.enumValueIndex;
    }

    private const string LevelPanelPrefabPath = "Assets/Resources/UI/Level Panel.prefab";

    /// <summary>

    /// <summary>
    /// Strips the per-difficulty visuals from the level panel. LevelPanel no longer switches art by
    /// <c>LevelType</c>, so these objects can never be activated again — they only keep their art alive
    /// as references. Normal-level visuals are left untouched.
    /// </summary>
    /// <summary>
    /// Removes components whose script was never ported (I2 Localization) from the UI prefabs brought over from
    /// the shared project. They are harmless at runtime but log "the referenced script is missing" for every
    /// instance, which buries real errors.
    /// </summary>
    [MenuItem("WaterFlow/Setup/Strip Missing Scripts In Resources Prefabs")]
    public static void StripMissingScriptsInResourcesPrefabs()
    {
        int strippedComponents = 0;
        int touchedPrefabs = 0;

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                int removed = 0;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }

                if (removed == 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                strippedComponents += removed;
                touchedPrefabs++;
                Debug.Log($"[WaterFlow Setup] {Path.GetFileName(path)}: removed {removed} missing-script component(s).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WaterFlow Setup] Missing scripts stripped: {strippedComponents} component(s) across {touchedPrefabs} prefab(s).");
    }

    [MenuItem("WaterFlow/Setup/Strip Level Difficulty Visuals")]
    public static void StripLevelDifficultyVisuals()
    {
        string[] difficultyObjects = { "BackgroundHard", "BackgroundSuperHard", "Level Difficulty Anim" };

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(LevelPanelPrefabPath);
        if (!prefabRoot)
        {
            Debug.LogError($"[WaterFlow Setup] Could not open '{LevelPanelPrefabPath}'.");
            return;
        }

        try
        {
            int removed = 0;
            foreach (string objectName in difficultyObjects)
            {
                foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child && child.name == objectName)
                    {
                        Debug.Log($"[WaterFlow Setup] Removing '{objectName}' from the level panel.");
                        Object.DestroyImmediate(child.gameObject);
                        removed++;
                        break;
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, LevelPanelPrefabPath);
            Debug.Log($"[WaterFlow Setup] Level panel stripped: {removed} difficulty object(s) removed. " +
                      "Check the panel for any remaining difficulty-only child (e.g. a mode label) and delete it by hand.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool IsInsideMissingPrefab(Transform parent)
    {
        for (Transform current = parent; current; current = current.parent)
        {
            if (PrefabUtility.IsPrefabAssetMissing(current.gameObject))
            {
                return true;
            }
        }

        return false;
    }
}
