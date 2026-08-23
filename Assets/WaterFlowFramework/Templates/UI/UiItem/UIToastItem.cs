using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using WaterFlow.Framework.Helper;

public class UIToastItem : MonoBehaviour
{
    public TMP_Text txtContent;
    
    public void SetData(string content, string param = null)
	{
        txtContent.SetLocalize(content);

        if (param != null)
        {
	        txtContent.SetLocalizeParam("VALUE", param);
        }
        
        //localize.SetTerm(content);
        Color color = txtContent.color;
        color.a = 0.5f;
        txtContent.color = color;
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one * 0.5f;
        txtContent.DOFade(1, 0.25f);
        transform.DOScale(1, 0.25f);
        //transform.DOLocalJump(Vector3.up * 400,1 ,1, 0.75f).SetDelay(0.5f);
        transform.DOLocalMoveY(400, 0.75f).SetDelay(0.5f).SetEase(Ease.OutQuad);
        txtContent.DOFade(0, 0.75f).SetDelay(0.5f);

	}
}
