#if UNITY_EDITOR && using_addressable
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class AddressableTool
{
    [MenuItem("Tools/Addressables/Clean Prefab Extensions in Addressables")]
    public static void CleanAddresses()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        foreach (var group in settings.groups)
        {
            foreach (var entry in group.entries)
            {
                ReplacePathOfAddressableEntry(entry);
            }
        }

        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("✅ Cleaned .prefab extensions from Addressable addresses.");
    }

    private static void ReplacePathOfAddressableEntry(AddressableAssetEntry entry)
    {
        if (entry.address.EndsWith(".prefab"))
        {
            entry.SetAddress(entry.address.Replace(".prefab", ""));
        }

        if (entry.SubAssets == null || entry.SubAssets.Count == 0) return;
        foreach (var subAsset in entry.SubAssets)
        {
            ReplacePathOfAddressableEntry(subAsset);
        }
    }
}
#endif