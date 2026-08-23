using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;
using UnityEngine.UI;

public abstract class UINavigateBarBase : MonoBehaviour
{
    protected Dictionary<NavigationType, UINavigationItem> items = new Dictionary<NavigationType, UINavigationItem>();
    protected Dictionary<NavigationType, UITabBase> tabs = new Dictionary<NavigationType, UITabBase>();
    
    public Transform container;
    public RectTransform highlight;
    public NavigationType tabStart;
    protected NavigationType currTab = NavigationType.None;
    public NavigationType CurrTab => currTab;
    public Transform tabContainer;
    public List<NavigationType> navigationTypes;
    
    public virtual void Init()
    {
        
        //PoolingManager.CleanContainer(container);
        navigationTypes = new List<NavigationType>();
        foreach (Transform child in tabContainer)
        {
            //child.gameObject.SetActive(true);
            var tab = child.GetComponent<UITabBase>();
            if(tab == null) continue;
            tabs.Add(tab.type, tab);
            navigationTypes.Add(tab.type);
        }

        foreach(Transform child in container)
        {
            var item = child.GetComponent<UINavigationItem>();
            item.InitData(this);
            items.Add(item.type, item);
        }
        EventBus<UpdateScreenEvent>.Raise(new (){screen = "H"});

    }

    public virtual void SetFirstTab(NavigationType tab)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
    }

    public abstract void SwitchTab(NavigationType type);
}

