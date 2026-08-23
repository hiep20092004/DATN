#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateCustomEnum
{
    private const string defaultEnumNameSpace = "WaterFlow.Enums";
    private static string templateScriptPath => $"{FrameworkWiz.RootDirectory}" + "/Templates/{0}Template.txt";
    private const string customEnumPath = "Scripts/Enums";
    private const string TEMPLATE_ENUM_NAME = "TEMPLATE_ENUM_NAME";


    public static void CheckCreateEnums()
    {
        CheckCreateEnum("AudioId");
        CheckCreateEnum("GameMode");
        CheckCreateEnum("GameResource");
#if !CUSTOM_ENUM
        FrameworkEditorHelper.AddSymbol(new[] { "CUSTOM_ENUM" }, BuildTargetGroup.Unknown);
#endif
    }

    private static void CheckCreateEnum(string enumName)
    {
        var isCustomEnumExists = IsCustomEnumScriptExists(enumName);
        if (!isCustomEnumExists) CreateCustomEnumScripts(enumName);
    }

    private static void CreateCustomEnumScripts(string EnumType)
    {
        var text = File.ReadAllText(Path.Combine(Application.dataPath, string.Format(templateScriptPath, EnumType)));
        var newText = text.Replace(TEMPLATE_ENUM_NAME, EnumType);
        SaveScript(EnumType, newText);
    }

    private static void SaveScript(string scriptName, string newText)
    {
        var direcPath = Path.Combine(Application.dataPath, customEnumPath);
        if (!Directory.Exists(direcPath))
            try
            {
                Directory.CreateDirectory(direcPath);
            }
            catch
            {
                Debug.LogError("Could not create directory: " + direcPath);
                return;
            }

        var scriptPath = Path.Combine(direcPath, $"{scriptName}.cs");
        File.WriteAllTextAsync(scriptPath, newText);
        AssetDatabase.ImportAsset(FileUtil.GetProjectRelativePath(scriptPath));
        //AssetDatabase.Refresh();
    }


    // public static bool IsCustomEnumScriptExists(string scriptName)
    // {
    //     //var scriptNameOrig = scriptName;
    //     scriptName = scriptName.ToLower();
    //     foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    //     foreach (var type in assembly.GetTypes())
    //         if (type.IsEnum)
    //             if (type.Name.ToLower().Equals(scriptName))
    //                 if (type.Namespace != defaultEnumNameSpace)
    //                     return true;
    //
    //     return false;
    // }
    
    public static bool IsCustomEnumScriptExists(string scriptName)
    {
        var direcPath = Path.Combine(Application.dataPath, customEnumPath, $"{scriptName}.cs");
        return File.Exists(direcPath);
    }
}
#endif