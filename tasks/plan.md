# Implementation Plan: WaterFlow Game Setup

## Overview

Set up DATN_WaterFlow into a playable Classic-mode MVP using the gameplay, level-loading, booster, framework, and reward logic already ported from WorksSonatFlowJam. Scope is runtime setup and configuration only: no ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, localization plugin, or iOS-specific app logic. The first target is one working `Game.unity` scene that can load a local level, let the player drag blocks, complete the level, grant coin rewards, use coin-buyable boosters, and replay/continue.

## Current State

- `Assets/Scenes/Game.unity` is currently close to a default scene: Main Camera, Directional Light, and Global Volume only.
- `ProjectSettings/EditorBuildSettings.asset` includes only `Assets/Scenes/Game.unity`.
- Core init settings exist at `Assets/Project Files/Data/Project Init Settings.asset`.
- Level data exists at `Assets/Project Files/Level System/LevelDatabase.asset` with sample level assets under `Assets/Project Files/Level System/Editor/Levels/`.
- Gameplay scripts are present under `Assets/Project Files/Game/Scripts/`, including `GameController`, `LevelController`, `UIGame`, level representation, power-ups, and editor tooling.
- `Assets/WaterFlowFramework/Templates/ServicesSO/[WATERFLOW] SERVICE MANAGER.asset` still contains several missing GUID references and must be cleaned before Play Mode is reliable.
- Art/prefab polish remains intentionally deferred; use temporary/minimal prefabs where needed so logic can be tested.

## Architecture Decisions

- Follow FlowJam's three-scene flow, which the framework already encodes: `GamePlacement { Loading, Home, Game }` in `Assets/Scripts/Enums/GameMode.cs` is commented "Must same with Scene Build List", and `GamePlacementSceneNames.ToBuildSceneName()` maps to scene assets named `Loading`, `Home`, `Game`. Build list order must therefore be Loading(0) -> Home(1) -> Game(2). Scene switching goes through `TransitionService.SwitchScene(GamePlacement)`.
- Boot runs from the Loading scene: one GameObject carrying `GameLoading` + `Initializer` (+ `LoadingGraphics`) drives `ProjectInitSettings`, whose `SaveInitModule` calls `SaveController.Init()`. Nothing that reads save data (`ActiveSession`, `BoosterService`) may run before that. Persistent systems (`GameSystem`, `PanelManager`, `UIController`) live in the Loading scene and survive via `DontDestroyOnLoad`.
- Use existing framework entry points: `GameSystem` for services, `PanelManager` for popups, `UIController` for pages, `GameController` for gameplay boot, and `LevelController` for level lifecycle.
- Configure only local data sources. Level loading should use editor/local assets, not remote config or SDK-driven content.
- Keep level data enum values stable. Retired obstacles stay hidden from authoring but enum integer values must not be removed or reordered.
- Use placeholder art only where required to unblock logic verification; user-owned final art and prefab styling can replace placeholders later.

## Task List

### Phase 1: Compile and Configuration Foundation

- [ ] Task 1: Confirm Unity compile is clean
- [ ] Task 2: Rebuild the runtime service manager asset
- [ ] Task 3: Verify required packages and scripting symbols

### Checkpoint: Foundation

- [ ] Unity Console has no C# compile errors.
- [ ] Service Manager has no missing service object references.
- [ ] Entering Play Mode reaches the first frame without service-resolution exceptions.

### Phase 2: Game Scene Bootstrap

- [ ] Task 4: Build the single-scene runtime hierarchy
- [ ] Task 5: Configure camera, input, raycast, and world containers
- [ ] Task 6: Wire `GameController` and `LevelController` references

### Checkpoint: Scene Boot

- [ ] Pressing Play in `Assets/Scenes/Game.unity` initializes core services.
- [ ] Level 1 is loaded from local/editor level data.
- [ ] No missing-reference exceptions appear during scene start.

### Phase 3: Gameplay UI and Popup Flow

- [ ] Task 7: Create minimal `UIGame` page hierarchy
- [ ] Task 8: Create required popup prefabs for Classic mode
- [ ] Task 9: Configure panel/resource loading for UI prefabs

### Checkpoint: UI Flow

- [ ] Game UI shows level number, timer, settings, replay, and booster slots.
- [ ] Win flow opens the expected pre-win/win popup path.
- [ ] Lose/revive flow does not reference ads, lives, remote config, or Gold Mode.

### Phase 4: Levels and Obstacles

- [ ] Task 10: Validate the level database and 30 sample levels
- [ ] Task 11: Configure environment, block visuals, colors, gates, and obstacle configs
- [ ] Task 12: Verify only DATN-required obstacles remain authorable

### Checkpoint: Level Playability

- [ ] At least 5 sample levels can spawn without missing prefab/config errors.
- [ ] Blocks can be dragged, snapped, filled by gates, and cleared.
- [ ] Win condition resolves correctly for Classic mode.

### Phase 5: Boosters and Coin Economy

- [ ] Task 13: Configure booster service and four power-up configs
- [ ] Task 14: Wire booster UI and purchase/use flow
- [ ] Task 15: Configure win coin rewards and inventory persistence

### Checkpoint: Economy Loop

- [ ] Winning grants coin through `GameWinFlowService`.
- [ ] Booster counts persist through `InventoryService`/`DataService`.
- [ ] Coin purchase for boosters succeeds/fails cleanly based on balance.

### Phase 6: Final Validation and Cleanup

- [ ] Task 16: Run full Play Mode smoke test
- [ ] Task 17: Remove stale SDK/liveops references from configured assets
- [ ] Task 18: Commit and push the setup work

### Checkpoint: Complete

- [ ] Fresh project open compiles cleanly.
- [ ] `Game.unity` is playable from level start to win reward to next/replay.
- [ ] No runtime path depends on ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, I2 Localization, or iOS app logic.
- [ ] Setup changes are committed and pushed to `develop`.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Missing scene/prefab references after logic port | High | Build scene bootstrap in small checkpoints and verify Play Mode after each group. |
| Service Manager still contains deleted SDK service references | High | Recreate the service list from currently existing WaterFlow service assets only. |
| Level assets reference retired obstacle data | Medium | Keep enum values stable, hide retired types from editor, and validate sample levels before deleting any data asset. |
| UI prefabs from FlowJam reference removed systems | Medium | Create minimal Classic-only prefabs instead of copying Home/Shop/Journey-heavy UI wholesale. |
| Placeholder visuals hide real gameplay bugs | Medium | Separate logic smoke tests from final art pass; verify block/gate mechanics before visual polish. |
| Unity package import issues | Medium | Treat package/asmdef fixes as Task 1 blockers before runtime setup. |

## Parallelization Opportunities

- Scene bootstrap and service asset cleanup must be sequential because most runtime systems depend on them.
- Level-data validation can run in parallel with UI prefab construction after compile is clean.
- Booster config and win reward config can be prepared in parallel once `InventoryService` and `DataService` are confirmed working.

## Open Questions

- Final art/prefab style is intentionally not decided in this plan; use minimal placeholders until the user configures art.
- The three-scene flow (Loading -> Home -> Game) is confirmed by the project owner and matches the `GamePlacement` enum; the earlier single-scene plan was wrong and is retired. `Loading.unity` and `Home.unity` are being brought over from FlowJam so the real loading UI comes with them.
