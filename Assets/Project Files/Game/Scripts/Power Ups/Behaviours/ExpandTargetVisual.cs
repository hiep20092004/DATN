using UnityEngine;
using UnityEngine.EventSystems;

namespace WaterFlow.Game
{
    public class ExpandTargetVisual : MonoBehaviour, IClickableObject, IPointerClickHandler
    {
        public Vector2Int Position { get; private set; }

        public void Init(Vector2Int position)
        {
            Position = position;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (PowerUpController.SelectedPowerUp)
            {
                PowerUpController.ApplyToElement(this, transform.position);
            }
        }

        public void OnObjectClicked()
        {
        }

        public bool CanBeClicked()
        {
            return true;
        }

        public void OnClickBlocked()
        {
        }
    }
}