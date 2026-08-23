using UnityEngine;

namespace WaterFlow.Framework.UIModule
{
    [CreateAssetMenu(menuName = "TweenDataGroup")]
    public class TweenDataGroup : ScriptableObject
    {
        public TweenData[] tweenDatas;
    }
}