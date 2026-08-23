using System;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.TimeManagement;
using TMPro;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.UIElements
{
    public class UITimer : MonoBehaviour
    {
        public TMP_Text txtTime;

        [Tooltip("Each option in the dropdown shows a sample output. Formats are implemented in FrameworkUtils.GetTimeByFormat.")]
        public TxtTimeFormat timeFomat;
        private readonly Service<TimeService> timeService = new();
        private Coroutine coroutine;

        private Action onTimeUp;
        
        public void Show(long sec, Action onTimeUp = null)
        {
            gameObject.SetActive(true);
            StopCount();
            this.onTimeUp = onTimeUp;
            if (gameObject.activeInHierarchy)
                coroutine = StartCoroutine(timeService.Instance.DoActionRealtime(sec, UpdateTime));
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            StopCount();
        }
        
        private void UpdateTime(long sec)
        {
            txtTime.text = FrameworkUtils.GetTimeByFormat(sec, timeFomat);
            
            if (sec > 0) return;
            onTimeUp?.Invoke();
            onTimeUp = null;
            Hide();
        }

        private void StopCount()
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }
}