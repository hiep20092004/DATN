using WaterFlow.Framework.Systems.InventoryManagement.GameResources;

namespace WaterFlow.Enums
{
    public enum GameResource : byte
    {
        None = 0,
        Coin = 1,
        Live = 2,
        UnlimitedLive = 3,
        Star = 4,

        MAX = 255
    }

    public enum GameResourceType : byte
    {
        None = 0,
        Currency,
        Booster,
        PreBooster,
    }

    public static class GameResourceHelper
    {
        public static GameResourceType ResourceType(this GameResource resource)
        {
            switch (resource)
            {
                case GameResource.Coin:
                case GameResource.Live:
                    return GameResourceType.Currency;
            }

            return GameResourceType.None;
        }

        public static GameResourceKey ToGameResourceKey(this GameResource resource)
        {
            return new GameResourceKey()
            {
                gameResource = resource,
            };
        }

        public static GameResourceKey ToGameResourceKey(this GameResource resource, int id)
        {
            return new GameResourceKey()
            {
                gameResource = resource,
                id = id,
            };
        }
    }
}
