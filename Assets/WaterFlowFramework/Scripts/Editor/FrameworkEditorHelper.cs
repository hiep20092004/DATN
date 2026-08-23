#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FrameworkEditorHelper
{
    public static Type GetTypeFromAllAssemblies(string typeFullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeFullName);
            if (type != null)
                return type;
        }

        return null;
    }


    public static bool DotNETCoreCompat_IsAssignableFrom(Type to, Type from)
    {
#if NETFX_CORE
			if (!to.GetTypeInfo().IsAssignableFrom(from.Ge‌​tTypeInfo()))
#else
        if (!to.IsAssignableFrom(from))
#endif
            return false;

        return true;
    }

    public static void ShowNotification(string msg, bool beep, bool allowFocusedWindow)
    {
        ShowNotification(msg, beep, null, allowFocusedWindow);
    }

    public static void ShowNotification(string msg, bool beep, EditorWindow editorWindow, bool allowFocusedWindow)
    {
        if (!editorWindow)
            editorWindow = GetBestEditorWindowToShowNotification(allowFocusedWindow);

        if (!editorWindow)
            return;

        try
        {
            editorWindow.ShowNotification(new GUIContent(msg));
        }
        catch
        {
        }

        if (beep)
            try
            {
                EditorApplication.Beep();
            }
            catch
            {
            }
    }

    public static EditorWindow GetBestEditorWindowToShowNotification(bool allowFocusedWindow = true)
    {
        var editorWindow = EditorWindow.focusedWindow;
        if (!editorWindow)
        {
            editorWindow = EditorWindow.mouseOverWindow;
            if (!editorWindow)
                editorWindow = EditorWindow.GetWindow<SceneView>();
        }

        return editorWindow;
    }

    public static bool HasSymbol(string symbol, BuildTargetGroup buildTargetGroup)
    {
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';');
        for (var i = 0; i < symbols.Length; i++)
            if (symbols[i] == symbol)
                return true;

        return false;
    }


    public static void AddSymbol(string[] add_symbols, BuildTargetGroup buildTargetGroup)
    {
        if (buildTargetGroup == BuildTargetGroup.Unknown)
        {
            AddSymbolForBuildTarget(add_symbols, BuildTargetGroup.Standalone);
            AddSymbolForBuildTarget(add_symbols, BuildTargetGroup.Android);
        }
        else
        {
            AddSymbolForBuildTarget(add_symbols, buildTargetGroup);
        }
    }

    private static void AddSymbolForBuildTarget(string[] add_symbols, BuildTargetGroup buildTargetGroup)
    {
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';');
        var newSymbols = new List<string>(symbols);
        foreach (var symbol in add_symbols)
            if (!newSymbols.Contains(symbol))
                newSymbols.Add(symbol);
        if (symbols.Length == newSymbols.Count) return;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newSymbols.ToArray());
    }
}

public static class FrameworkWiz
{
    public const string FILE_TO_DETECT = "WATERFLOW_FRAMEWORK_HELLO.txt";
    public const string TEMPLATE_PANEL_NAME = "TEMPLATE_PANEL_NAME";
    public static string panelPrefabBasePath => $"Assets/{RootDirectory}/Prefabs/UI/PanelBase.prefab";
    public static string templateUIPath => $"{RootDirectory}/Templates/UI";
    public static string templateScriptPath => $"{RootDirectory}/Templates/PanelTemplate.txt";
    public static string createPanelDataPath = $"Assets/{RootDirectory}/Templates/CreatePanelData.asset";
    private static string rootDirectory;
    public static string RootDirectory => rootDirectory ??= GetRootDirectory();

    public static string GetRootDirectory()
    {
        string[] res = Directory.GetFiles(Application.dataPath, FILE_TO_DETECT, SearchOption.AllDirectories);
        if (res.Length == 0)
        {
            Debug.LogError("error message ....");
        }

        var root = res[0].Split('\\')[^2];
        return root;
    }
}

#endif