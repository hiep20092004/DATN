using UnityEngine;
using Sirenix.OdinInspector;
using WaterFlow.Framework.UIModule;

[CreateAssetMenu(fileName = "TweenConfig", menuName = "Configs/Tween Config")]
public class PanelTweenConfig : ScriptableObject
{
    public UITweenType tweenType;
    
    [ShowIf("@tweenType == UITweenType.Fade || tweenType == UITweenType.FadeGroup || tweenType == UITweenType.Scale")]
    public float from;
    
    [ShowIf("@tweenType == UITweenType.Fade || tweenType == UITweenType.FadeGroup || tweenType == UITweenType.Scale")]
    public float to = 1;
    
    [ShowIf("@tweenType == UITweenType.Move || tweenType == UITweenType.LocalMove || tweenType == UITweenType.RectLocalMove")]
    public Vector3 moveFrom;
    
    [ShowIf("@tweenType == UITweenType.Move || tweenType == UITweenType.LocalMove || tweenType == UITweenType.RectLocalMove")]
    public Vector3 moveTo;
    
    public float duration;
    public float delay;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
}
