using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UINavigateSwipeBar : UINavigateBarBase
{
    [SerializeField] private SwipeController swipeController;

	private void Start()
	{
        swipeController.enabled = false;
        Init();
	}
	

	public override void Init()
	{
        base.Init();

        DOVirtual.DelayedCall(0.025f, () =>
        {
            swipeController.enabled = true;
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
                tab.Value.OnShow();
			}
			else
			{
                tab.Value.OnHide();
			}
		}

        items[type].OnSelected();
        //highlight.DOMove(items[type].transform.position, 0.2f);

    }

    public override void SwitchTab(NavigationType type)
    {
	    SelectType(type);
        swipeController.UpdateTab(navigationTypes.IndexOf(type));
    }
    public void SetNewTab(int newTab)
    {
	    SelectType(navigationTypes[newTab]);
    }
}


