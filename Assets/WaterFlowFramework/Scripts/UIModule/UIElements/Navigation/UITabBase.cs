using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;

public class UITabBase : MonoBehaviour
{
    [SerializeField] protected string screenName;
    private RectTransform rectTransform;
    private RectTransform screenRect;
    public NavigationType type;
    protected bool isActive = false;
    private Panel panel;
    [SerializeField] private bool fitSize;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private string placement;

    protected virtual void Awake()
    {
        this.rectTransform = GetComponent<RectTransform>();
        screenRect = transform.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        panel = GetComponentInChildren<Panel>();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    protected virtual void Start()
    {
        if (fitSize)
        {
            rectTransform.sizeDelta = new Vector2(screenRect.sizeDelta.x + 10, rectTransform.sizeDelta.y);
        }
    }

    public virtual void OnShow()
    {
        if(isActive) return;
        isActive = true;
        gameObject.SetActive(true);
        
        // Fade in animation
        FadeIn();
        EventBus<UpdateScreenEvent>.Raise(new (){screen = screenName});
        EventBus<UpdatePlacementEvent>.Raise(new(){placement = placement});
        
    }
    
    public virtual void OnHide()
    {
        if(!isActive) return;
        isActive = false;
        FadeOut();
    }

    public virtual void FadeIn()
    {
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, fadeInDuration);
    }

    public virtual void FadeOut()
    {
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() => {
            gameObject.SetActive(false);
        });
    }

    
}
