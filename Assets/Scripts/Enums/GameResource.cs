using WaterFlow.Framework.Systems.InventoryManagement.GameResources;

namespace WaterFlow.Enums
{
    public enum GameResource : byte
    {
        None,
        NoAds = 1,
        Coin = 2,
        Live = 3,
        UnlimitedLive = 4,
        Star = 5,
        Freeze = 6,
        Expand = 7,
        Hammer = 8,
        WaterGun = 9,
        PreClock = 10,
        PreWand = 11,
        UnlimitedPreClock = 12,
        UnlimitedPreWand = 13,

        Card_Randomx1 = 100,
        Card_Randomx2 = 101,
        Card_Randomx3 = 102,
        Card_Randomx4 = 103,
        Card_Randomx5 = 104,
        Card_Randomx6 = 105,
        Card_Special = 106,

        FruitPassKey = 200,
        
        MAX = 201,
        Token_Rail

    }

    public enum GameResourceType : byte
    {
        None = 0,
        Currency,
        Booster,
        PreBooster,
        Card,
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
                case GameResource.Freeze:
                case GameResource.Expand:
                case GameResource.Hammer:
                case GameResource.WaterGun:
                    return GameResourceType.Booster;
                case GameResource.PreClock:
                case GameResource.PreWand:
                    return GameResourceType.PreBooster;
                case GameResource.Card_Randomx1:
                case GameResource.Card_Randomx2:
                case GameResource.Card_Randomx3:
                case GameResource.Card_Randomx4:
                case GameResource.Card_Randomx5:
                case GameResource.Card_Randomx6:
                case GameResource.Card_Special:
                    return GameResourceType.Card;
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
