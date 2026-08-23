#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Reusable radial ("pie") menu for Unity Scene view tools. It is rendered and driven entirely from
    /// <see cref="SceneView.duringSceneGui"/>: call <see cref="Open"/> to show it at a screen point with a set
    /// of <see cref="Item"/>s, then forward the scene GUI loop to <see cref="OnGUI"/> while <see cref="IsOpen"/>.
    ///
    /// Selection is angle-based (wheel feel): the sector nearest the cursor angle is highlighted, and a left
    /// click invokes that sector's callback. Right click / Escape / clicking the dead-zone cancels.
    ///
    /// All geometry and hit-testing use GUI points (the same space as <see cref="Event.mousePosition"/>), so the
    /// menu is DPI-safe and needs no pixel/point conversion.
    /// </summary>
    public class ScenePieMenu
    {
        public sealed class Item
        {
            public string Label;
            public Texture Icon;
            public Color AccentColor = new Color(0.3f, 0.6f, 0.9f);
            public Action OnSelected;
        }

        private const float OUTER_RADIUS = 96f;
        private const float INNER_RADIUS = 40f;
        private const float MID_RADIUS = 70f;
        private const float ICON_SIZE = 34f;
        private const int TEX_SIZE = 256;
        private const float INNER_RATIO = INNER_RADIUS / OUTER_RADIUS;

        private static Texture2D discTexture;
        private static readonly Dictionary<int, Texture2D> wedgeTextureByCount = new Dictionary<int, Texture2D>();

        private GUIStyle labelStyle;
        private GUIStyle hoverLabelStyle;

        private readonly List<Item> items = new List<Item>();
        private Vector2 center;
        private int lastHoveredIndex = -1;

        public bool IsOpen { get; private set; }

        public void Open(Vector2 screenCenter, IReadOnlyList<Item> menuItems)
        {
            items.Clear();
            if (menuItems != null)
            {
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i] != null)
                        items.Add(menuItems[i]);
                }
            }

            if (items.Count == 0)
            {
                IsOpen = false;
                return;
            }

            center = screenCenter;
            lastHoveredIndex = -1;
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            items.Clear();
        }

        /// <summary>
        /// Draws the menu and processes input for the current scene GUI event. Returns true while the menu is
        /// open so the caller can short-circuit its own scene handling. Invokes a selected item's callback
        /// (after closing) on a left click over a sector.
        /// </summary>
        public bool OnGUI(SceneView view)
        {
            if (!IsOpen)
                return false;

            EnsureStyles();
            Event e = Event.current;

            Handles.BeginGUI();
            DrawMenu(e);
            Handles.EndGUI();

            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    view.Repaint();
                    e.Use();
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape)
                    {
                        Close();
                        e.Use();
                        view.Repaint();
                    }
                    break;

                case EventType.MouseDown:
                {
                    int index = GetHoveredIndex(e.mousePosition);
                    Action selected = (e.button == 0 && index >= 0 && index < items.Count)
                        ? items[index].OnSelected
                        : null;
                    Close();
                    e.Use();
                    view.Repaint();
                    // Invoke after Close so the callback can re-open a menu or rebuild scene state safely.
                    selected?.Invoke();
                    break;
                }

                case EventType.MouseUp:
                case EventType.ScrollWheel:
                    e.Use();
                    break;
            }

            return true;
        }

        private void DrawMenu(Event e)
        {
            int count = items.Count;
            float sectorDegrees = 360f / count;

            Rect discRect = new Rect(center.x - OUTER_RADIUS, center.y - OUTER_RADIUS,
                OUTER_RADIUS * 2f, OUTER_RADIUS * 2f);
            GUI.DrawTexture(discRect, GetDiscTexture());

            int hovered = e.type == EventType.Repaint ? GetHoveredIndex(e.mousePosition) : lastHoveredIndex;
            if (e.type == EventType.Repaint)
                lastHoveredIndex = hovered;

            if (hovered >= 0 && hovered < count)
            {
                Texture wedge = GetWedgeTexture(count);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(hovered * sectorDegrees, center);
                Color previous = GUI.color;
                Color accent = items[hovered].AccentColor;
                accent.a = 0.6f;
                GUI.color = accent;
                GUI.DrawTexture(discRect, wedge);
                GUI.color = previous;
                GUI.matrix = matrix;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = i * sectorDegrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                Vector2 itemCenter = center + direction * MID_RADIUS;
                Item item = items[i];
                bool isHovered = i == hovered;

                Rect iconRect = new Rect(itemCenter.x - ICON_SIZE * 0.5f, itemCenter.y - ICON_SIZE * 0.5f - 7f,
                    ICON_SIZE, ICON_SIZE);

                if (item.Icon != null)
                {
                    Color previous = GUI.color;
                    GUI.color = isHovered ? Color.white : new Color(1f, 1f, 1f, 0.9f);
                    GUI.DrawTexture(iconRect, item.Icon, ScaleMode.ScaleToFit);
                    GUI.color = previous;
                }
                else
                {
                    Color accent = item.AccentColor;
                    accent.a = isHovered ? 1f : 0.85f;
                    Color previous = GUI.color;
                    GUI.color = accent;
                    GUI.DrawTexture(iconRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = previous;
                }

                Rect labelRect = new Rect(itemCenter.x - 44f, iconRect.yMax - 1f, 88f, 16f);
                GUI.Label(labelRect, item.Label, isHovered ? hoverLabelStyle : labelStyle);
            }

            // Center dead-zone marker (cancel).
            float centerSize = INNER_RADIUS * 1.2f;
            Rect centerRect = new Rect(center.x - centerSize * 0.5f, center.y - centerSize * 0.5f,
                centerSize, centerSize);
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(centerRect, GetDiscTexture());
            GUI.color = prevColor;
            GUI.Label(new Rect(center.x - INNER_RADIUS, center.y - 8f, INNER_RADIUS * 2f, 16f), "✕", labelStyle);
        }

        private int GetHoveredIndex(Vector2 mousePosition)
        {
            Vector2 delta = mousePosition - center;
            float distance = delta.magnitude;
            if (distance < INNER_RADIUS || distance > OUTER_RADIUS * 1.6f)
                return -1;

            // Clockwise degrees from north (up), matching the item placement convention.
            float degrees = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;
            if (degrees < 0f)
                degrees += 360f;

            float sectorDegrees = 360f / items.Count;
            return Mathf.RoundToInt(degrees / sectorDegrees) % items.Count;
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                };
                labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }

            if (hoverLabelStyle == null)
            {
                hoverLabelStyle = new GUIStyle(labelStyle)
                {
                    fontStyle = FontStyle.Bold,
                };
                hoverLabelStyle.normal.textColor = Color.white;
            }
        }

        private static Texture2D GetDiscTexture()
        {
            if (discTexture == null)
                discTexture = CreateDiscTexture(TEX_SIZE);
            return discTexture;
        }

        private static Texture2D GetWedgeTexture(int count)
        {
            if (!wedgeTextureByCount.TryGetValue(count, out Texture2D tex) || tex == null)
            {
                tex = CreateWedgeTexture(TEX_SIZE, count);
                wedgeTextureByCount[count] = tex;
            }

            return tex;
        }

        private static Texture2D CreateDiscTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float radius = size * 0.5f;
            const float edge = 0.03f;
            Color fill = new Color(0.13f, 0.13f, 0.16f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - radius) / radius;
                    float dy = (y - radius) / radius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - r) / edge) * 0.92f;
                    pixels[y * size + x] = new Color(fill.r, fill.g, fill.b, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateWedgeTexture(int size, int count)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float radius = size * 0.5f;
            float sectorDegrees = 360f / count;
            float halfDegrees = Mathf.Max(5f, sectorDegrees * 0.5f - 3f); // small gap between sectors
            const float radialEdge = 0.03f;
            const float angularEdge = 3f;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture +Y maps to screen up after GUI.DrawTexture, so "north" = +dy.
                    float dx = (x - radius) / radius;
                    float dy = (y - radius) / radius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float radialAlpha = Mathf.Clamp01((1f - r) / radialEdge) *
                                        Mathf.Clamp01((r - INNER_RATIO) / radialEdge);

                    float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // 0 = north, +clockwise
                    float angularAlpha = Mathf.Clamp01((halfDegrees - Mathf.Abs(angle)) / angularEdge);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, radialAlpha * angularAlpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
#endif
