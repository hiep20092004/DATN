using System.IO;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the panel prefabs that the code opens but that were never brought over from the shared project.
/// PanelManager resolves a panel by its type name through the [Panel] Resources loader, so a missing prefab
/// is a silent runtime dead end — <see cref="PopupRevive"/> in particular is opened on every loss.
/// </summary>
public static partial class WaterFlowClassicSetup
{
    private const string PanelResourcesFolder = "Assets/Resources/Panel";
    private const string RevivePopupPath = PanelResourcesFolder + "/PopupRevive.prefab";
    private const string SettingPopupPath = PanelResourcesFolder + "/PopupSetting.prefab";
    private const string LoseReasonConfigPath = "Assets/Project Files/Level System/LoseReasonConfig.asset";

    private const string OpenFadeTweenPath = "Assets/Services/Tween/[Open] Fade.asset";
    private const string OpenScaleTweenPath = "Assets/Services/Tween/[Open] Scale.asset";
    private const string CloseFadeTweenPath = "Assets/Services/Tween/[Close] Fade.asset";
    private const string CloseScaleTweenPath = "Assets/Services/Tween/[Close] Scale.asset";

    private static readonly Color PopupDimColor = new Color(0f, 0f, 0f, 0.8f);
    private static readonly Color32 PopupBodyColor = new Color32(0x1B, 0x2A, 0x41, 0xFF);
    private static readonly Color32 PopupAccentColor = new Color32(0x3C, 0xC8, 0x64, 0xFF);

