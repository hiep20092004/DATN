using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.TimeManagement;
using WaterFlow.Framework.Systems.UserData;
using WaterFlow.Game;

/// <summary>
/// Static read-through facade over <see cref="GameSystem"/>'s service registry.
/// Gameplay code reaches services through here instead of resolving them by hand,
/// so a service swap is a single edit in this file.
/// </summary>
public class Services
{
    public static GameAudioService AudioService => GameSystem.GetService<GameAudioService>();
    public static BoosterService BoosterService => GameSystem.GetService<BoosterService>();
    public static GameplayConfigService GameplayConfig => GameSystem.GetService<GameplayConfigService>();
    public static TransitionService TransitionService => GameSystem.GetService<TransitionService>();
    public static SpecialLevelService SpecialLevelService => GameSystem.GetService<SpecialLevelService>();

    public static InventoryService InventoryService => GameSystem.GetService<InventoryService>();
    public static DataService DataService => GameSystem.GetService<DataService>();
    public static UserDataService UserDataService => GameSystem.GetService<UserDataService>();
    public static TimeService TimeService => GameSystem.GetService<TimeService>();
}
