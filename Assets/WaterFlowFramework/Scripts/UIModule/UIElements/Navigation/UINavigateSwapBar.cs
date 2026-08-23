using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;

public class UINavigateSwapBar : UINavigateBarBase
{
    private void Start()
    {
        Init();
        DOVirtual.DelayedCall(0.25f, () =>
        {
            SelectType(tabStart);
        });
    }
    
    public void SelectType(NavigationType type)
    {
        if (type == currTab) return;
        currTab = type;
        foreach(var i in items)
        {
            i.Value.OnDeselected();
        }
        foreach(var tab in tabs)
        {
            if(tab.Key == type)
            {
                tab.Value.gameObject.SetActive(true);
                tab.Value.OnShow();
            }
            else
            {
                tab.Value.OnHide();
                tab.Value.gameObject.SetActive(false);
            }
        }

        items[type].OnSelected();
        highlight.DOMoveX(items[type].transform.position.x, 0.2f);

    }

    public override void SwitchTab(NavigationType type)
    {
        SelectType(type);
    }
}
