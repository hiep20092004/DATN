using System;

using System.Collections;

using System.Collections.Generic;

using WaterFlow.Framework.Base.Singleton;

using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;



public class UICollectBoostersPoint : MonoBehaviour

{

    [SerializeField] private List<GameResource> resourceTypes;

    [SerializeField] private Transform scaleObject;

    [SerializeField] private float punchDuration = 0.5f;

    [SerializeField] private float punchCyclesPerSecond = 3f;

    [SerializeField] private float scaleMax = 1.15f;

    [SerializeField] private bool skipCollectEffectWhileRunning = true;
    [SerializeField] private ParticleSystem collectParticle;

    [Header("Collect Target")]
    [Tooltip("Offset local X/Y so flying items land on the correct spot (UI units on RectTransform).")]
    [SerializeField] private Vector2 collectEndOffset;

    private EventBinding<AddResourceVisualEvent> collectItemEvent;

    private Coroutine collectAnim;



    private void Awake()

    {

        if (scaleObject == null)

            scaleObject = transform;

    }



    private void OnEnable()

    {

        collectItemEvent = new EventBinding<AddResourceVisualEvent>(OnCollectResource);

    }



    private void OnDisable()

    {

        EventBus<AddResourceVisualEvent>.Deregister(collectItemEvent);

    }



    public bool CanReceiveVisualResource(GameResource resource)

    {

        return resourceTypes != null && resourceTypes.Contains(resource);

    }

    public Vector3 GetCollectEndPosition()
    {
        return transform.TransformPoint(new Vector3(collectEndOffset.x, collectEndOffset.y, 0f));
    }

    protected virtual void OnCollectResource(AddResourceVisualEvent eventData)

    {

        if (!CanReceiveVisualResource(eventData.key.gameResource)) return;



        if (eventData.collectEffect != null)

        {

            eventData.collectEffect.Collect(eventData.key, eventData.visualQuantity, eventData.position, GetCollectEndPosition(), (index) =>

            {

                if (index != 0) return;
                PlayCollectEffect();

                ClaimPendingResource(eventData);

            });

        }

        else

        {
            ClaimPendingResource(eventData);
        }
    }
    private void PlayCollectEffect()
    {
        if (!gameObject.activeInHierarchy) return;
        if (skipCollectEffectWhileRunning && collectAnim != null)
            return;
        if (collectAnim != null)
            StopCoroutine(collectAnim);
        collectAnim = StartCoroutine(CollectEffect());
    }

    private IEnumerator CollectEffect()
    {
        float elapsed = 0f;
        if (scaleObject == null) yield break;
        
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float wave = Mathf.PingPong(elapsed * punchCyclesPerSecond * 2f, 1f);
            float s = Mathf.Lerp(1f, scaleMax, wave);
            scaleObject.localScale = Vector3.one * s;
            yield return null;
        }
        scaleObject.localScale = Vector3.one;
        collectAnim = null;
        
    }

    private void ClaimPendingResource(AddResourceVisualEvent eventData)
    {
        if (Services.InventoryService == null)
        {
            return;
        }

        Services.InventoryService.ClaimPendingResource(eventData.source, eventData.key);
    }

}


