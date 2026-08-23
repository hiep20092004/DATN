using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Framework.UIModule
{
    public class UIProgressBar : MonoBehaviour
    {
        public Slider slider;
        public TMP_Text txtValue;
        public GameObject fullObj, normalObj;

        public bool IsFull => _isFull;
        private bool _isFull = false;

        private void Awake()
        {
            slider.interactable = false;
        }

        public void SetData(float value, float maxValue)
        {
            slider.maxValue = maxValue;
            slider.value = value;

            if (txtValue) txtValue.text = $"{value}/{maxValue}";
            if (value >= maxValue)
            {
                _isFull = true;
                if (fullObj)
                    fullObj.SetActive(true);
                if (normalObj)
                    normalObj.SetActive(false);
            }
            else
            {
                _isFull = false;
                if (fullObj)
                    fullObj.SetActive(false);
                if (normalObj)
                    normalObj.SetActive(true);
            }
        }

        public void UpdateValue(float newValue, float duration = 0.15f, Action onComplete = null)
        {
            slider.DOValue(newValue, duration).OnComplete(() =>
            {
                if (newValue >= slider.maxValue)
                {
                    onComplete?.Invoke();
                    if (fullObj)
                        fullObj.SetActive(true);
                    if (normalObj)
                        normalObj.SetActive(false);
                }
                else
                {
                    if (fullObj)
                        fullObj.SetActive(false);
                    if (normalObj)
                        normalObj.SetActive(true);
                }
            });
            if (txtValue) txtValue.text = $"{newValue}/{slider.maxValue}";
        }

        public void AddValue(float valueAdd, float duration = 0.15f)
        {
            var newValue = slider.value + valueAdd;
            if (newValue >= slider.maxValue) _isFull = true;
            UpdateValue(newValue, duration);
        }

        public bool CheckPreFull()
        {
            //Check
            return slider.value + 1 >= slider.maxValue;
        }
    }
}