using WaterFlow.Game.Common;
using WaterFlow.Framework.UIModule;
using UnityEngine;
using UnityEngine.UI;

namespace Popup
{
    public class PopupMessageInfoParam
    {
        public RectTransform Anchor;
        public string PlainMessage;
        public RectTransform BoundsRect;
        /// <summary>Force a specific arrow direction. Null = auto-detect from available space within BoundsRect.</summary>
        public MessageInfoView.ArrowDirection? Direction;
        /// <summary>When true, the anchor GameObject is destroyed when the popup closes (used for throwaway world-space anchors).</summary>
        public bool DestroyAnchorOnClose;
    }
    
    public class PopupMessageInfo : Panel
    {
        [SerializeField] private Button backgroundButton;
        [SerializeField] private MessageInfoView messageInfoView;

        private PopupMessageInfoParam param;
        public override void OnSetup()
        {
            base.OnSetup();
            backgroundButton.onClick.AddListener(Close);
        }

        public override void Open(UIData uiData)
        {
            base.Open(uiData);
            if (uiData != null && uiData.TryGet<PopupMessageInfoParam>(UIDataKey.Content, out var param))
            {
                this.param = param;
            }
            else
            {
                return;
            }
            if (messageInfoView != null)
            {
                string message = param.PlainMessage;
                messageInfoView.ShowAtAnchor(param.Anchor, message, param.BoundsRect, param.Direction);
            }
        }

        public override void OnOpenCompleted()
        {
            base.OnOpenCompleted();
        }

        public override void Close()
        {
            if (messageInfoView != null)
                messageInfoView.Hide();
            base.Close();
        }

        protected override void OnCloseCompleted()
        {
            base.OnCloseCompleted();
            backgroundButton.onClick.RemoveListener(Close);

            if (param != null && param.DestroyAnchorOnClose && param.Anchor != null)
            {
                Destroy(param.Anchor.gameObject);
                param.Anchor = null;
            }
        }
        
    }
}