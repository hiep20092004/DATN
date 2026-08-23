using System;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.AudioManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.UI;
using HapticType = WaterFlow.Core.HapticType;
using HapticFeedback = WaterFlow.Core.HapticFeedback;

public class UICollectEffectItemMultiple : UICollectEffectItem
{
    [SerializeField] private GameObject coinAnimation;
    private Vector3 tempPos;
    [SerializeField] private float scaleUp = 1.0f;
    [SerializeField] private float scaleDown = 1.0f;
    [SerializeField] private float timeMoveOut = 0.2f;
    [SerializeField] private float timeDelay = 0.3f;
    [SerializeField] private bool random;
    [SerializeField] private float randomAmplitude = 0.1f;
    [SerializeField] private float delayRotate = 0f;

    [SerializeField] private int numCircle = 1;
    [SerializeField] private AnimationCurve rotateCurve;

    [Header("Play On Awake")] [SerializeField]
    private bool playOnAwake = true;

    [SerializeField] private float delayToDestroyEfffect = 0.8f;
    [SerializeField] private AudioId collectSound;

    [Header("Particle")] [SerializeField] private ParticleSystem psDisappear;
    [SerializeField] private ParticleSystem psCoin;
    [SerializeField] private ParticleSystem psLive;
    [SerializeField] private ParticleSystem psButtonCollectEffect;

    [Tooltip("Gọi onFinish (punch UI…) tại tỷ lệ thời gian bay về đích. <1 = sớm hơn OnComplete, khớp cảm giác va chạm trước khi tween kết thúc hẳn.")]
    [SerializeField] [Range(0.5f, 1f)] private float collectNotifyNormalizedTime = 0.92f;
    [SerializeField] [Range(0.5f, 1f)] private float collectNotifyNormalizedTimeButtonCollectEffect = 0.95f;
    

    private bool isCoin = false;
    private bool isLive = false;
    private bool isPlayButtonCollectEffect = false;

    private void ResetState()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        transform.localPosition = Vector3.zero;
    }

    public override void SetData(int index, GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinish)
    {
        ResetState();
        if (uiResourceItem != null && key != GameResource.Coin.ToGameResourceKey())
        {
            uiResourceItem.Icon?.gameObject.SetActive(true);
            isCoin = false;
            if (key == GameResource.UnlimitedLive.ToGameResourceKey()) isLive = true;
            coinAnimation.SetActive(false);
            if (key != GameResource.UnlimitedLive.ToGameResourceKey())
            {
                isPlayButtonCollectEffect = true;
                if (psButtonCollectEffect != null)
                {
                    psButtonCollectEffect.gameObject.SetActive(true);
                }
            }
        } else if (uiResourceItem != null && key == GameResource.Coin.ToGameResourceKey())
        {
            coinAnimation.SetActive(true);
            uiResourceItem.Icon?.gameObject.SetActive(false);
            isCoin = true;
        } 
        
        base.SetData(index, key, quantity, startPos, endPos, onFinish);
    }

    public void SetIntermediaryPos(Vector3 pos)
    {
        tempPos = pos;
    }

    /// <summary>
    /// Tổng thời gian bay: tới điểm trung gian (timeMoveOut) + chờ (timeDelay) + bay về đích (customDuration).
    /// Gọi sau SetIntermediaryPos + SetData để tempPos/targetPosition đã được set.
    /// </summary>
    public float GetTotalFlightDuration()
    {
        float customDuration = speed > 0 ? Vector3.Distance(tempPos, targetPosition) / speed : duration;
        return timeMoveOut + timeDelay + customDuration;
    }

    private void OffVisual()
    {
        if (isCoin && psCoin != null)
        {
            psCoin.Play();
        }
        else if (isLive && psLive != null)
        {
             psLive.Play();
        }
        else {
            HapticFeedback.Play(HapticType.ClickButton);
        }
        if (psDisappear != null)
        {
            psDisappear.Play();
        }
        if (isPlayButtonCollectEffect && psButtonCollectEffect != null)
        {
            psButtonCollectEffect.Play();
        }
        uiResourceItem.Icon?.gameObject.SetActive(false);
        coinAnimation.SetActive(false);
    }

    private void DelayDestroy()
    {
        FrameworkUtils.DelayCall(delayToDestroyEfffect, () => Destroy());
    }

    protected override void DOEffect()
    {
        transform.DOMove(tempPos, timeMoveOut).onComplete += () =>
        {
            float rand = random ? Random.Range(0f, randomAmplitude) : 0;
            if (isPlayButtonCollectEffect) rand = 0;
            DOVirtual.DelayedCall(timeDelay + rand, () =>
            {
                //transform.DOMove(tempPos, timeMoveOut);
                transform.localScale = Vector3.zero;
                transform.DOScale(scaleUp, .025f);

                float customDuration = speed > 0 ? Vector3.Distance(tempPos, targetPosition) / speed : duration;

                float notifyAt = Mathf.Clamp(isPlayButtonCollectEffect ? collectNotifyNormalizedTimeButtonCollectEffect : collectNotifyNormalizedTime, 0.5f, 1f);

                var seq = DOTween.Sequence();
                seq.Join(transform.DOScale(scaleDown, customDuration).SetEase(Ease.InOutSine));
                seq.Join(transform.DOMoveX(targetPosition.x, customDuration).SetEase(moveXCurve));
                seq.Join(transform.DOMoveY(targetPosition.y, customDuration).SetEase(moveYCurve));
                seq.InsertCallback(customDuration * notifyAt, () =>
                {
                    try
                    {
                        onFinish?.Invoke(index);
                        OffVisual();
                        onFinish = null;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                });

                seq.OnComplete(DelayDestroy);

                if (rotate)
                {
                    transform.DORotate(new Vector3(0, 0, numCircle * 360), customDuration, RotateMode.FastBeyond360).SetEase(rotateCurve).SetDelay(delayRotate);
                }

                if (collectSound != AudioId.None)
                    GameSystem.GetService<AudioService>().PlaySound(collectSound);
            });
        };
    }

    private void OffIcon()
    {
        uiResourceItem.Icon?.gameObject.SetActive(false);
        if (psDisappear != null) psDisappear.Play();
        FrameworkUtils.DelayCall(delayToDestroyEfffect, () => Destroy());
    }

    private void Destroy()
    {
        if(!this) return;
        Destroy(gameObject);
    }

    public void PlayEffect()
    {
        DOEffect();
    }
}