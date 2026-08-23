using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;
using UnityEngine.UI;

public class RewardToUI : MonoBehaviour
{
    public Image MainIcon;

    [Header("Animation")]
    public float duration = 0.5f;
    public float startDuration = 0.2f;
    public float deplayStart = 0f;
    public float delayEachItem = 0.1f;
    public float delayOnMove = 0.2f;
    public AnimationCurve MoveXCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve MoveYCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float randomAmplitude = 0f;
    public Vector2 randomMove = new Vector2(0.1f, 0.1f);
    public float delayDestroy = 0.1f;
    public AudioId collectSound = AudioId.None;
    private Action onFinish;
    private Vector3 startPos;
    private Vector3 endPos;
    private bool playOnAwake = true;
    private int index;
    private GameObject cachedGameObject;
    public void SetData(int index, GameResourceKey key, Vector3 startPos, Vector3 endPos, bool playOnAwake = true, Action onFinish = null)
    {
        this.startPos = startPos;
        this.endPos = endPos;
        this.onFinish = onFinish;
        this.index = index;
        if (MainIcon != null)
        {
            MainIcon.sprite = Services.GameResourceService?.GetSprite(key.gameResource);
        }

        transform.position = startPos;
        this.playOnAwake = playOnAwake;
    }

    private void Awake()
    {
        cachedGameObject = gameObject;
    }

    private void OnEnable()
    {
        if (playOnAwake)
        {
            PlayEffect().Forget();
        }
    }

    public async UniTask PlayEffect()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        await UniTask.WaitForSeconds(deplayStart, cancellationToken: ct);
        var randomMoveX = UnityEngine.Random.Range(-randomMove.x, randomMove.x);
        var randomMoveY = UnityEngine.Random.Range(-randomMove.y, randomMove.y);
        await transform.DOMove(new Vector3(startPos.x + randomMoveX, startPos.y + randomMoveY, startPos.z), startDuration).SetEase(Ease.OutQuad);
        await UniTask.WaitForSeconds(delayEachItem * index, cancellationToken: ct);
        await UniTask.WaitForSeconds(delayOnMove, cancellationToken: ct);
        _ = transform.DOMoveX(endPos.x, duration).SetEase(MoveXCurve);
        await transform.DOMoveY(endPos.y, duration).SetEase(MoveYCurve);
        onFinish?.Invoke();
        SelfDestroy().Forget();
    }

    private async UniTask SelfDestroy()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        await UniTask.WaitForSeconds(delayDestroy, cancellationToken: ct);
        if (cachedGameObject != null)
        {
            Destroy(cachedGameObject);
        }
    }
}
