# WaterFlow Game Setup Todo

## Task 1: Confirm Unity compile is clean

**Description:** Re-run Unity compile after the latest fixes and resolve any remaining C# errors before touching scene or prefab wiring.

**Acceptance criteria:**
- [ ] Unity Console shows no C# compile errors.
- [ ] `Editor.log` has no new `error CS` entries after the latest recompile.
- [ ] No asmdef duplicate or missing namespace errors remain.

**Verification:**
- [ ] Unity recompiles after focusing the Editor.
- [ ] Latest `Editor.log` error scan returns no C# errors.

**Dependencies:** None

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\`
- `D:\Study\DA\Project\DATN_WaterFlow\Packages\manifest.json`
- `D:\Study\DA\Project\DATN_WaterFlow\ProjectSettings\ProjectSettings.asset`

**Estimated scope:** Medium

## Task 2: Rebuild the runtime service manager asset

**Description:** Clean `ServicesManager` configuration so it references only existing WaterFlow services needed for Classic gameplay.

**Acceptance criteria:**
- [ ] `ServicesManager.servicesObject` has no missing GUID references.
- [ ] Removed SDK services are not present: ads, tracking, remote config, Firebase, IAP/shop, lives, network gate.
- [ ] Required services resolve: audio, data, user data, inventory, config, gameplay config, transition, resource, booster, time, load-object/panel.

**Verification:**
- [ ] Inspect service manager asset in Unity with no missing entries.
- [ ] Enter Play Mode and verify `GameSystem.InitializeServices()` completes without null exceptions.

**Dependencies:** Task 1

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\[WATERFLOW] SERVICE MANAGER.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\*.cs`

**Estimated scope:** Medium

## Task 3: Verify required packages and scripting symbols

**Description:** Ensure all packages and define symbols required by the ported logic are installed and no removed platform/SDK package is required by runtime code.

**Acceptance criteria:**
- [ ] Addressables, Newtonsoft.Json, UniTask, DOTween, Odin, Nice Vibrations, Input System, and mob-sakai UI packages import cleanly.
- [ ] Android/Standalone/WebGL define symbols include required WaterFlow symbols.
- [ ] No runtime compile path requires I2 Localization, ads, Firebase, IAP, tracking, or iOS app code.

**Verification:**
- [ ] Unity package resolver finishes with no package import errors.
- [ ] Unity Console has no missing assembly or missing define-related errors.

**Dependencies:** Task 1

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Packages\manifest.json`
- `D:\Study\DA\Project\DATN_WaterFlow\ProjectSettings\ProjectSettings.asset`

**Estimated scope:** Small

## Checkpoint: Foundation

- [ ] Tasks 1-3 complete.
- [ ] Unity reaches Play Mode startup without service-resolution exceptions.
- [ ] No missing service references remain.

## Task 4: Build the single-scene runtime hierarchy

**Description:** Add the runtime root objects needed by the Classic game into `Game.unity`: game system, panel manager, main UI canvas, gameplay controller holder, event system, and world containers.

**Acceptance criteria:**
- [ ] `Game.unity` contains a configured `GameSystem`.
- [ ] `Game.unity` contains a configured `PanelManager`.
- [ ] `Game.unity` contains `GameController`, `LevelController`, `UIController`, `EventSystem`, and UI canvases needed by the existing code.

**Verification:**
- [ ] Opening `Game.unity` shows the expected hierarchy.
- [ ] Pressing Play does not throw missing component errors during Awake/Start.

**Dependencies:** Task 2

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Project Init Settings.asset`

**Estimated scope:** Medium

## Task 5: Configure camera, input, raycast, and world containers

**Description:** Wire the camera/controller layer required for dragging blocks and spawning the level representation.

**Acceptance criteria:**
- [ ] Main Camera has the required gameplay camera component and tags/layers.
- [ ] New Input System action asset is assigned where needed.
- [ ] Raycast/drag path can detect blocks in Play Mode.

**Verification:**
- [ ] In Play Mode, clicking or dragging on a block triggers game activation.
- [ ] No camera-controller missing errors appear.

**Dependencies:** Task 4

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Input\InputSystem_Actions.inputactions`

**Estimated scope:** Medium

## Task 6: Wire `GameController` and `LevelController` references

**Description:** Assign all required data assets and UI references so the gameplay lifecycle can load, spawn, and unload levels.

**Acceptance criteria:**
- [ ] `GameController` has `UIController`, `LevelDatabase`, and Main UI references.
- [ ] `LevelController` has `EnvironmentData`, block visuals data, `LevelDatabase`, and `BlockConfig`.
- [ ] Level 1 loads through `LevelLoaderSystem` in local/editor mode.

**Verification:**
- [ ] Play Mode logs a successful level load.
- [ ] No null reference appears from `GameController.OnAwake()` or `LevelController.Init()`.

**Dependencies:** Task 4, Task 5

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\LevelDatabase.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Block\BlockConfig.asset`

