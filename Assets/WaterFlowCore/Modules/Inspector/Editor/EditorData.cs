using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace WaterFlow.Core
{
    [HideScriptField]
    [CreateAssetMenu(fileName = "Editor Data", menuName = "Data/Core/Editor/Editor Data")]
    public class EditorData : ScriptableObject
    {
        [BoxGroup("Styles", "Styles")]
        [SerializeField] GUISkin defaultGUISkin;
        [BoxGroup("Styles")]
        [SerializeField] GUISkin proGUISkin;

        [Space]
        [BoxGroup("Styles")]
        [Tooltip("Custom Reference icons")]
        [SerializeField] Texture2D[] icons;

        [BoxGroup("Styles")]
        [SerializeField] Texture2D missingIcon;
        public Texture2D MissingIcon => missingIcon;

        [Space]
        [BoxGroup("Styles")]
        [Tooltip("Base paths to search for icons (checked in order)")]
        [SerializeField] string[] iconSearchPaths = new string[] 
        { 
            "",           // Root Editor Default Resources
            "System",     // System icons
            "Hierarchy"   // Hierarchy icons
        };

        [BoxGroup("Hierarchy", "Hierarchy")]
        [SerializeField] HierarchyItem[] hierarchyIcons;
        public HierarchyItem[] HierarchyIcons => hierarchyIcons;

        private Color defaultIconColor = Color.black;
        private Color darkIconColor = Color.white;

        private Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

        public Color IconColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                    return darkIconColor;

                return defaultIconColor;
            }
        }

        public GUISkin Skin
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                    return proGUISkin;

                return defaultGUISkin;
            }
        }

        /// <summary>
        /// Get icon by name. Supports both legacy array and EditorGUIUtility.Load()
        /// </summary>
        public Texture2D GetIcon(string name)
        {
            if (iconCache.TryGetValue(name, out Texture2D cachedIcon))
            {
                if (cachedIcon)
                    return cachedIcon;
            }

            if (icons != null)
            {
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] && icons[i].name == name)
                    {
                        iconCache[name] = icons[i];
                        return icons[i];
                    }
                }
            }

            Texture2D loadedIcon = LoadIconFromResources(name);
            if (loadedIcon)
            {
                iconCache[name] = loadedIcon;
                return loadedIcon;
            }

            if (missingIcon)
            {
                iconCache[name] = missingIcon;
                return missingIcon;
            }

            Texture2D builtInIcon = EditorGUIUtility.IconContent(name).image as Texture2D;
            if (builtInIcon)
            {
                iconCache[name] = builtInIcon;
                return builtInIcon;
            }

            return null;
        }

        /// <summary>
        /// Load icon from Editor Default Resources folder
        /// </summary>
        private Texture2D LoadIconFromResources(string iconName)
        {
            foreach (string basePath in iconSearchPaths)
            {
                string fullPath = string.IsNullOrEmpty(basePath) 
                    ? iconName 
                    : $"{basePath}/{iconName}";

                string[] extensions = { ".png", ".jpg", ".psd", "" };
                foreach (string ext in extensions)
                {
                    string pathWithExt = iconName.Contains(".") ? fullPath : fullPath + ext;
                    Texture2D icon = EditorGUIUtility.Load(pathWithExt) as Texture2D;
            
                    if (icon)
                        return icon;
                }
            }

            return null;
        }

        /// <summary>
        /// Load icon with custom path
        /// </summary>
        public Texture2D GetIconWithPath(string relativePath)
        {
            if (iconCache.TryGetValue(relativePath, out Texture2D cachedIcon))
            {
                if (cachedIcon)
                    return cachedIcon;
            }

            Texture2D icon = EditorGUIUtility.Load(relativePath) as Texture2D;
            if (icon)
            {
                iconCache[relativePath] = icon;
                return icon;
            }

            return missingIcon;
        }

        /// <summary>
        /// Clear icon cache
        /// </summary>
        public void ClearIconCache()
        {
            iconCache.Clear();
        }

        /// <summary>
        /// Preload Icons
        /// </summary>
        [Button("Preload Icons")]
        private void PreloadIcons()
        {
            iconCache.Clear();

            if (icons != null)
            {
                foreach (var icon in icons)
                {
                    if (icon)
                        iconCache[icon.name] = icon;
                }
            }

            Debug.Log($"Preloaded {iconCache.Count} icons into cache");
        }

        [System.Serializable]
        public class HierarchyItem
        {
            [SerializeField] string name;
            public string Name => name;

            [SerializeField] Texture texture;
            public Texture Texture => texture;
        }
    }
}