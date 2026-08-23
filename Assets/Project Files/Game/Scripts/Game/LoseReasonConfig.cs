using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/LoseReasonConfig", fileName = "LoseReasonConfig")]
    public sealed class LoseReasonConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [HorizontalGroup("Header", Width = 0.45f)]
            [HideLabel]
            public LoseReason reason = LoseReason.None;

            [HorizontalGroup("Header")]
            [ShowInInspector, ReadOnly, HideLabel]
            [GUIColor(nameof(GetCategoryColor))]
            private string reviveType => GetReviveTypeLabel();

            [BoxGroup("Content", ShowLabel = false)]
            [InlineProperty, HideLabel]
            public ReviveReasonConfig config;

            public string EditorLabel => reason == LoseReason.None
                ? "(Unassigned)"
                : $"{reason} ({GetReviveTypeLabel()})";

            private string GetReviveTypeLabel()
            {
                if (reason == LoseReason.None)
                    return "—";

                return reason.IsTimeBasedRevive() ? "Time" : "Obstacle";
            }

            private Color GetCategoryColor()
            {
                if (reason == LoseReason.None)
                    return new Color(0.75f, 0.75f, 0.75f);

                return reason.IsTimeBasedRevive()
                    ? new Color(0.55f, 0.8f, 1f)
                    : new Color(1f, 0.85f, 0.45f);
            }
        }

        [Title("Lose Reason Config")]
        [InfoBox("Maps each LoseReason to revive popup content (icon, title, description, bonus) used by PopupRevive.")]
        [ListDrawerSettings(
            ListElementLabelName = nameof(Entry.EditorLabel),
            ShowFoldout = true,
            DraggableItems = false,
            ShowPaging = true,
            NumberOfItemsPerPage = 10)]
        public List<Entry> entries = new List<Entry>();

    }
}