**Estimated scope:** Medium

## Checkpoint: Scene Boot

- [ ] Tasks 4-6 complete.
- [ ] `Game.unity` enters Play Mode and spawns the first level.
- [ ] No missing-reference exceptions during startup.

## Task 7: Create minimal `UIGame` page hierarchy

**Description:** Build the in-game UI page required by `UIGame`: safe area, item overlays, level panel container, normal level panel, message box, settings button, and replay button.

**Acceptance criteria:**
- [ ] `UIController.ShowPage<UIGame>()` displays a valid page.
- [ ] Timer and level panel initialize.
- [ ] Settings and replay buttons are clickable.

**Verification:**
- [ ] Play Mode shows the game UI.
- [ ] Replay button reloads or resets the game scene without exceptions.

**Dependencies:** Task 6

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\UI\UIGame.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\UI\*.prefab`

**Estimated scope:** Medium

## Task 8: Create required popup prefabs for Classic mode

**Description:** Provide the popup prefabs needed by the current gameplay code while keeping only Classic-mode flows.

**Acceptance criteria:**
- [ ] Required popups can be loaded by name through `PanelManager`.
- [ ] Popups do not reference ads, lives, IAP/shop, remote config, Gold Mode, Journey, or I2 Localization.
- [ ] Missing/deferred visual fields are handled with placeholders or null-safe logic.

**Verification:**
- [ ] Win opens `PopupPreWin`/`PopupWin` flow cleanly.
- [ ] Lose opens revive/settings path without SDK dependencies.
- [ ] Booster purchase popup opens without missing prefab errors.

**Dependencies:** Task 7

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\*.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Popup\*.cs`

**Estimated scope:** Medium

## Task 9: Configure panel/resource loading for UI prefabs

**Description:** Make `LoadObjectServiceAsync` and related resource loading paths find all UI pages and popups by the names used in code.

**Acceptance criteria:**
- [ ] `PanelManager.OpenForget<T>()` can load panel prefabs by class name.
- [ ] UI prefabs live in the expected Resources or Addressables path for the active load service.
- [ ] No panel load failure occurs for settings, win, revive, buy booster, or booster unlock popup.

**Verification:**
- [ ] Manually open each required popup in Play Mode or through simple debug buttons.
- [ ] Console shows no missing resource/addressable errors.

**Dependencies:** Task 8

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\*.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\SaveLoadObject\*.asset`

**Estimated scope:** Small

## Checkpoint: UI Flow

- [ ] Tasks 7-9 complete.
- [ ] Win, lose, replay, settings, and booster popup paths are visible and functional.
- [ ] No removed SDK feature appears in the runtime UI.

## Task 10: Validate the level database and 30 sample levels

**Description:** Confirm the ported level database references all 30 intended sample levels and that those levels deserialize after namespace renaming.

**Acceptance criteria:**
- [ ] Level 001 through Level 030 exist and are referenced by the active `LevelDatabase`.
- [ ] `[SerializeReference]` entries deserialize as `WaterFlow.Game` classes.
- [ ] No sample level references removed Gold Mode or retired-only data.

**Verification:**
- [ ] Open Level Database inspector without missing type warnings.
- [ ] Load at least Levels 001-005 in Play Mode or level editor.

**Dependencies:** Task 1

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\LevelDatabase.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\Editor\Levels\*.asset`

**Estimated scope:** Medium

## Task 11: Configure environment, block visuals, colors, gates, and obstacle configs

**Description:** Assign visual/data prefabs needed for blocks, gates, water colors, environment, particle database, and remaining DATN obstacles.

**Acceptance criteria:**
- [ ] Basic blocks and gates instantiate with valid meshes/materials.
- [ ] Water colors display distinctly.
- [ ] Required obstacle configs exist and are assigned where referenced.

**Verification:**
- [ ] Spawned level is visible and interactable.
- [ ] No missing material/prefab/config errors during spawn.

**Dependencies:** Task 10

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Block\BlockConfig.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Obstacle\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\*.asset`

**Estimated scope:** Medium

## Task 12: Verify only DATN-required obstacles remain authorable

**Description:** Keep level authoring aligned with section 2.2 by hiding/removing non-required obstacle options without breaking serialized level data.

**Acceptance criteria:**
- [ ] Level editor picker exposes only required obstacle/effect types.
- [ ] Retired enum values are not deleted or renumbered.
- [ ] Existing sample levels either avoid retired-only effects or are flagged for re-authoring.

**Verification:**
- [ ] Open Level Editor and inspect available block/effect options.
- [ ] Save and reload a test level without serialization warnings.

**Dependencies:** Task 10

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Level System\Editor\BlockEffectTypeDrawer.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\ObstacleRuleConfig.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\Obstacle Unlock Database.asset`

**Estimated scope:** Small

## Checkpoint: Level Playability

