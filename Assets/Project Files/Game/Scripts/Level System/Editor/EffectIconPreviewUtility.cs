using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Shared editor utility for drawing small "effect type" preview thumbnails. Looks up a sprite
    /// in <see cref="ObstacleIconFolder"/> whose asset name matches the enum value name; falls back
    /// to drawing the underlying integer enum value when no sprite is found.
    /// </summary>
    internal static class EffectIconPreviewUtility
    {
        public const string ObstacleIconFolder = "Assets/Project Files/Game/Sprites/Obstacle_Icon/Editor";

        public const float PreviewSide = 22f;
        public const float PreviewPadding = 2f;

        private static readonly Color PreviewBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color PreviewBackgroundNone = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color PreviewBorder = new Color(0f, 0f, 0f, 0.45f);

        private static GUIStyle s_numberStyle;
        private static GUIStyle s_numberStyleSmall;

        private static Dictionary<string, Sprite> s_iconsByName;

        [InitializeOnLoadMethod]
        private static void RegisterCacheInvalidation()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged()
        {
            s_iconsByName = null;
        }

        public static bool TryGetIcon(string typeName, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(typeName))
                return false;

            EnsureIconCache();
            return s_iconsByName.TryGetValue(typeName, out sprite) && sprite != null;
        }

        private static void EnsureIconCache()
        {
            if (s_iconsByName != null)
                return;

            s_iconsByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { ObstacleIconFolder });
            if (guids == null)
                return;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;
                s_iconsByName[sprite.name] = sprite;
            }
        }

        /// <summary>Draws either the icon (if <paramref name="typeName"/> matches a sprite) or the integer value.</summary>
        public static void DrawEffectPreview(Rect outer, string typeName, int value, bool isNone)
        {
            if (!isNone && TryGetIcon(typeName, out Sprite sprite) && sprite != null && sprite.texture != null)
            {
                DrawSpritePreview(outer, sprite);
                return;
            }

            DrawNumberPreview(outer, value, isNone);
        }

        public static void DrawNumberPreview(Rect outer, int value, bool isNone)
        {
            EditorGUI.DrawRect(outer, isNone ? PreviewBackgroundNone : PreviewBackground);

            Rect inner = new Rect(
                outer.x + PreviewPadding,
                outer.y + PreviewPadding,
                outer.width - PreviewPadding * 2f,
                outer.height - PreviewPadding * 2f);

            const float t = 1f;
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, inner.width, t), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMax - t, inner.width, t), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, t, inner.height), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMax - t, inner.yMin, t, inner.height), PreviewBorder);

            EnsureNumberStyles();
            string text = value.ToString();
            GUIStyle style = text.Length >= 3 ? s_numberStyleSmall : s_numberStyle;
            GUI.Label(inner, text, style);
        }

        public static void DrawSpritePreview(Rect outer, Sprite sprite)
        {
            EditorGUI.DrawRect(outer, PreviewBackground);

            Rect inner = new Rect(
                outer.x + PreviewPadding,
                outer.y + PreviewPadding,
                outer.width - PreviewPadding * 2f,
                outer.height - PreviewPadding * 2f);

            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;
            if (spriteW <= 0f || spriteH <= 0f)
                return;

            float aspect = spriteW / spriteH;
            float targetW = inner.width;
            float targetH = inner.height;
            if (aspect >= 1f)
                targetH = inner.width / aspect;
            else
                targetW = inner.height * aspect;

            Rect drawRect = new Rect(
                inner.x + (inner.width - targetW) * 0.5f,
                inner.y + (inner.height - targetH) * 0.5f,
                targetW,
                targetH);

            Texture texture = sprite.texture;
            Rect texCoords = new Rect(
                sprite.rect.x / texture.width,
                sprite.rect.y / texture.height,
                sprite.rect.width / texture.width,
                sprite.rect.height / texture.height);

            GUI.DrawTextureWithTexCoords(drawRect, texture, texCoords, true);
        }

        private static void EnsureNumberStyles()
        {
            if (s_numberStyle != null)
                return;

            s_numberStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
            };

            s_numberStyleSmall = new GUIStyle(s_numberStyle)
            {
                fontSize = 10
            };
        }
    }
#endif
}
