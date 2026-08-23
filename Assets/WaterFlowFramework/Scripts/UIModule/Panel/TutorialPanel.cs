using UnityEngine.UI;

namespace WaterFlow.Framework.UIModule
{
    public abstract class TutorialPanel : Panel
    {
        public Button closeBtn;
        
        public override void OnSetup()
        {
            base.OnSetup();
            closeBtn.onClick.AddListener(Close);
        }

        protected override void OnCloseCompleted()
        {
            base.OnCloseCompleted();
            closeBtn.onClick.RemoveAllListeners();
        }
    }
}