using System.Collections.Generic;
using DG.Tweening;
using Popup;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using UnityEngine;
using Ease = DG.Tweening.Ease;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    /// <summary>
    /// Runtime-only manager for the coins that live inside Gold-colored blocks during a Gold Mode
    /// special level. Created when a Gold Mode level loads. For every Gold block it spawns one coin
    /// prefab on each filled cell of the block's figure — but only once that block's own spawn
    /// (scale-in) animation has finished. When a Gold block becomes fully filled (after its fill
    /// animation completes) the coins it holds are claimed and added to <see cref="LevelRuntimeData"/>
    /// as earned gold.
    /// </summary>
    public class GoldBlockCoinManager
    {
        private const float CoinSpawnTweenDuration = 0.3f;
        private const string GoldModeIntroTerm =
            "This is a bonus level! Remove as many GOLDEN BLOCKS as possible in given time!";

        private readonly GameObject coinPrefab;
        private readonly LevelRepresentation representation;

        private readonly HashSet<LevelBlockBehavior> watchedBlocks = new();
        private readonly Dictionary<LevelBlockBehavior, List<GameObject>> blockCoins = new();
        private bool disposed;

        /// <summary>
        /// Total gold collectible in this level (every filled gold-block cell × <see cref="SpecialLevelService.GoldBlockRewardMultiplier"/>).
        /// Counted up front in <see cref="SpawnCoinsForGoldBlocks"/> so completion tracking has a stable denominator
        /// even though the coin GameObjects themselves spawn lazily as each block finishes its spawn animation.
        /// </summary>
        public int MaxPossibleGold { get; private set; }

        public GoldBlockCoinManager(GameObject coinPrefab, LevelRepresentation representation)
        {
            this.coinPrefab = coinPrefab;
            this.representation = representation;
        }

        public void SpawnCoinsForGoldBlocks()
        {
            if (disposed || coinPrefab == null || representation?.ActiveBlocks == null)
                return;

            foreach (LevelBlockBehavior block in representation.ActiveBlocks)
            {
                if (!block || !IsGoldBlock(block) || blockCoins.ContainsKey(block) || watchedBlocks.Contains(block))
                    continue;

                MaxPossibleGold += CountFilledCells(block) * SpecialLevelService.GoldBlockRewardMultiplier;

                if (block.IsSpawnAnimationCompleted)
                    SpawnCoins(block);
                else
                {
                    watchedBlocks.Add(block);
                    block.SpawnAnimationCompleted += OnBlockSpawnAnimationCompleted;
                }
            }
        }

        /// <summary>
        /// Shows the one-time Gold Mode intro bubble the very first time the player enters any
        /// Gold Mode level. The bubble is anchored to the block sitting closest to the level's
        /// center (see the design reference). Persisted via <see cref="SpecialLevelService"/> so
        /// it never appears again.
        /// </summary>
        public void TryShowIntroMessage()
        {
            if (disposed)
                return;

            SpecialLevelService special = Services.SpecialLevelService;
            if (special == null || !special.ShouldShowGoldModeIntro())
                return;

            if (!TryGetCenterBlockWorldPosition(out Vector3 worldPosition))
                return;

            RectTransform anchor = CreateWorldSpaceAnchor(worldPosition);
            if (anchor == null)
                return;

            special.MarkGoldModeIntroSeen();

            var param = new PopupMessageInfoParam
            {
                Anchor = anchor,
                PlainMessage = GoldModeIntroTerm,
                DestroyAnchorOnClose = true
            };
            PanelManager.Instance.OpenForget<PopupMessageInfo>(new UIData().Add(UIDataKey.Content, param));
        }

        private bool TryGetCenterBlockWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            List<LevelBlockBehavior> blocks = representation?.ActiveBlocks;
            if (blocks == null || blocks.Count == 0)
                return false;

            Vector3 centroid = Vector3.zero;
            int count = 0;
            foreach (LevelBlockBehavior block in blocks)
            {
                if (!block)
                    continue;
                centroid += block.transform.position;
                count++;
            }

            if (count == 0)
                return false;

            centroid /= count;

            LevelBlockBehavior nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (LevelBlockBehavior block in blocks)
            {
                if (!block)
                    continue;
                float sqr = (block.transform.position - centroid).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = block;
                }
            }

            if (!nearest)
                return false;

            worldPosition = nearest.transform.position;
            return true;
        }

        private static RectTransform CreateWorldSpaceAnchor(Vector3 worldPosition)
        {
            Canvas canvas = UIController.MainCanvas;
            Camera worldCamera = Camera.main;
            if (canvas == null || worldCamera == null)
                return null;

            var canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
                return null;

            var anchorObject = new GameObject("[GOLD_MODE_INTRO_ANCHOR]", typeof(RectTransform));
            var anchorRect = (RectTransform)anchorObject.transform;
            anchorRect.SetParent(canvasRect, false);
            anchorRect.sizeDelta = Vector2.zero;
            anchorRect.anchorMin = anchorRect.anchorMax = anchorRect.pivot = new Vector2(0.5f, 0.5f);
            anchorRect.anchoredPosition = localPoint;
            return anchorRect;
        }

        private void OnBlockSpawnAnimationCompleted(LevelBlockBehavior block)
        {
            if (block)
                block.SpawnAnimationCompleted -= OnBlockSpawnAnimationCompleted;
            watchedBlocks.Remove(block);

            if (disposed || !block)
                return;

            SpawnCoins(block);
        }

        private void SpawnCoins(LevelBlockBehavior block)
        {
            if (blockCoins.ContainsKey(block))
                return;

            List<GameObject> coins = SpawnCoinsForBlock(block);
            if (coins.Count == 0)
                return;

            blockCoins.Add(block, coins);
            block.FullFilledAfterAnimation += OnBlockFullFilled;
        }

        private List<GameObject> SpawnCoinsForBlock(LevelBlockBehavior block)
        {
            var coins = new List<GameObject>();
            Transform modelParent = block.ModelParentTransform;

            foreach (Vector2Int cell in EnumerateFilledCells(block.Figure))
            {
                Vector3 cellWorld = block.transform.TransformPoint(new Vector3(cell.x, 0f, cell.y));
                GameObject coin = Object.Instantiate(coinPrefab, modelParent);
                coin.transform.position = cellWorld;
                coin.transform.localRotation = Quaternion.Inverse(modelParent.localRotation);
                PlayCoinSpawnTween(coin.transform);
                coins.Add(coin);
            }

            return coins;
        }

        private static int CountFilledCells(LevelBlockBehavior block)
        {
            int filled = 0;
            foreach (Vector2Int _ in EnumerateFilledCells(block.Figure))
                filled++;
            return filled;
        }

        /// <summary>
        /// Single source of truth for walking a block figure's filled cells, yielding each cell's
        /// grid coordinate. Shared by coin spawning (needs the position) and gold counting (needs the count)
        /// so the figure-layout/bounds convention lives in exactly one place.
        /// </summary>
        private static IEnumerable<Vector2Int> EnumerateFilledCells(LevelFigure figure)
        {
            PointData[] points = figure?.Points;
            if (points == null)
                yield break;

            int width = figure.Size.x;
            int cellCount = Mathf.Min(points.Length, width * figure.Size.y);
            for (int i = 0; i < cellCount; i++)
            {
                if (points[i].IsFilled)
                    yield return new Vector2Int(i % width, i / width);
            }
        }

        private static void PlayCoinSpawnTween(Transform coinTransform)
        {
            Vector3 targetScale = coinTransform.localScale;
            coinTransform.localScale = Vector3.zero;
            coinTransform.DOScale(targetScale, CoinSpawnTweenDuration).SetEase(Ease.OutBack);
        }

        private void OnBlockFullFilled(LevelBlockBehavior block)
        {
            if (disposed || !block)
                return;

            block.FullFilledAfterAnimation -= OnBlockFullFilled;

            if (!blockCoins.Remove(block, out List<GameObject> coins))
                return;

            int goldReward = coins.Count * SpecialLevelService.GoldBlockRewardMultiplier;
            LevelRuntimeData.Current.AddGold(goldReward);
            DestroyCoins(coins);

            if (UIController.GetPage<UIGame>()?.LevelPanel is GoldModeLevelPanel goldPanel)
                goldPanel.AnimateReward(LevelRuntimeData.Current.GoldEarned, goldReward);
        }

        private static void DestroyCoins(List<GameObject> coins)
        {
            foreach (GameObject coin in coins)
            {
                if (coin)
                    Object.Destroy(coin);
            }
        }

        private static bool IsGoldBlock(LevelBlockBehavior block)
            => block.GetActiveBlockColor() == BlockColor.Gold ||
               block.GetSecondaryBlockColor() == BlockColor.Gold;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            foreach (LevelBlockBehavior block in watchedBlocks)
            {
                if (block)
                    block.SpawnAnimationCompleted -= OnBlockSpawnAnimationCompleted;
            }
            watchedBlocks.Clear();

            foreach (KeyValuePair<LevelBlockBehavior, List<GameObject>> entry in blockCoins)
            {
                if (entry.Key)
                    entry.Key.FullFilledAfterAnimation -= OnBlockFullFilled;

                DestroyCoins(entry.Value);
            }

            blockCoins.Clear();
        }
    }
}
