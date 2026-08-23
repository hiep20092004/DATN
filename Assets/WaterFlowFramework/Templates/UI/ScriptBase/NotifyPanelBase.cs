using System;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEngine;

public class NotifyPanelBase : Panel
{
	[SerializeField] private TMP_Text txtContent;
	private Action onClose;
	
	public class Data : UIData
	{
		public string content;
		public Action onClose = null;
	}
	
	public override void OnSetup()
	{
		base.OnSetup();
	}

	public override void Open(UIData uiData)
	{
		base.Open(uiData);

		var data = uiData as Data;
		this.txtContent.text = data.content;
		this.onClose = data.onClose;
	}

	public override void OnOpenCompleted()
	{
		base.OnOpenCompleted();
	}

	public override void Close()
	{
		base.Close();
		onClose?.Invoke();
		onClose = null;
	}

	protected override void OnCloseCompleted()
	{
		base.OnCloseCompleted();
	}
}