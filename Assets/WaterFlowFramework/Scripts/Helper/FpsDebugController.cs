using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FpsDebugController : MonoBehaviour
{
    public int fps = 2;

    public int vSync;

    public Button changeFpsButton;
    public Button changeVSyncButton;
    [SerializeField] private bool setOnRootWhenEnable;

    private void OnEnable()
    {
        if(setOnRootWhenEnable)
            transform.SetParent(null);
    }

    void Start()
    {
        if (changeFpsButton != null)
        {
            changeFpsButton.onClick.AddListener(ChangeFps);
            if (changeFpsButton.GetComponentInChildren<TMP_Text>() != null)
                changeFpsButton.GetComponentInChildren<TMP_Text>().text = $"fps ({Application.targetFrameRate})";
        }

        if (changeVSyncButton != null)
        {
            changeVSyncButton.onClick.AddListener(ChangeVsync);
            if (changeVSyncButton.GetComponentInChildren<TMP_Text>() != null)
                changeVSyncButton.GetComponentInChildren<TMP_Text>().text = $"vsync ({QualitySettings.vSyncCount})";
        }
//
//        QualitySettings.vSyncCount = 0;
//        if (QualitySettings.vSyncCount > 0)
//            Application.targetFrameRate = 60;
//        else
//            Application.targetFrameRate = 60;

//        if (setTargetFps)
//        {
//            QualitySettings.vSyncCount = 0;
//            Application.targetFrameRate = 60;
//        }
//        
//        QualitySettings.vSyncCount = vsync % 3;
        _rect = new Rect(20, 0, Screen.width, Screen.height * 2f / 100);
        _rect2 = new Rect(20, 50, Screen.width, Screen.height * 2f / 100);
        _rect3 = new Rect(20, 100, Screen.width, Screen.height * 2f / 100);
    }

    private void ChangeFps()
    {
        fps++;
        if (fps == 5)
        {
            fps = -1;
            Application.targetFrameRate = -1;
            return;
        }
        Application.targetFrameRate = (fps % 4 + 1) * 30;
        if(    changeFpsButton.GetComponentInChildren<TMP_Text>() != null)
        changeFpsButton.GetComponentInChildren<TMP_Text>().text = $"fps ({Application.targetFrameRate})";
    }

    private void ChangeVsync()
    {
        vSync++;
        QualitySettings.vSyncCount = vSync % 4;
        if(    changeFpsButton.GetComponentInChildren<TMP_Text>() != null)
            changeVSyncButton.GetComponentInChildren<TMP_Text>().text = $"vsync ({QualitySettings.vSyncCount})";
    }

    float _deltaTime;
    private GUIStyle _style;
    
    private Rect _rect;
    private Rect _rect2;
    private Rect _rect3;

    private GUIStyle Style
    {
        get
        {
            if (_style == null)
            {
                _style = new GUIStyle();
                Style.alignment = TextAnchor.UpperLeft;
                Style.fontSize = (int) ((Screen.height * 2.5f) / 100f);
                Style.normal.textColor = Color.red; //new Color(0.0f, 1f, 0.5f, 1.0f);
            }

            return _style;
        }
    }

    void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    public static string message = "null";
    public static string message2 = "null";

    void OnGUI()
    {
        //   GUI.Label(_rect, string.Format("{0:0.0} ms ({1:0.} fps)" + message, _deltaTime * 1000.0f, 1.0f / _deltaTime), Style);
        GUI.Label(_rect3,
            string.Format(
                "{0:0.0} ms ({1:0.} fps)\n" +
                $"targetFR = {Application.targetFrameRate}\nvSyncCount = {QualitySettings.vSyncCount} \nRefresh-rate = " +
                Screen.currentResolution.refreshRate, _deltaTime * 1000.0f, 1.0f / _deltaTime), Style);
        //    GUI.Label(_rect, $"{message}", Style);
        //   GUI.Label(_rect2, $"{Camera.main.orthographicSize}", Style);
        //    GUI.Label(_rect3, $"{Camera.main.pixelWidth}/{Camera.main.pixelHeight}={Camera.main.pixelWidth * 1f / Camera.main.pixelHeight}", Style);
    }

    public void TestAspect()
    {
    }
}