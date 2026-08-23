using System.Threading;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Game
{
    public static class GameWinFlowService
    {
        /// <summary>
        /// Pending inventory bucket for level-win rewards (claimed on Home with collect UI).
        /// </summary>
        public const string WinCoinPendingSource = "win_level_coin";

        public static ResourceData GetWinReward()
        {
            return GetWinReward(LevelController.Instance.LevelRepresentation.LevelData);
        }

        public static ResourceData GetWinReward(LevelData levelData)
        {
            LevelType levelType = levelData.Type;
            GameplayConfigService gameplayConfig = Services.GameplayConfig;
            return gameplayConfig.GetWinReward(levelType);
        }

        public static void GrantWinReward()
        {
            GrantWinReward(LevelController.Instance.LevelRepresentation.LevelData);
        }

        public static void GrantWinReward(LevelData levelData)
        {
            ResourceData winReward = GetWinReward(levelData);
            GrantWinCoinPendingReward(winReward, "win_level", "win_level");
        }

        private static void GrantWinCoinPendingReward(ResourceData reward, string spendType, string spendId)
        {
            if (reward == null || reward.quantity <= 0)
                return;

            EarnResourceLogData log = new EarnResourceLogData(spendType, spendId);
            if (reward.gameResource != GameResource.Coin)
            {
                Services.InventoryService.AddResource(reward, log);
                return;
            }

            var rewardData = new RewardData();
            rewardData.AddReward(reward.Clone());
            Services.InventoryService.AddPendingReward(WinCoinPendingSource, rewardData, log);
        }

        /// <summary>
        /// Home tab: show coin collect for pending win coin (same event path as PopupReward), wait until claimed, then continue async flow.
        /// </summary>
        public static async UniTask TryDeliverPendingWinCoinOnHomeAsync(MonoBehaviour host, Transform effectStart,
            CancellationToken cancellationToken)
        {
            if (host == null)
            {
                return;
            }

            InventoryService inventory = Services.InventoryService;
            string source = WinCoinPendingSource;
            GameResourceKey coinKey = GameResource.Coin.ToGameResourceKey();

            ResourceData pending = inventory.GetPendingResource(source, coinKey);
            if (pending == null || pending.quantity <= 0)
            {
                return;
            }

            Vector3 startPos = effectStart != null ? effectStart.position : host.transform.position;

            if (!HasActiveCoinCurrencyReceiver())
            {
                inventory.ClaimPendingResource(source, coinKey);
                return;
            }

            string pooledObjectName = ResolveWinCoinPooledEffectName();
            bool usePooledCollect = !string.IsNullOrEmpty(pooledObjectName);
            int visualQuantity = Mathf.Max(1, pending.quantity);

            EventBus<AddResourceVisualEvent>.Raise(new AddResourceVisualEvent()
            {
                source = source,
                key = coinKey,
                visualQuantity = visualQuantity,
                position = startPos,
                collectEffect = usePooledCollect
                    ? new CollectEffectMultiple()
                    {
                        collectEffectName = pooledObjectName,
                        radius = visualQuantity > 10 ? 0.75f : 0.2f,
                    }
                    : null
            });
            Services.AudioService.PlaySound(AudioId.Coin_Received);

            const float timeoutSeconds = 6f;
            float elapsed = 0f;
            while (elapsed < timeoutSeconds && inventory.GetPendingResource(source, coinKey) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.deltaTime;
            }

            if (inventory.GetPendingResource(source, coinKey) != null)
            {
                inventory.ClaimPendingResource(source, coinKey);
            }
        }

        private static bool HasActiveCoinCurrencyReceiver()
        {
            UICurrency[] currencyViews =
                Object.FindObjectsByType<UICurrency>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (UICurrency currencyView in currencyViews)
            {
                if (currencyView == null || !currencyView.isActiveAndEnabled)
                {
                    continue;
                }

                if (currencyView.CanReceiveVisualResource(GameResource.Coin))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveWinCoinPooledEffectName()
        {
            const string preferredName = "UIPopupCollectItemMultiple";
            if (Resources.Load<GameObject>($"PoolingObjects/{preferredName}") != null)
            {
                return preferredName;
            }

            const string fallbackName = "UICollectEffectItemMultiple";
            return Resources.Load<GameObject>($"PoolingObjects/{fallbackName}") != null ? fallbackName : null;
        }

        public static void SaveAndSwitchScene(GamePlacement gamePlacement)
        {
            global::WaterFlow.Core.SaveController.Save(true);
            GameController.Instance.Unload(false, () =>
            {
                Services.TransitionService.SwitchScene(gamePlacement);
            });
        }
    }
}
