using HapticType = WaterFlow.Core.HapticType;
using HapticFeedback = WaterFlow.Core.HapticFeedback;
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.AudioManagement;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WaterFlow.Framework.UIModule.UIElements
{
    public class UICurrency : MonoBehaviour
    {
        public GameResource resource;
        public Image icon;
        public Transform scaleObject;
        public TMP_Text txtValue;
        protected int value = -1;
        private Coroutine collectAnim;
        public float counterDuration = 0.5f;
        public float punchDuration = 0.5f;
        /// <summary>Số chu kỳ nhún đầy đủ (1→scaleMax→1) mỗi giây trong lúc đếm.</summary>
        [FormerlySerializedAs("scaleSpeed")]
        public float punchCyclesPerSecond = 3f;
        public float scaleMax = 1.15f;
        [Tooltip("When true, skip restarting punch scale if animation is already running; blast still spawns each time.")]
        [SerializeField] private bool skipCollectEffectWhileRunning = true;
        [SerializeField] protected bool blockClick;
        public GameObject plusObj;
        protected readonly Service<InventoryService> inventoryService = new();
        [SerializeField] protected ParticleSystem blastEffect;

        protected EventBinding<AddResourceVisualEvent> addCurrencyEvent;
        [SerializeField] protected string openPanelName = "ShopPanel";
        [SerializeField] protected string receiveSound;

        [Header("Haptic")]
        [SerializeField] protected bool playCoinHaptic = false;

        protected virtual void Start()
        {
            if (plusObj)
                plusObj.SetActive(!blockClick);
            // if (blastEffect)
            //     blastEffect.gameObject.SetActive(false);
        }

        public virtual void OnEnable()
        {
            UpdateValueView(false);
            addCurrencyEvent = new EventBinding<AddResourceVisualEvent>(OnAddCurrency);
            inventoryService.Instance.OnResourceUpdate += OnResourceUpdate;
        }

        protected virtual void OnDisable()
        {
            EventBus<AddResourceVisualEvent>.Deregister(addCurrencyEvent);
            inventoryService.Instance.OnResourceUpdate -= OnResourceUpdate;
            addCurrencyEvent = null;
            if (txtValue != null)
                txtValue.DOKill();
        }


        protected virtual void OnAddCurrency(AddResourceVisualEvent eventData)
        {
            if (eventData.key.gameResource != this.resource && eventData.key.gameResource != GameResource.MAX) return;
            string source = eventData.source;

            if (eventData.collectEffect != null && icon != null)
            {
                eventData.collectEffect.Collect(eventData.key, eventData.visualQuantity, eventData.position, icon.transform.position,
                    (index) => OnCollectEffect(index, source));
            }
            else
            {
                inventoryService.Instance.ClaimPendingResource(source, resource.ToGameResourceKey());
            }
        }

        public virtual bool CanReceiveVisualResource(GameResource targetResource)
        {
            return resource == GameResource.MAX || targetResource == GameResource.MAX || resource == targetResource;
        }

        protected virtual void OnCollectEffect(int index, string source)
        {
            if (index != 0) return;
            try
            {

                inventoryService.Instance.ClaimPendingResource(source, resource.ToGameResourceKey());
                PlayCollectEffect();
                // if (blastEffect)
                // {
                //     blastEffect.gameObject.SetActive(true);
                //     blastEffect.Play();
                // }
                if (resource == GameResource.Coin && playCoinHaptic)
                {
                    PlayReceiveCoinHaptic();
                }

                if (!string.IsNullOrEmpty(receiveSound))
                {
                    GameSystem.GetService<AudioService>().PlaySound(receiveSound);
                }
            }
            catch (Exception)
            {
            }
        }

        bool isPlayingHaptic = false;
        protected virtual void PlayReceiveCoinHaptic()
        {
            if (isPlayingHaptic) return;
            isPlayingHaptic = true;
            HapticFeedback.Play(HapticType.CoinAtHome);

            //set isPlayingHaptic to false after 5 seconds for recovery
            FrameworkUtils.DelayCall(5f, () => isPlayingHaptic = false);
        }

        protected virtual void OnResourceUpdate(GameResourceKey resourceKey)
        {
            if (resourceKey.gameResource != this.resource && resourceKey.gameResource != GameResource.MAX) return;
            UpdateValueView(true);
        }

        public virtual void UpdateValueView(bool doCounter = true)
        {
            if (this == null || txtValue == null) return;

            int oldvalue = this.value;
            value = inventoryService.Instance.GetResource(resource.ToGameResourceKey()).quantity;

            if (value == oldvalue) return;
            if (gameObject.activeInHierarchy && doCounter)
                txtValue.DOCounter(oldvalue, value, counterDuration, addThousandsSeparator: false);
            else
                txtValue.text = value.ToString();
        }


        public void SetBlockClick(bool block)
        {
            this.blockClick = block;
            if (plusObj)
                plusObj.SetActive(!block);
        }

        public virtual void OnClickCurrency()
        {
            if (blockClick) return;
            var uidata = new UIData();
            uidata.Add("OpenBy", "UICurrency");
            PanelManager.Instance.OpenPanelByNameAsync<Panel>(openPanelName).Forget();
        }

        public void PlayCollectEffect()
        {
            if (!gameObject.activeInHierarchy) return;

            if (blastEffect)
            {
                blastEffect.Play();
            }

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
                scaleObject.transform.localScale = Vector3.one * s;
                yield return null;
            }

            scaleObject.transform.localScale = Vector3.one;
            collectAnim = null;
        }
    }
}