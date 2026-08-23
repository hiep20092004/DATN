using WaterFlow.Game;
using UnityEngine;

namespace Popup
{
    public class PopupTooltip : Singleton<PopupTooltip>
    {
        [SerializeField] NotifyTextPresenter textNoTarget;

        private RectTransform rectTransform;

        protected override void OnAwake()
        {
            base.OnAwake();
            rectTransform = GetComponent<RectTransform>();
        }

        public void Show(string message)
        {
            rectTransform.localScale  = Vector3.one;
            textNoTarget?.Show(message);
        }
    }
    
}