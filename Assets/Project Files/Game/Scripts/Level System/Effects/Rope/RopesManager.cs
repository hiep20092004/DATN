using System.Collections.Generic;

namespace WaterFlow.Game
{
    public static class RopesManager
    {
        private static List<RopeEffectBehavior> registeredElements;

        public static void Init()
        {
            registeredElements = new List<RopeEffectBehavior>();
        }

        public static void RegisterElement(RopeEffectBehavior element)
        {
            registeredElements.Add(element);
        }

        public static void UnregisterElement(RopeEffectBehavior element)
        {
            registeredElements.Remove(element);
        }

        public static RopeBehavior LinkRope(BlockColor color)
        {
            registeredElements.RemoveAll(e => !e);
            
            foreach(RopeEffectBehavior element in registeredElements)
            {
                foreach(RopeBehavior rope in element.Ropes)
                {
                    if (!rope.IsLinked && rope.RopeColor == color)
                    {
                        rope.OnRopeLinked();
                        return rope;
                    }
                }
            }

            return null;
        }
    }
}