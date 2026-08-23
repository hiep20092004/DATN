using System.Collections.Generic;

namespace WaterFlow.Game
{
    public interface IElementProvider
    {
        IReadOnlyList<LevelBlockBehavior> ActiveBlocks { get; }

        /// <summary>
        /// Live spawned instances for <paramref name="elementType"/>.
        /// Supports Block, Gate, Generator, InteractableObject, and Obstacle.
        /// </summary>
        IReadOnlyList<T> GetElements<T>(ElementType elementType) where T : class;
    }
}
