using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class BackgroundTransition : BaseTransition
    {
        [Header("References")]
        [SerializeField] private GameObject transitionPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private List<TransitionTooltipProvider> tooltipProviders;

        // [Header("Loading Text")]
        // [SerializeField] private TMP_Text loadingTxt;
        // [SerializeField] private string loadingTerm;
        
        
        [Header("Timing")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private bool useUnscaledTime = true;

        
        private string baseLoadingTxt;
        private Coroutine _loadingTextCoroutine;

        private void Start()
        {
            if (transitionPanel)
            {
                transitionPanel.SetActive(false);
            }

            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
            }
        }

        // private void OnDestroy()
        // {
        //     StopLoadingText();
        // }
        //
        // private void StartLoadingText()
        // {
        //     if (!loadingTxt)
        //     {
        //         return;
        //     }
        //
        //     StopLoadingText();
        //     _loadingTextCoroutine = StartCoroutine(IELoadingText());
        // }
        //
        // private void StopLoadingText()
        // {
        //     if (_loadingTextCoroutine != null)
        //     {
        //         StopCoroutine(_loadingTextCoroutine);
        //         _loadingTextCoroutine = null;
        //     }
        // }
        //
        // private IEnumerator IELoadingText()
        // {
        //     baseLoadingTxt = LocalizationManager.GetTranslation(loadingTerm);
        //
        //     int index = 0;
        //     while (true)
        //     {
        //         loadingTxt.text = baseLoadingTxt + new string('.', index + 1);
        //         index = (index + 1) % 3;
        //         yield return new WaitForSeconds(0.5f);
        //     }
        // }
        //
        
        public override async UniTask FadeIn(GamePlacement from, GamePlacement to)
        {
            if (transitionPanel)
            {
                transitionPanel.SetActive(true);
            }

            // StartLoadingText();
            ShowTooltip(from, to);

            if (canvasGroup && fadeDuration > 0f)
            {
                _ = canvasGroup.DOFade(1f, fadeDuration).From(0f).SetEase(Ease.Linear);
                await UniTask.WaitForSeconds(fadeDuration, ignoreTimeScale: useUnscaledTime);
            }
        }

        public override async UniTask FadeOut()
        {
            if (canvasGroup && fadeDuration > 0f)
            {
                _ = canvasGroup.DOFade(0f, fadeDuration).From(1f).SetEase(Ease.Linear);
                await UniTask.WaitForSeconds(fadeDuration, ignoreTimeScale: useUnscaledTime);
            }

            if (transitionPanel)
            {
                transitionPanel.SetActive(false);
            }

            // StopLoadingText();
        }
        
        /// <summary>First eligible provider (in list order) shows itself; the rest stay hidden.</summary>
        private void ShowTooltip(GamePlacement from, GamePlacement to)
        {
            bool shown = false;
            foreach (var provider in tooltipProviders)
            {
                if (!provider) continue;

                if (!shown && provider.TryShow(from, to))
                    shown = true;
                else
                    provider.Hide();
            }
        }
    }
}