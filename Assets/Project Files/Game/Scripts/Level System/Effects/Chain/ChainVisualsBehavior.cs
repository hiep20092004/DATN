using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class ChainVisualsBehavior : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI amountText;
        [SerializeField] Transform lockCenter;
        public TextMeshProUGUI AmountText => amountText;
        public Transform LockCenter => lockCenter;

        
    }
}