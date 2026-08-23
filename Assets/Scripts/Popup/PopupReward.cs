using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Helper;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupReward : Panel
{
    public const string REWARD_KEY = "Reward";
    public const string SOURCE_KEY = "Source";
    public const string ON_CLAIM_COMPLETE_KEY = "OnClaimComplete";
    public const string TITLE_KEY = "Title";
    public const string TRACKING_NAME_KEY = "TrackingName";
    public Button Claim;
    public TMP_Text Title;

    public UIItem prefabReward;

    public Transform parentReward;

    private RewardData rewardData;

    public Transform coinFinish;

    [SerializeField] private float initDelayBetweenItems = 0.15f;

    [SerializeField] private float claimDelayBetweenItems = 0.1f;
    [SerializeField] private float smallQuantityRadius = 0.2f;
    [SerializeField] private float largeQuantityRadius = 0.75f;

    [SerializeField] private float singleRewardScale = 1.4f;
    [SerializeField] private float doubleRewardScale = 1.15f;

    [Tooltip("Delay (s) sau khi chuyển về Home, chờ tab slide ổn định trước khi bắn item collect.")]
    [SerializeField] private float switchHomeSettleDelay = 0.35f;

    private Action onClaimComplete;
    private string claimSource;
    private string defaultTrackingName;
    private bool isClaiming;
    private bool hasInvokedClaimCallback;
    private readonly List<UIItem> rewardItems = new();

    public static void Show(UIData data)
    {
        PanelManager.Instance.OpenForget<PopupReward>(data);
    }

    public override void OnSetup()
    {
        base.OnSetup();
        defaultTrackingName = trackingName;
        Claim.onClick.AddListener(OnClaimClick);
    }

    private async UniTask InitReward()
    {
        Claim.interactable = false;
        //_ = Claim.transform.DOScale(Vector3.zero, 0);
        ClearReward();
        await UniTask.WaitForSeconds(0.1f);

        // Ít item thì phóng to để lấp khoảng trống của vùng reward.
        float itemScale = GetRewardItemScale(rewardData.resourceUnits.Count);

        foreach (var resource in rewardData.resourceUnits)
        {
            UIItem itemUI = Instantiate(prefabReward, parentReward);
            _ = itemUI.transform.DOScale(Vector3.one * itemScale, 0.3f).SetEase(Ease.OutBack).From(Vector3.zero);
            Services.AudioService.PlaySound(AudioId.Booster_Unlock);
            WaterFlow.Core.HapticFeedback.Play(WaterFlow.Core.HapticType.ItemAppear);
            itemUI.gameObject.SetActive(true);
            itemUI.SetData(resource);
            rewardItems.Add(itemUI);
            await UniTask.WaitForSeconds(initDelayBetweenItems);
        }

        await UniTask.WaitForSeconds(0.1f);
        //await Claim.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).From(Vector3.zero);
        
        Claim.interactable = true;
    }

    private float GetRewardItemScale(int rewardCount)
    {
        return rewardCount switch
        {
            1 => singleRewardScale,
            2 => doubleRewardScale,
            _ => 1f
        };
    }

    private void ClearReward()
    {
        rewardItems.Clear();
        foreach (Transform child in parentReward)
        {
            Destroy(child.gameObject);
        }
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        isClaiming = false;
        hasInvokedClaimCallback = false;
        claimSource = null;

        if (uiData.TryGet(REWARD_KEY, out RewardData reward))
        {
            rewardData = reward;
        }
        if (uiData.TryGet(SOURCE_KEY, out string source))
        {
            claimSource = source;
        }
        if (uiData.TryGet(ON_CLAIM_COMPLETE_KEY, out Action action))
        {
            onClaimComplete = action;
        }
        //Add title if uiData has title key
        if (uiData.TryGet(TITLE_KEY, out string title))
        {
            Title.SetLocalize(title);
        }

        // Panel instance is reused in the stack — fall back to the prefab's
        // trackingName when the caller doesn't pass an override.
        trackingName = uiData.TryGet(TRACKING_NAME_KEY, out string customTrackingName) && !string.IsNullOrEmpty(customTrackingName)
            ? customTrackingName
            : defaultTrackingName;

        InitReward().Forget();
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
    }

    public override void Close()
    {
        base.Close();

        Action onComplete = null;
        uiData?.TryGet("onClose", out onComplete);
        onComplete?.Invoke();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
        Claim.onClick.RemoveListener(OnClaimClick);

        if (hasInvokedClaimCallback)
        {
            return;
        }

        hasInvokedClaimCallback = true;
        onClaimComplete?.Invoke();
    }

    private void OnClaimClick()
    {
        if (isClaiming)
        {
            return;
        }

        isClaiming = true;
        Claim.interactable = false;
        ClaimFlow().Forget();
    }

    private async UniTaskVoid ClaimFlow()
    {
        await TrySwitchToHomeForCollectAsync();

        if (rewardData?.resourceUnits != null && rewardData.resourceUnits.Count > 0)
        {
            bool hasCoin = rewardData.resourceUnits.Any(resource => resource.gameResource == GameResource.Coin);
            if (hasCoin)
            {
                Services.AudioService.PlaySound(AudioId.Coin_Received);
            }
            ReceiveItems().Forget();
            return;
        }

        Close();
    }

    // Tab Shop không có UICurrency/UICollectBoostersPoint nhận thưởng nên item bay không có đích.
    // Chuyển về Home trước (giống UIJourney.ClaimReward) để animation UICollectEffectItemMultiple
    // đáp vào coin counter của Home. Các popup mở sẵn trên Home không bị ảnh hưởng.
    private async UniTask TrySwitchToHomeForCollectAsync()
    {
        HomeManager homeManager = HomeManager.Instance;
        if (homeManager == null || homeManager.CurrentNavigationType != NavigationType.Shop)
        {
            return;
        }

        await homeManager.SwitchTab(NavigationType.Home);

        if (switchHomeSettleDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(switchHomeSettleDelay),
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    private async UniTaskVoid ReceiveItems()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        string pooledObjectName = ResolvePooledObjectName();
        bool canUsePooledCollect = !string.IsNullOrEmpty(pooledObjectName);

        for (int i = 0; i < rewardData.resourceUnits.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            ResourceData resource = rewardData.resourceUnits[i];
            if (resource == null)
            {
                continue;
            }

            Vector3 startPos = GetRewardItemPosition(i);
            int visualQuantity = GetVisualQuantity(resource);
            if (!HasActiveCurrencyReceiver(resource.gameResource))
            {
                ClaimPendingFallback(resource.Key);
                await UniTask.Delay((int)(claimDelayBetweenItems * 1000), cancellationToken: ct);
                continue;
            }

            EventBus<AddResourceVisualEvent>.Raise(new AddResourceVisualEvent()
            {
                source = claimSource,
                key = resource.Key,
                visualQuantity = visualQuantity,
                position = startPos,
                collectEffect = canUsePooledCollect
                    ? new CollectEffectMultiple()
                    {
                        collectEffectName = pooledObjectName,
                        radius = visualQuantity > 10 ? largeQuantityRadius : smallQuantityRadius,
                    }
                    : null
            });

            await UniTask.Delay((int)(claimDelayBetweenItems * 1000), cancellationToken: ct);
        }

        if (!ct.IsCancellationRequested)
        {
            Close();
        }
    }

    private Vector3 GetRewardItemPosition(int index)
    {
        if (index >= 0 && index < rewardItems.Count && rewardItems[index] != null)
        {
            return rewardItems[index].transform.position;
        }

        return transform.position;
    }

    private static int GetVisualQuantity(ResourceData resource)
    {
        if (resource.gameResource == GameResource.Live)
        {
            return 1;
        }

        return Mathf.Max(1, resource.quantity);
    }

    private static bool HasActiveCurrencyReceiver(GameResource gameResource)
    {
        if (gameResource == GameResource.Coin || gameResource == GameResource.UnlimitedLive)
        {
            UICurrency[] currencyViews = FindObjectsByType<UICurrency>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (UICurrency currencyView in currencyViews)
            {
                if (currencyView == null || !currencyView.isActiveAndEnabled)
                {
                    continue;
                }

                if (currencyView.CanReceiveVisualResource(gameResource))
                {
                    return true;
                }
            }
        }
        if (gameResource == GameResource.Hammer || gameResource == GameResource.Expand || gameResource == GameResource.Freeze || gameResource == GameResource.WaterGun
             || gameResource == GameResource.PreClock || gameResource == GameResource.PreWand)
        {

            UICollectBoostersPoint[] boosterPoints = FindObjectsByType<UICollectBoostersPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (UICollectBoostersPoint boosterPoint in boosterPoints)
            {
                if (boosterPoint == null || !boosterPoint.isActiveAndEnabled)
                {
                    continue;
                }

                if (boosterPoint.CanReceiveVisualResource(gameResource))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ClaimPendingFallback(GameResourceKey key)
    {
        if (string.IsNullOrEmpty(claimSource))
        {
            return;
        }

        Services.InventoryService.ClaimPendingResource(claimSource, key);
    }

    private static string ResolvePooledObjectName()
    {
        const string preferredName = "UIPopupCollectItemMultiple";
        if (Resources.Load<GameObject>($"PoolingObjects/{preferredName}") != null)
        {
            return preferredName;
        }

        const string fallbackName = "UICollectEffectItemMultiple";
        return Resources.Load<GameObject>($"PoolingObjects/{fallbackName}") != null ? fallbackName : null;
    }
}
