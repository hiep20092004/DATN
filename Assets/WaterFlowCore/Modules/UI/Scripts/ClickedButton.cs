using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.AudioManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WaterFlow.Core
{
    public class ClickedButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        //[SerializeField] private KeySound key;
        [SerializeField] private bool isScale = true;
        [Sirenix.OdinInspector.ShowIf(nameof(isScale))] [SerializeField] private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1.1f);
        [Sirenix.OdinInspector.ShowIf(nameof(isScale))] [SerializeField] private Vector3 clickScale = new Vector3(0.9f, 0.9f, 0.9f);
        [Sirenix.OdinInspector.ShowIf(nameof(isScale))] [SerializeField] private Vector3 defaultScale = new Vector3(1f, 1f, 1f);
        [SerializeField] private float duration = 0.09f;

        private void Reset(){
            button = GetComponent<Button>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(!button.IsActive()) return;
            GameSystem.GetService<AudioService>()?.PlaySound(AudioId.ButtonClick);
            Haptic.Play(Haptic.PATTERN_BUTTON_CLICK);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isScale)
            {
                transform.DOScale(hoverScale, duration);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isScale)
            {
                transform.DOScale(defaultScale, duration);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isScale)
            {
                transform.DOScale(defaultScale, duration);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isScale)
            {
                transform.DOScale(clickScale, duration);
            }
        }
    }
}