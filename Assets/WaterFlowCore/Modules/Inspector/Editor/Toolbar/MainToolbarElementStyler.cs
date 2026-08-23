#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WaterFlow.Core
{
    public static class MainToolbarElementStyler {
        /// <summary>Applies default compact styling for the Level load mode dropdown (path matches <see cref="MainToolbarSceneShortcuts.LevelLoaderModeToolbarRefreshPath"/>).</summary>
        public static void StyleLevelLoaderModeDropdown()
        {
            StyleElement<VisualElement>(MainToolbarSceneShortcuts.LevelLoaderModeToolbarRefreshPath, element =>
            {
                element.style.paddingLeft = 4f;
                element.style.paddingRight = 4f;
                element.style.marginLeft = 2f;
                element.style.marginRight = 2f;
            });
        }

        public static void StyleElement<T>(string elementName, System.Action<T> styleAction) where T : VisualElement {
            EditorApplication.delayCall += () => {
                ApplyStyle(elementName, (element) => {
                    T targetElement = element is T typedElement ? typedElement : element.Query<T>().First();
                    if (targetElement != null) {
                        styleAction(targetElement);
                    }
                });
            };
        }

        static void ApplyStyle(string elementName, System.Action<VisualElement> styleCallback) {
            var element = FindElementByName(elementName);
            if (element != null) {
                styleCallback(element);
            }
        }

        static VisualElement FindElementByName(string name) {
            var nameWithoutSpace = name.Replace(" ", "");
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows) {
                var root = window.rootVisualElement;
                if (root == null) continue;
            
                VisualElement element;
                if ((element = root.FindElementByName(name)) != null) return element;
                if ((element = root.FindElementByName(nameWithoutSpace)) != null) return element;
                if ((element = root.FindElementByTooltip(name)) != null) return element;
            }
            return null;
        }
    }
}

#endif