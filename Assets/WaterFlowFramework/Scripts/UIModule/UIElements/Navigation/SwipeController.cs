using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class SwipeController : MonoBehaviour
{
    public Scrollbar scrollbar;
    private float scroll_pos = 0;
    float[] pos;
    int btnNumber;
    [FormerlySerializedAs("uiNavigateBar")] public UINavigateSwipeBar uiNavigateSwipeBar;
    float distance;
    public int startTab;
    private int currentTab, targetTab;
    private int totalTabs;
    [SerializeField] private float valueToSwitchTab = 0.2f;
    [SerializeField] private float speedSwitchTab = 0.1f;
    private bool block = true;
    
    public void Start()
    {
        currentTab = startTab;
        totalTabs = transform.childCount;
        pos = new float[totalTabs];
        distance = 1f / (pos.Length - 1f);
        //if (pos.Length > 0)
            //scrollbar.value = startTab * 1.0f / pos.Length;
        scroll_pos = scrollbar.value;
        
        for (int i = 0; i < pos.Length; i++)
        {
            pos[i] = distance * i;
        }
    }

    private float deltaSwipeX;

    private Vector3 startPoint, endPoint;
    // Update is called once per frame
    void Update()
    {

        if (Input.GetMouseButtonUp(0) && !block)
        {
            MoveToNextTab();
        }
        
        if (Input.GetMouseButton(0))
        {
            scroll_pos = scrollbar.value;
        }
        else
        {
            scrollbar.value = Mathf.Lerp(scrollbar.value, pos[currentTab], speedSwitchTab);
        }

        if (block)
        {
            block = false;
        }
        
    } 

    private void MoveToNextTab()
    {
        float delta = scroll_pos - pos[currentTab];
        if (Mathf.Abs(delta) > distance * valueToSwitchTab)
        {
            if (delta > 0 && currentTab < totalTabs - 1)
            {
                SetNewTab(currentTab + 1);
            }
            else if (delta < 0 && currentTab > 0)
            {
                SetNewTab(currentTab - 1);
            }
        }
    }

    private void SetNewTab(int newTab)
    {
        currentTab = newTab;
        uiNavigateSwipeBar.SetNewTab(newTab);
    }

    public void UpdateTab(int newTab)
    {
        block = true;
        currentTab = newTab;
    }
}