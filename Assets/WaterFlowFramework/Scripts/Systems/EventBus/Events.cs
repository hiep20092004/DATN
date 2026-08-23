using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Framework.Systems.EventBus
{
    public interface IEvent
    {
    }

    public struct OpenGameEvent : IEvent
    {
    }

    public struct LevelLoadedEvent : IEvent
    {
        public int level;
    }
    
    public struct LevelStartedEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int realLevel;
        public int phase;
    }

    public struct LevelEndedEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int realLevel;
        public bool success;
        public int phase;
        public string cause;
        public bool suppressProgression;
    }

    public struct LevelStartTurnEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int realLevel;
        public int phase;
    }

    public struct LevelEndTurnEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int realLevel;
        public int levelType;
        public bool success;
        public int continueTimes;
    }

    public struct LevelQuitEvent : IEvent
    {
        public string cause;
    }

    public struct LevelStuckEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public string cause;
    }

    public struct LevelContinueEvent : IEvent
    {
        public string by;
    }

    public struct GameStateChangeEvent : IEvent
    {
        public GameState gameState;
    }
    
    public struct GamePausedEvent : IEvent
    {
        public bool paused;
        public string reason;
    }

    public struct PhaseStartedEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int phase;
    }

    public struct PhaseEndedEvent : IEvent
    {
        public GameMode gameMode;
        public int level;
        public int phase;
        public bool success;
    }

    public struct UseBoosterEvent : IEvent
    {
        public GameResource booster;
    }

    public struct BlockMoveEvent : IEvent
    {

    }

    public struct BlockDestroyedEvent : IEvent
    {

    }

    public struct SwitchPlacementEvent : IEvent
    {
        public GamePlacement from;
        public GamePlacement to;
        public Status status;

        public enum Status : byte
        {
            Start = 0,
            End = 1,
        }
    }

    public struct UpdatePlacementEvent : IEvent
    {
        public string placement;
    }

    public struct UpdateScreenEvent : IEvent
    {
        public string screen;
    }

    public struct ClickShortcutEvent : IEvent
    {
        public string shortcut;
    }

    public struct EarnResourceEvent : IEvent
    {
        public GameResourceKey resource;
        public int value;
        public string spendType;
        public string spendId;
        public bool isFirstBuy;
        public string source;

        public EarnResourceEvent(GameResourceKey gameResource, int value, EarnResourceLogData logData)
        {
            resource = gameResource;
            this.value = value;
            spendType = logData.spendType;
            spendId = logData.spendId;
            isFirstBuy = logData.isFirstBuy;
            source = logData.source;
        }
    }

    public struct SpendResourceEvent : IEvent
    {
        public GameResourceKey resource;
        public int value;
        public string earnType;
        public string earnId;
        public string source;
    
        public SpendResourceEvent(GameResourceKey gameResource, int value, SpendResourceLogData logData)
        {
            resource = gameResource;
            this.value = value;
            earnType = logData.earnType;
            earnId = logData.earnId;
            source = logData.source;
        }
    }
    //
    // public struct AddRewardEvent : IEvent
    // {
    //     public string source;
    //     public bool updateVisual;
    //     public RewardData rewardData;
    //     public EarnResourceLogData logData;
    // }
    //
    // public struct AddResourceEvent : IEvent
    // {
    //     public string source;
    //     public bool updateVisual;
    //     public ResourceData resourceData;
    //     public EarnResourceLogData logData;
    // }
    //
    // public struct ReduceResourceEvent : IEvent
    // {
    //     public string source;
    //     public GameResourceKey key;
    //     public bool delayUpdateVisual;
    //     public int quantity;
    //     public SpendResourceLogData logData;
    // }
    //
    public struct AddResourceVisualEvent : IEvent
    {
        public string source;
        public GameResourceKey key;
        public int visualQuantity;
        public Vector3 position;
        public BaseCollectEffect collectEffect;
    }
    //
    // public struct ReduceResourceVisualEvent : IEvent
    // {
    //     public string source;
    //     public GameResourceKey key;
    //     public int visualQuantity;
    //     public Vector3 position;
    //     public BaseCollectEffect collectEffect;
    // }

        public struct HomeTasksCompletedEvent : IEvent { }

        public struct HomeSetupEvent : IEvent { }

}