- [ ] Tasks 10-12 complete.
- [ ] At least 5 sample levels spawn and can be played.
- [ ] Classic win condition succeeds when all required blocks are cleared.

## Task 13: Configure booster service and four power-up configs

**Description:** Wire the Classic boosters the project uses: Pump, Hammer, Freeze Timer, and Expand.

**Acceptance criteria:**
- [ ] `BoosterService.boosterConfigs` includes only the intended Classic boosters.
- [ ] Each booster config has type, price, default count, unlock level, icon, and behavior reference.
- [ ] Removed pre-booster/Gold Mode/shop configs are not referenced.

**Verification:**
- [ ] Booster data initializes from `InventoryService`.
- [ ] Unlock conditions fire at the configured levels.

**Dependencies:** Task 2, Task 10

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\BoosterService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Power Ups\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Configs\BoosterConfig.cs`

**Estimated scope:** Medium

## Task 14: Wire booster UI and purchase/use flow

**Description:** Connect booster buttons, count labels, locked state, buy popup, and runtime power-up behavior to the in-game UI.

**Acceptance criteria:**
- [ ] Booster UI shows locked/unlocked/empty/usable states.
- [ ] Using a booster reduces its count by 1.
- [ ] Buying a booster spends coin and increases count.

**Verification:**
- [ ] Test each booster from the in-game UI.
- [ ] Console has no missing prefab/icon/audio errors from booster actions.

**Dependencies:** Task 13, Task 7, Task 8

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Power Ups\*.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Power Ups\UI\*.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\UI\PowerUpItemView.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\PopupBuyBooster.prefab`

**Estimated scope:** Medium

## Task 15: Configure win coin rewards and inventory persistence

**Description:** Make Classic level completion grant coin only, then persist coin and booster inventory locally.

**Acceptance criteria:**
- [ ] `GameplayConfigService.winRewards` contains coin rewards for normal/hard/very hard level types.
- [ ] `PopupWin` displays reward coin without requiring other reward board/liveops systems.
- [ ] Coin and booster balances persist after replay or scene reload.

**Verification:**
- [ ] Complete a level and verify coin increases.
- [ ] Spend coin on booster and verify persisted balance/count after reload.

**Dependencies:** Task 2, Task 8, Task 13

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\GameplayConfigService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Game\GameWinFlowService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Popup\PopupWin.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\DefaultInventoryService.asset`

**Estimated scope:** Medium

## Checkpoint: Economy Loop

- [ ] Tasks 13-15 complete.
- [ ] Win reward, coin balance, booster buy, and booster use form one complete loop.
- [ ] No reward/liveops/ad dependency remains in the loop.

## Task 16: Run full Play Mode smoke test

**Description:** Test the complete MVP flow in Unity from a fresh Editor state.

**Acceptance criteria:**
- [ ] Start `Game.unity` from Play Mode.
- [ ] Play level 1 to completion.
- [ ] Claim coin reward, continue/replay, use at least one booster, and trigger settings popup.

**Verification:**
- [ ] Unity Console remains free of errors during the smoke test.
- [ ] Save data updates as expected.

**Dependencies:** Task 15

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\`

**Estimated scope:** Small

## Task 17: Remove stale SDK/liveops references from configured assets

**Description:** After the game flow works, scan configured assets and scenes for references to removed systems and clear any that remain.

**Acceptance criteria:**
- [ ] No scene/service/prefab reference points to ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, I2 Localization, or iOS app logic.
- [ ] Removed systems do not appear in active UI or game flow.
- [ ] Remaining third-party vendor package artifacts that are harmless are documented rather than wired.

**Verification:**
- [ ] Text scan of active scene/assets for removed feature names.
- [ ] Unity Console has no missing script warnings from removed systems.

**Dependencies:** Task 16

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\`

**Estimated scope:** Medium

## Task 18: Commit and push the setup work

**Description:** Commit the completed setup work in clean logical commits and push to `origin/develop`.

**Acceptance criteria:**
- [ ] Setup changes are split into clear commits.
- [ ] Commit messages use project format like `[fix]`, `[feat]`, `[chore]`, or `[remove]`.
- [ ] `develop` is pushed successfully.

**Verification:**
- [ ] `git status -sb` is clean after commit.
- [ ] `git log --oneline -5` shows the new setup commits.
- [ ] `git status -sb` no longer reports local branch ahead of origin after push.

**Dependencies:** Task 17

**Files likely touched:**
- `D:\Study\DA\Project\DATN_WaterFlow\`

**Estimated scope:** Small

## Checkpoint: Complete

- [ ] Tasks 1-18 complete.
- [ ] Fresh Unity open compiles cleanly.
- [ ] `Assets\Scenes\Game.unity` is playable through Classic level start, drag, win, coin reward, booster buy/use, replay/continue.
- [ ] Setup work is committed and pushed to `develop`.
