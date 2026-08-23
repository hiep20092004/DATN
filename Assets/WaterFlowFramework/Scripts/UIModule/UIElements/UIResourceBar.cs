using System.Collections.Generic;
using System.Linq;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.UIElements
{
    public class UIResourceBar : MonoBehaviour
    {
        private static readonly List<UIResourceBar> resourceBars = new();
        [SerializeField] private Transform container;
        [SerializeField] private List<UICurrency> uiCurrencies;
        [SerializeField] private bool hideOnLostFocus = true;

        public static UIResourceBar Main => resourceBars?.Count > 0 ? resourceBars[^1] : null;

        public void OnEnable()
        {
            Main?.OnLostFocus();
            resourceBars.Add(this);
            OnFocus();
        }

        private void OnDisable()
        {
            resourceBars.Remove(this);
            Main?.OnFocus();
        }

        protected virtual void OnFocus()
        {
            container.gameObject.SetActive(true);
        }

        protected virtual void OnLostFocus()
        {
            if (hideOnLostFocus)
                container.gameObject.SetActive(false);
        }

        public UICurrency GetUICurrency(GameResource currency)
        {
            return uiCurrencies.FirstOrDefault(e => e.resource == currency);
        }
    }
}