using System;
using DG.Tweening;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.TimeManagement;
using TMPro;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.UIElements
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UITimeCounterWarning : MonoBehaviour
    {
        public TMP_Text txtTime;
        public TxtTimeFormat timeFomat;
        public MonoBehaviour go;
        private readonly Service<TimeService> timeService = new();
        private Action callback;
        private Coroutine coroutine;

        public void SetData(long sec, Action callback = null)
        {
            gameObject.SetActive(true);
            StopCount();
            go ??= this;
            this.callback = callback;
            if (go.gameObject.activeInHierarchy)
                coroutine = go.StartCoroutine(timeService.Instance.DoActionRealtime(sec, UpdateTime));
        }

        private void UpdateTime(long sec)
        {
            if (sec <= 0)
            {
                StopCount();
                sec = 0;
                callback?.Invoke();
            }

            txtTime.text = FrameworkUtils.GetTimeByFormat(sec, timeFomat);
        }

        private void StopCount()
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
            GetComponent<CanvasGroup>().DOFade(0, 0.25f).OnComplete(() => gameObject.SetActive(false));
        }
    }
}
