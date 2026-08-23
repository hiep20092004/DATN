using UnityEngine;
using UnityEngine.Events;

namespace WaterFlow.Framework.Helper
{
    public class ColliderButton : MonoBehaviour
    {
        [SerializeField] private UnityEvent unityEvent;

        private void OnMouseUpAsButton()
        {
            unityEvent?.Invoke();
        }
    }
}
