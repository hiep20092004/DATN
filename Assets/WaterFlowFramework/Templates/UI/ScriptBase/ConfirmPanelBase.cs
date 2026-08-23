using System;
using WaterFlow.Framework.UIModule;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ConfirmPanelBase : Panel
{
	[SerializeField] private TMP_Text txtContent;
	private Action onYes, onNo;
	public override void OnSetup()
	{
		base.OnSetup();
	}

	public override void Open(UIData uiData)
	{
		base.Open(uiData);
		if (uiData != null)
		{
			if (uiData.TryGet("content", out string content))
				txtContent.text = content;
			if(uiData.TryGet("onYes", out Action onYes))
				this.onYes = onYes;
			if(uiData.TryGet("onNo", out Action onNo))
				this.onNo = onNo;
		}
	}

	public virtual void OnClickYes()
	{
		onYes?.Invoke();
		onYes = null;
		Close();
	}

	public virtual void OnClickNo()
	{
		onNo?.Invoke();
		onNo = null;
		Close();
	}
}