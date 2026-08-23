using System;
using Sirenix.OdinInspector;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.TimeManagement;
using TMPro;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.UIElements
{
    public class UITimeCounter : MonoBehaviour
    {
        public TMP_Text txtTime;
        public TxtTimeFormat timeFomat;
        public TxtTimeFormat timeFomatFixed = TxtTimeFormat.OnlyOneUnit;
        public MonoBehaviour go;

        [Space]
        [Header("Warning")]
        [SerializeField] private bool showWarning = false;
        [SerializeField, ShowIf("showWarning")] private GameObject normalObject;
        [SerializeField, ShowIf("showWarning")] private UITimeCounterWarning warning;
        [SerializeField, ShowIf("showWarning")] private int warningTime = 10;

        private readonly Service<TimeService> timeService = new();
        private Action callback;
        private Coroutine coroutine;
        private bool isWarning = false;

        public void SetData(long sec, Action callback = null)
        {
            if (showWarning)
            {
                isWarning = false;
                normalObject.SetActive(true);
                warning.gameObject.SetActive(false);
            }

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
                txtTime.text = "Finished";
                return;
            }
            if (showWarning)
            {
                if (sec <= warningTime && !isWarning)
                {
                    isWarning = true;
                    FrameworkUtils.DelayCall(0.5f, () =>
                    {
                        normalObject.SetActive(false);
                    });
                    warning.SetData(sec);
                }
            }

            txtTime.text = FrameworkUtils.GetTimeByFormat(sec, timeFomat);
        }

        public void SetFixedData(long sec)
        {
            gameObject.SetActive(true);
            txtTime.text = FrameworkUtils.GetTimeByFormat(sec, timeFomatFixed);
        }

        private void StopCount()
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }
}