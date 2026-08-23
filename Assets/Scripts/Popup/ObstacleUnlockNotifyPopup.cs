using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using WaterFlow.Game;
using Sirenix.OdinInspector;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.GameDataManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Popup
{
    public class ObstacleUnlockNotifyPopup : NotifyPopupBase
    {
        private const string NOTIFY_KEY_PREFIX = "ObstacleNotify_";

        private static int pendingFlowCount;

        public static bool IsFlowPending => pendingFlowCount > 0;

        public static void BeginFlow() => pendingFlowCount++;

        public static void EndFlow()
        {
            if (pendingFlowCount > 0) pendingFlowCount--;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFlowState() => pendingFlowCount = 0;

        [BoxGroup("SERVICES")] [SerializeField]
        private Service<DataService> dataService = new();

        [SerializeField] private List<TextMeshProUGUI> textTitles;
        [SerializeField] private TextMeshProUGUI textInfo;
        [SerializeField] private Image mainIcon;

        private List<ObstacleUnlockEntry> pendingEntries;
        private int currentIndex;
        private ObstacleUnlockEntry previewEntry;

        // Prefab-authored icon width, captured before any entry override is applied so
        // switching between entries always restores the default instead of stacking overrides.
        private float defaultIconWidth;
        private bool defaultIconWidthCached;

        // ─── Test/Swap inspector fields (editor only) ────────────────────
        [BoxGroup("TEST/SWAP")] [SerializeField]
        private bool enableTestSwap;

        [BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap))]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private LevelDatabase testLevelDatabase;

        [BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap))]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private ObstacleCategory testCategory = ObstacleCategory.Block;

        [BoxGroup("TEST/SWAP"), ShowIf("@enableTestSwap && testCategory == ObstacleCategory.Block")]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private BlockEffectType testBlockEffectType = BlockEffectType.None;

        [BoxGroup("TEST/SWAP"), ShowIf("@enableTestSwap && testCategory == ObstacleCategory.Gate")]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private GateEffectType testGateEffectType = GateEffectType.None;

        [BoxGroup("TEST/SWAP"), ShowIf("@enableTestSwap && testCategory == ObstacleCategory.InteractableObject")]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private InteractableObjectType testInteractableObjectType = InteractableObjectType.None;

        [BoxGroup("TEST/SWAP"), ShowIf("@enableTestSwap && testCategory == ObstacleCategory.ExtraLayer")]
        [OnValueChanged(nameof(ApplySelectedEffect))]
        [SerializeField] private ExtraLayerType testExtraLayerType = ExtraLayerType.Lift;

        [BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap)), ReadOnly]
        [SerializeField] private int currentTestIndex = -1;

        // Generator has no sub-type — selecting ObstacleCategory.Generator in testCategory is sufficient.

        // ─── Static helpers ───────────────────────────────────────────────

        private ObstacleUnlockEntry CurrentEntry =>
            enableTestSwap && previewEntry != null
                ? previewEntry
                : pendingEntries != null && currentIndex < pendingEntries.Count
                    ? pendingEntries[currentIndex]
                    : null;

        /// <summary>
        /// Show the popup for a list of obstacle unlock entries.
        /// Entries are displayed one by one: closing one reveals the next.
        /// Enqueues via <see cref="NotifyPopupQueue"/> so only one notify popup shows at a time.
        /// </summary>
        public static void Show(List<ObstacleUnlockEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            // Build a deduplication key from all entry effect keys so we don't
            // enqueue the same set twice (e.g. on rapid level transitions).
            var sb = new StringBuilder("Obstacle");
            foreach (var e in entries)
            {
                if (e != null)
                    sb.Append('_').Append(e.GetEffectKey());
            }
            string dedupeKey = sb.ToString();

            UIData uiData = new UIData();
            uiData.Add("obstacleEntries", entries);
            NotifyPopupQueue.Instance.EnqueuePanel<ObstacleUnlockNotifyPopup>(uiData, dedupeKey);
        }

        /// <summary>Show the popup for a single obstacle unlock entry.</summary>
        public static void Show(ObstacleUnlockEntry entry)
        {
            if (entry == null) return;
            Show(new List<ObstacleUnlockEntry> { entry });
        }

        /// <summary>Check if the notification for this entry has already been shown.</summary>
        public static bool HasBeenNotified(ObstacleUnlockEntry entry)
        {
            var ds = Services.DataService;
            return ds != null && ds.GetBool(NOTIFY_KEY_PREFIX + entry.GetEffectKey(), false);
        }

        // ─── Panel lifecycle ──────────────────────────────────────────────

        public override void Open(UIData uiData)
        {
            base.Open(uiData);

            pendingEntries = uiData.TryGet("obstacleEntries", out List<ObstacleUnlockEntry> entries)
                ? entries
                : new List<ObstacleUnlockEntry>();

            currentIndex = 0;
            UpdateData();
            PlayReveal();
        }

        // ─── Click handling ───────────────────────────────────────────────

        /// <summary>
        /// Override: in test-swap mode just swap effects; in normal mode advance entries.
        /// The popup closes only when all entries have been acknowledged.
        /// </summary>
        protected override void HandleClick()
        {
            if (isClosing || isRevealing) return;

            if (enableTestSwap)
            {
                SwapToNextEffect();
                PlayReveal();
                return;
            }

            // Mark current entry as notified
            var entry = CurrentEntry;
            if (entry != null)
                dataService.Instance.SetBool(NOTIFY_KEY_PREFIX + entry.GetEffectKey(), true);

            currentIndex++;

            if (currentIndex < pendingEntries.Count)
            {
                // More entries remain — just refresh the UI, don't close yet.
                UpdateData();
                PlayReveal();
            }
            else
            {
                // All entries shown — close
                RequestClose();
            }
        }

        /// <summary>Not used by HandleClick, but required by the base class contract.</summary>
        protected override UniTask OnConfirmAsync() => UniTask.CompletedTask;

        // ─── UI ───────────────────────────────────────────────────────────

        private void UpdateData()
        {
            var entry = CurrentEntry;
            if (entry == null)
            {
                foreach (var t in textTitles)
                    t.text = string.Empty;
                textInfo.text = string.Empty;
                mainIcon.enabled = false;
                return;
            }

            foreach (var t in textTitles)
                t.text = entry.Title;
            textInfo.text = entry.Description;

            ApplyIconWidth(entry);

            Sprite themedIcon = entry.GetIcon(LevelController.Instance.BlockTheme);
            if (themedIcon)
            {
                mainIcon.sprite = themedIcon;
                mainIcon.enabled = true;
            }
            else
            {
                mainIcon.enabled = false;
            }
        }

        private void ApplyIconWidth(ObstacleUnlockEntry entry)
        {
            if (mainIcon == null) return;

            var rect = mainIcon.rectTransform;
            if (!defaultIconWidthCached)
            {
                defaultIconWidth = rect.sizeDelta.x;
                defaultIconWidthCached = true;
            }

            float width = entry.OverrideIconWidth ? entry.IconWidth : defaultIconWidth;
            Vector2 size = rect.sizeDelta;
            if (Mathf.Approximately(size.x, width)) return;

            size.x = width;
            rect.sizeDelta = size;
        }

        // ─── Test/Swap methods ────────────────────────────────────────────

        [Button("Apply Selected Effect"), BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap))]
        public void ApplySelectedEffect()
        {
            previewEntry = FindEntryBySelectedEffect();
            if (previewEntry != null)
                UpdateSelectorsFromEntry(previewEntry);
            UpdateData();
        }

        [Button("Previous Effect"), BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap))]
        public void SwapToPreviousEffect() => SwapEffect(-1);

        [Button("Next Effect"), BoxGroup("TEST/SWAP"), ShowIf(nameof(enableTestSwap))]
        public void SwapToNextEffect() => SwapEffect(1);

        private void SwapEffect(int direction)
        {
            var available = GetEntriesByCategory();
            if (available.Count == 0)
            {
                currentTestIndex = -1;
                previewEntry = null;
                UpdateData();
                return;
            }

            int current = FindCurrentTestIndex(available);
            if (current < 0) current = 0;
            current = (current + direction + available.Count) % available.Count;
            currentTestIndex = current;
            previewEntry = available[current];
            UpdateSelectorsFromEntry(previewEntry);
            UpdateData();
        }

        private ObstacleUnlockEntry FindEntryBySelectedEffect()
        {
            var available = GetEntriesByCategory();
            if (available.Count == 0)
            {
                currentTestIndex = -1;
                return null;
            }

            for (int i = 0; i < available.Count; i++)
            {
                var entry = available[i];
                if (!entry.MatchesEffect(testCategory, testBlockEffectType, testGateEffectType,
                        testInteractableObjectType, testExtraLayerType)) continue;
                currentTestIndex = i;
                return entry;
            }

            currentTestIndex = 0;
            return available[0];
        }

        private int FindCurrentTestIndex(List<ObstacleUnlockEntry> available)
        {
            if (available == null || available.Count == 0 || previewEntry == null) return -1;
            for (int i = 0; i < available.Count; i++)
            {
                if (ReferenceEquals(available[i], previewEntry)) return i;
            }
            return -1;
        }

        private List<ObstacleUnlockEntry> GetEntriesByCategory()
        {
            var db = ResolveLevelDatabase();
            var result = new List<ObstacleUnlockEntry>();
            if (db == null || db.ObstacleUnlockEntries == null) return result;

            foreach (var entry in db.ObstacleUnlockEntries)
            {
                if (entry == null || entry.Category != testCategory) continue;
                result.Add(entry);
            }
            return result;
        }

        private LevelDatabase ResolveLevelDatabase()
        {
            if (testLevelDatabase != null) return testLevelDatabase;
            if (LevelController.Instance != null) return LevelController.Instance.LevelDatabase;
            return null;
        }

        private void UpdateSelectorsFromEntry(ObstacleUnlockEntry entry)
        {
            if (entry == null) return;
            testCategory = entry.Category;
            if (entry.Category != ObstacleCategory.Generator)
            {
                testBlockEffectType = entry.BlockEffectType;
                testGateEffectType = entry.GateEffectType;
                testInteractableObjectType = entry.InteractableObjectType;
                testExtraLayerType = entry.ExtraLayerType;
            }
        }
    }
}
