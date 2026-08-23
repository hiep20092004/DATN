using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Core
{
    [RequireComponent(typeof(Canvas)), RequireComponent(typeof(GraphicRaycaster))]
    public abstract class UIPage  : MonoBehaviour, ISceneSavingCallback
    {
        [Hide] [SerializeField] Component[] registeredElements;

        protected bool isPageDisplayed;

        protected Canvas canvas;
        protected GraphicRaycaster graphicRaycaster;
        protected bool isCached;
        
        private string defaultName;
        private IUIPageElement[] pageElements;
        
        
        public bool IsPageDisplayed { get => isPageDisplayed; set => isPageDisplayed = value; }
        public bool IsCached => isCached;
        public Canvas Canvas => canvas;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;

        public void CacheComponents()
        {
            defaultName = name;

            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
        }

        public void PreparePage()
        {
            isPageDisplayed = false;
            canvas.enabled = false;

            pageElements = new IUIPageElement[registeredElements.Length];
            if(!pageElements.IsNullOrEmpty())
            {
                for (int i = 0; i < pageElements.Length; i++)
                {
                    pageElements[i] = registeredElements[i] as IUIPageElement;
                    pageElements[i].Init(this);
                }
            }
        }

        public abstract void Init();

        public void EnableCanvas()
        {
            isPageDisplayed = true;

            canvas.enabled = true;

            if(!pageElements.IsNullOrEmpty())
            {
                for (int i = 0; i < pageElements.Length; i++)
                {
                    pageElements[i]?.OnPageStateChanged(true);
                }
            }

#if UNITY_EDITOR
            name = $"{defaultName} (Active)";
#endif
        }

        public void DisableCanvas()
        {
            isPageDisplayed = false;

            canvas.enabled = false;

            for (int i = 0; i < pageElements.Length; i++)
            {
                pageElements[i]?.OnPageStateChanged(false);
            }

#if UNITY_EDITOR
            name = defaultName;
#endif
        }

        public abstract void PlayShowAnimation();
        public abstract void PlayHideAnimation();

        public virtual void Unload()
        {
            isPageDisplayed = false;

            canvas.enabled = false;
        }

        public void MarkAsCached()
        {
            isCached = true;
        }

        public bool OnPrefabSaving()
        {
            Component[] cachedPageElements = GetComponentsInChildren(typeof(IUIPageElement));

            if(registeredElements == null || registeredElements.Length != cachedPageElements.Length)
            {
                registeredElements = cachedPageElements;

                return true;
            }

            for(int i = 0; i < registeredElements.Length; i++)
            {
                if (!registeredElements[i])
                {
                    registeredElements = cachedPageElements;

                    return true;
                }
            }

            for (int i = 0; i < cachedPageElements.Length; i++)
            {
                if(!ReferenceEquals(registeredElements[i], cachedPageElements[i]))
                {
                    registeredElements = cachedPageElements;

                    return true;
                }
            }

            return false;
        }

        public void OnSceneSaving()
        {
            void SaveElements(Component[] elements)
            {
                registeredElements = elements;

                RuntimeEditorUtils.SetDirty(this);
            }

            Component[] cachedPageElements = GetComponentsInChildren(typeof(IUIPageElement));

            if (registeredElements == null || registeredElements.Length != cachedPageElements.Length)
            {
                SaveElements(cachedPageElements);

                return;
            }

            for (int i = 0; i < registeredElements.Length; i++)
            {
                if (!registeredElements[i])
                {
                    SaveElements(cachedPageElements);

                    return;
                }
            }

            for (int i = 0; i < cachedPageElements.Length; i++)
            {
                if (!ReferenceEquals(registeredElements[i], cachedPageElements[i]))
                {
                    SaveElements(cachedPageElements);

                    return;
                }
            }
        }
    }
}