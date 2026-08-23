using System;
using UnityEngine;

public class ScaleFitScreen : MonoBehaviour
{
    private float originalWidth = 1080;
    private float originalHeight = 1920f;

    public bool isScaleFactor;

    public bool isBackground;

    public Canvas canvasUI;

    public ParticleSystem efxSystem;

    [SerializeField]
    private Vector3 initSize;

    private void Reset()
    {
        efxSystem = this.GetComponent<ParticleSystem>();
        initSize = efxSystem.shape.scale;

    }

    private void Start()
    {
        ScaleToScreen();
    }
    
    [ContextMenu("Scale To Screen (Editor)")]
    private void ScaleToScreenEditor()
    {
        ScaleToScreen();
    }

    private void ScaleToScreen()
    {
        if (isBackground)
        {
            ScaleImage();
        }
        else
        {
            ScaleParticle();
        }
    }

    private void ScaleParticle()
    {
        float targetWidth = Screen.width / canvasUI.scaleFactor;
        float targetHeight = Screen.height / canvasUI.scaleFactor;

        float scaleX = targetWidth / originalWidth;
        float scaleY = targetHeight / originalHeight;

        var scaleFactor = isScaleFactor ? (1 / transform.localScale.x) : 1;


        var shape = GetComponent<ParticleSystem>().shape; // Get the Shape module

        Debug.Log("Scale x/y" + scaleX + "/" + scaleY);

        Vector3 newSize = new Vector3(initSize.x * scaleX, initSize.y * scaleY, initSize.z) / scaleFactor;

        shape.scale = newSize;
    }

    private void ScaleImage()
    {
        float targetWidth = Screen.width / canvasUI.scaleFactor;
        float targetHeight = Screen.height / canvasUI.scaleFactor;

        float scaleX = targetWidth / originalWidth;
        float scaleY = targetHeight / originalHeight;

        var scaleFactor = isScaleFactor ? (1 / transform.localScale.x) : 1;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer)
        {
            transform.localScale = new Vector3(scaleX, scaleY, 1) / scaleFactor;
            return;
        }

        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform)
        {
            rectTransform.localScale = new Vector3(scaleX, scaleY, 1) / scaleFactor;
        }
    }
}
