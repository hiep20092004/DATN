namespace WaterFlow.Enums
{
    public enum AudioId : ushort
    {
        None = 0,

        // UI
        ButtonClick = 1,
        Items_Collected = 2,

        // Game State
        Win = 10,
        Lose = 11,

        // Music
        BGM_Home = 100,
        BGM_Ingame = 101,
    }
}
