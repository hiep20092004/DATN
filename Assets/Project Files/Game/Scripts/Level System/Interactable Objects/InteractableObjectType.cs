namespace WaterFlow.Game
{
    public enum InteractableObjectType
    {
        None = 0,

        // RETIRED — implementations removed; values reserved because level data and
        // LevelDatabase.interactableObjects serialize this enum by integer.
        ColorObstacle = 1,
        MoveableColorObstacle = 2,

        Grinder = 3
    }
}