    [MenuItem("WaterFlow/Setup/Create Missing Popups")]
    public static void CreateMissingPopups()
    {
        Directory.CreateDirectory(PanelResourcesFolder);

        bool revivePopupCreated = CreateRevivePopup();
        bool settingPopupWired = WireSettingPopupToggles();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WaterFlow Setup] Missing popups pass finished. PopupRevive: " +
                  (revivePopupCreated ? "created" : "already present") + ". PopupSetting toggles: " +
                  (settingPopupWired ? "wired" : "incomplete, see warnings") +
                  ". Assign real art to the Image/TMP fields when it is ready.");
    }

    /// <summary>Builds Resources/Panel/PopupRevive.prefab with grey-box art and every serialized field wired.</summary>
    private static bool CreateRevivePopup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RevivePopupPath))
        {
            return false;
        }

        GameObject root = NewUiObject("PopupRevive", null);
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<CanvasGroup>();

            Image dim = root.AddComponent<Image>();
            dim.color = PopupDimColor;
            dim.raycastTarget = true;

            GameObject container = NewUiObject("Container", root.transform);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            Center(containerRect, new Vector2(880f, 1000f));
            CanvasGroup containerGroup = container.AddComponent<CanvasGroup>();
            container.AddComponent<Image>().color = PopupBodyColor;

            Button closeButton = BuildIconButton(container.transform, "Close", new Vector2(-32f, -32f),
                Vector2.one, new Color32(0xFF, 0xFF, 0xFF, 0xCC));

            TextMeshProUGUI title = BuildLabel(container.transform, "Title", new Vector2(0f, 380f),
                new Vector2(760f, 110f), 72f);
            title.text = "Hết giờ!";

            GameObject icon = NewUiObject("Icon", container.transform);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            Center(iconRect, new Vector2(280f, 280f));
            iconRect.anchoredPosition = new Vector2(0f, 130f);
            Image iconImage = icon.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;

            TextMeshProUGUI moreTime = BuildLabel(container.transform, "More Time", new Vector2(0f, -70f),
                new Vector2(400f, 100f), 64f);
            moreTime.text = "+60 giây";
            moreTime.color = PopupAccentColor;

            TextMeshProUGUI description = BuildLabel(container.transform, "Description", new Vector2(0f, -210f),
                new Vector2(720f, 180f), 44f);
            description.text = "Tiếp tục chơi với {[value]} giây!";
            SetWrapping(description, true);

            Button coinButton = BuildCoinCostButton(container.transform, out TextMeshProUGUI coinCost);

            // Peek deliberately sits outside Container: the hold fades that CanvasGroup to alpha 0 and drops
            // its raycasts, so a button parented under it could never report the pointer-up.
            HoldButton holdButton = BuildHoldToPeekButton(root.transform);

            PopupRevive popup = root.AddComponent<PopupRevive>();
            var serialized = new SerializedObject(popup);
            serialized.FindProperty("CloseButton").objectReferenceValue = closeButton;
            serialized.FindProperty("CoinButton").objectReferenceValue = coinButton;
            serialized.FindProperty("HoldButton").objectReferenceValue = holdButton;
            serialized.FindProperty("CoinRequiredText").objectReferenceValue = coinCost;
            serialized.FindProperty("PopupCanvasGroup").objectReferenceValue = containerGroup;
            serialized.FindProperty("overlayDimImage").objectReferenceValue = dim;
            serialized.FindProperty("TitleText").objectReferenceValue = title;
            serialized.FindProperty("IconImage").objectReferenceValue = iconImage;
            serialized.FindProperty("MoreTimeText").objectReferenceValue = moreTime;
            serialized.FindProperty("DescriptionText").objectReferenceValue = description;
            serialized.FindProperty("loseReasonConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(LoseReasonConfigPath);

            FillCoinCostLadder(serialized);
            FillDefaultReviveConfig(serialized);
            FillTweens(serialized, root.GetComponent<RectTransform>(), containerRect);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RevivePopupPath);
            Debug.Log("[WaterFlow Setup] Created '" + RevivePopupPath + "'.");

            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// The coin price per revive within one run. PopupRevive clamps RevivedTime to this list, so an empty
    /// dictionary would throw on the first open.
    /// </summary>
    private static void FillCoinCostLadder(SerializedObject serialized)
    {
        int[] costs = { 200, 400, 800 };

        SerializedProperty keys = serialized.FindProperty("coinRequiredDict.m_Keys");
        SerializedProperty values = serialized.FindProperty("coinRequiredDict.m_Values");
        if (keys == null || values == null)
        {
            Debug.LogError("[WaterFlow Setup] PopupRevive.coinRequiredDict layout changed — coin costs not written.");
            return;
        }

        keys.arraySize = costs.Length;
        values.arraySize = costs.Length;

        for (int i = 0; i < costs.Length; i++)
        {
            keys.GetArrayElementAtIndex(i).intValue = i;

            SerializedProperty currency = values.GetArrayElementAtIndex(i);
            SerializedProperty currencyType = currency.FindPropertyRelative("currency");
            currencyType.enumValueIndex = GetEnumIndex(currencyType, nameof(GameResource.Coin));
            currency.FindPropertyRelative("value").intValue = costs[i];
        }
    }

    /// <summary>Fallback content used when a LoseReason has no entry in the config asset.</summary>
    private static void FillDefaultReviveConfig(SerializedObject serialized)
    {
        SerializedProperty defaultConfig = serialized.FindProperty("defaultConfig");
        defaultConfig.FindPropertyRelative("title").stringValue = "Hết lượt!";
        defaultConfig.FindPropertyRelative("descriptionFormat").stringValue = "Tiếp tục chơi và giữ nguyên tiến độ!";
        defaultConfig.FindPropertyRelative("continueSeconds").intValue = 0;
        defaultConfig.FindPropertyRelative("additionText").stringValue = string.Empty;
    }

    private static void FillTweens(SerializedObject serialized, RectTransform root, RectTransform container)
    {
        WriteTweenPair(serialized.FindProperty("openTween"), root, OpenFadeTweenPath, container, OpenScaleTweenPath);
        WriteTweenPair(serialized.FindProperty("closeTween"), root, CloseFadeTweenPath, container, CloseScaleTweenPath);
    }

    private static void WriteTweenPair(SerializedProperty tweens, RectTransform fadeTarget, string fadeConfigPath,
        RectTransform scaleTarget, string scaleConfigPath)
    {
        tweens.arraySize = 2;
        WriteTween(tweens.GetArrayElementAtIndex(0), fadeTarget, fadeConfigPath);
        WriteTween(tweens.GetArrayElementAtIndex(1), scaleTarget, scaleConfigPath);
    }

    private static void WriteTween(SerializedProperty tween, RectTransform target, string configPath)
    {
        var config = AssetDatabase.LoadAssetAtPath<TweenConfigSO>(configPath);
        if (!config)
        {
            Debug.LogWarning("[WaterFlow Setup] Tween config '" + configPath +
                             "' is missing — the popup will open instantly.");
        }

        tween.FindPropertyRelative("target").objectReferenceValue = target;
        tween.FindPropertyRelative("configSO").objectReferenceValue = config;
        tween.FindPropertyRelative("custom").boolValue = false;
    }

    /// <summary>
    /// Points PopupSetting's music/sound/vibrate rows at the buttons that already exist in the prefab art.
    /// The rows were drawn but never hooked up, so the three settings the popup advertises did nothing.
    /// </summary>
    private static bool WireSettingPopupToggles()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SettingPopupPath);
        if (!prefabRoot)
        {
            Debug.LogError("[WaterFlow Setup] Could not open '" + SettingPopupPath + "'.");
            return false;
        }

        try
        {
            var popup = prefabRoot.GetComponent<PopupSetting>();
            if (!popup)
            {
                Debug.LogError("[WaterFlow Setup] '" + SettingPopupPath + "' has no PopupSetting component.");
                return false;
            }

            var serialized = new SerializedObject(popup);
            bool wiredAll = WireSettingRow(serialized, prefabRoot, "MusicRow", "Music")
                            & WireSettingRow(serialized, prefabRoot, "SoundRow", "Sound")
                            & WireSettingRow(serialized, prefabRoot, "VibrateRow", "Vibrate");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, SettingPopupPath);

            return wiredAll;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool WireSettingRow(SerializedObject serialized, GameObject prefabRoot, string propertyName,
        string rowObjectName)
    {
        Transform row = FindDescendant(prefabRoot.transform, rowObjectName);
        if (!row)
        {
            Debug.LogWarning("[WaterFlow Setup] PopupSetting has no '" + rowObjectName + "' row — " + propertyName +
                             " left empty.");
            return false;
        }

        var button = row.GetComponentInChildren<Button>(true);
        Transform slash = FindDescendant(row, "Slash");
        if (!button)
        {
            Debug.LogWarning("[WaterFlow Setup] '" + rowObjectName + "' has no Button — " + propertyName +
                             " left empty.");
            return false;
        }

        SerializedProperty rowProperty = serialized.FindProperty(propertyName);
        rowProperty.FindPropertyRelative("button").objectReferenceValue = button;
        rowProperty.FindPropertyRelative("slash").objectReferenceValue = slash ? slash.gameObject : null;

        return true;
    }

    private static Transform FindDescendant(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != parent && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Button BuildCoinCostButton(Transform parent, out TextMeshProUGUI costLabel)
    {
        GameObject root = NewUiObject("Coin Button", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        Center(rect, new Vector2(600f, 170f));
        rect.anchoredPosition = new Vector2(0f, -370f);

        Image image = root.AddComponent<Image>();
        image.color = PopupAccentColor;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject icon = NewUiObject("Icon", root.transform);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        Center(iconRect, new Vector2(72f, 72f));
        iconRect.anchoredPosition = new Vector2(-140f, 0f);
        icon.AddComponent<Image>().color = new Color32(0xFF, 0xC8, 0x3C, 0xFF);

        costLabel = BuildLabel(root.transform, "Cost", new Vector2(40f, 0f), new Vector2(340f, 110f), 64f);
        costLabel.text = "200";

        return button;
    }

    private static HoldButton BuildHoldToPeekButton(Transform parent)
    {
        GameObject root = NewUiObject("Hold To Peek", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(560f, 120f);

        // HoldButton needs a Graphic to be hit-tested, but the bar itself should stay invisible over the art.
        Image image = root.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.0039f);
        image.raycastTarget = true;

        TextMeshProUGUI label = BuildLabel(root.transform, "Label", Vector2.zero, new Vector2(560f, 120f), 40f);
        label.text = "NHẤN GIỮ ĐỂ XEM";
        label.color = new Color(1f, 1f, 1f, 0.7f);

        return root.AddComponent<HoldButton>();
    }

    private static Button BuildIconButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 anchor,
        Color color)
    {
        GameObject root = NewUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(96f, 96f);

        Image image = root.AddComponent<Image>();
        image.color = color;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;

        return button;
    }

    private static TextMeshProUGUI BuildLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
        float fontSize)
    {
        GameObject root = NewUiObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        Center(rect, size);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        SetWrapping(text, false);

        return text;
    }

    private static void SetWrapping(TextMeshProUGUI text, bool wrap)
    {
#if UNITY_6000_0_OR_NEWER
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
        text.enableWordWrapping = wrap;
#endif
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        var created = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        if (parent)
        {
            created.transform.SetParent(parent, false);
        }

        return created;
    }
}
