using System.Collections.Generic;

namespace WaterFlow.Game
{
    public static class ColorManager
    {
        private static List<IColorElement> registeredElements;
        
        public static void Init()
        {
            registeredElements = new List<IColorElement>();
#if UNITY_EDITOR
            var debug = KeyLockDebugVisualizer.Instance;
#endif 
        } 

        public static void RegisterElement(IColorElement element)
        {
            registeredElements.Add(element);
        }

        public static void UnregisterElement(IColorElement element)
        {
            registeredElements.Remove(element);
        }
        
        public static IColorElement GetColorElement(BlockColor color)
        {
            registeredElements.RemoveAll(e => e == null);
            
            foreach(var element in registeredElements)
            {
                if (element.IsActive && element.Color == color)
                {
                    return element;
                }
            }

            return null;
        }
        
    }
}