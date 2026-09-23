# Todo Setup Game WaterFlow

> Cập nhật 2026-09-24: Đã gỡ bộ script Editor `WaterFlowSetup` theo yêu cầu dọn dẹp. Các mục bên dưới nhắc đến menu `WaterFlow/Setup/*` là ghi chép lịch sử, không còn là hướng dẫn có thể chạy. Scene, prefab và vật liệu đã tạo vẫn được giữ lại; kiểm tra trực tiếp bằng các asset hiện có trong Unity.

## Trạng thái hiện tại - 2026-08-31

- [x] Service Manager đã được dựng lại với các service local/Classic cần dùng, không còn GUID missing trong danh sách service.
- [x] Network time và Google Drive/Sheets level loader đã được bỏ khỏi runtime compile path.
- [x] ShopPanel/Home scene path/Live reward behavior đã được chặn khỏi luồng Classic một-scene.
- [x] Đã thêm `WaterFlow/Setup/Create Classic Bootstrap Scene` và `WaterFlow/Setup/Validate Classic Setup` trong Unity Editor.
- [x] Đã thêm `CameraControllerConfig.asset` mặc định.
- [ ] Chưa bật `GameController` trong scene vì còn thiếu UI/popup prefab và smoke test Play Mode.
- [x] **ĐÃ XỬ LÝ (2026-09-01):** Compile từng vỡ — 132 lỗi `CS0246`, toàn bộ đến từ 2 file lạ lọt vào khi import art:
  `Assets/Project Files/Game/Scripts/Level System/Effects/SwitchLayerBlockEffectBehavior.cs` và `TimeCapsuleEffectBehavior.cs`.
  Đây là bản gốc trước rename (còn `using SonatCore` / `namespace FlowOut`), không phải bản đã port — bản port đã bị xoá có chủ đích ở commit `343c7e1`.
  Không level nào trong 30 level mẫu dùng `SwitchLayerBlockEffectData`/`TimeCapsuleBlockEffectData`, và `TimeCapsuleUIView` cũng đã bị xoá → đã xoá 2 file này (kèm `.meta`); cần focus Unity để recompile xác nhận.
  Backup: `scratchpad/stray-effects-backup/`.
- [x] **Dọn LevelDatabase (2026-09-01):** xoá entry `type: 16` (TimeCapsule) và `type: 19` (SwitchLayer) khỏi list block effect trong `LevelDatabase.asset` — 2 entry này trỏ vào prefab đã mất script (`FlowOut.TimeCapsuleBlockEffectBehavior`, `FlowOut.SwitchLayerBlockEffectBehavior`). Phiên trước đã xoá type 12/13 (Ropes/Scissor) nhưng bỏ sót 16/19. Backup: `scratchpad/LevelDatabase.asset.bak`.
- [x] **Task 1 xác nhận (2026-09-01):** Unity vào được Play Mode → compile đã sạch (Unity không cho Play khi còn lỗi CS).
- [x] **Fix init-order TimeService (2026-09-01):** `ArgumentOutOfRangeException` trong `DefaultTimeService.GetUnixTimeSeconds`.
  Nguyên nhân: `ServicesManager.Resolve()` init service theo thứ tự list; `UserDataService` (vị trí 6) gọi `CheckTimeUser()`
  → đọc clock trong khi `DefaultTimeService` (vị trí 7) chưa `Initialize()`, nên `lastFeatTime` còn `DateTime.MinValue`.
  Cast `(DateTimeOffset)` ở locale UTC+7 đẩy về trước year 1 → throw. Guard `if (now == DateTime.MinValue)` không bắt được
  vì `GetCurrentTime()` đã cộng `realtimeSinceStartup` nên giá trị không còn đúng bằng MinValue.
  Bản gốc không lỗi vì nhánh `if (!getNetTimeSuccess)` luôn set `lastFeatTime` trước khi return — phiên trước bỏ nhánh net-time
  đã bỏ luôn phần khởi tạo ngầm đó. Fix: thêm `ResetTimeBase()` + lazy guard trong `GetCurrentTime()` → không còn phụ thuộc thứ tự init.
- [x] **Kiến trúc chốt lại 3 scene (2026-09-01):** Loading -> Home -> Game, theo enum `GamePlacement` + comment "Must same with Scene Build List".
  `plan.md`/`plan.vi.md` đã sửa; quyết định "single-scene MVP" của phiên trước là sai và đã bỏ.
  Build list hiện đã đúng: Loading(0) -> Home(1) -> Game(2).
- [x] **Bỏ hack `TransitionService.ResolveAvailablePlacement` (2026-09-01):** nó âm thầm ép `Home` -> `Game` khi Home
  không có trong build list. Thay bằng `Debug.LogError` + abort khi scene không load được, để lỗi cấu hình lộ ra thay vì bị che.
- [ ] **BLOCKER còn lại:** không scene nào có object boot. Check theo script GUID trên cả 3 scene đều = 0:
  `GameLoading` (b21ce68d3e7a480db732a3ca6ed44863), `Initializer` (7153a6f10bca46fa96e5fc9b6f3723a2),
  `LoadingGraphics` (b97a487dfd4147d99e48d9fa8c7b99aa). Hệ quả: `ProjectInitSettings` không chạy -> `SaveController.Init()`
  không được gọi -> `ActiveSession` nhận `levelSave = null` -> NullReferenceException trong `BoosterService.Initialize()`.
  Hướng xử lý đã chốt với chủ project: copy `Loading.unity` (+ `Home.unity`) từ FlowJam sang thay vì dựng placeholder.
- [ ] `Home.unity` hiện chỉ có Main Camera + Directional Light -> `SwitchScene(Home)` sẽ vào scene trắng.
- [x] **Copy Loading scene từ FlowJam (2026-09-01):** `Assets/Scenes/Loading.unity` nay là bản FlowJam (78KB, có `Game Loading`
  + `LoadingGraphics` + UI + `Initializer.prefab`). **Giữ nguyên `.meta` của DATN** nên GUID `c20059b1...` không đổi, build settings vẫn đúng.
  Mọi script guid khớp giữa 2 project (code copy kèm meta) nên component bind đúng class DATN dù `m_EditorClassIdentifier` còn ghi tên cũ `SonatSystem`.
  Đã copy đúng 26 file: `Initializer.prefab`, 4 sprite splash, `NotoSans SDF.asset` + material Yellow, folder Spine `Logo/` (6 file) — kèm meta.
  KHÔNG copy: I2 Localization, `[SONAT] SERVICE MANAGER.asset`, `TransitionCanvas.prefab` (kéo theo FPSDebug.cs + StylizedTitleContainer.prefab + 3 sprite + 2 material), `Home.unity` của FlowJam.
- [x] **Trỏ lại service manager:** guid `81f1cd99...` (SONAT) -> `8551bb2d...` (`[WATERFLOW] SERVICE MANAGER`), 1 chỗ, 0 chỗ còn sót.
- [x] **Sửa race init services (2026-09-01):** `GameSystem.WaitToInit()` trước đây chỉ chờ `delayToInit` rồi `Resolve()` —
  chạy song song với module init. Nay chờ `SaveController.IsSaveLoaded` (property public sẵn có), timeout 10s kèm `LogError`
  nói rõ thiếu object GameLoading/Initializer. Trước đó lỗi hiện ra dưới dạng NRE trong `ActiveSession`, rất khó suy ra nguyên nhân.
- [x] **Build settings sửa map GUID chéo:** trước đó [0] Loading trỏ guid của file Game và ngược lại. Nay khớp Loading(0)/Home(1)/Game(2).
- [x] **Tạo lại `Home.unity`** với đúng guid `8c47a7d3...` mà build settings trỏ.
- [ ] **Còn phải làm trong Unity:** chạy `WaterFlow/Setup/Fix Imported Loading Scene` — strip 2 component I2 mất script,
  xoá instance `TransitionCanvas` mất prefab (40 ref), dựng lại transition canvas của DATN. 8 guid còn thiếu còn lại là built-in Unity, bỏ qua được.
- [ ] `Game.unity` hiện là scene rỗng (bản 11KB có `[WaterFlow] Gameplay` chỉ còn trong Recycle Bin) -> cần chạy lại setup hoặc restore.
- [x] **Scene Home (2026-09-01):** Loading đã chạy được. Dựng Home theo yêu cầu chủ project: background, thanh currency, settings button, nút Play giữa.
  Không copy `Home.unity` của FlowJam (676KB, đầy UI Shop/Journey). Thêm mới:
  - `Assets/Project Files/Game/Scripts/UI/UIHome.cs` — `UIPage`, Play -> `Services.TransitionService.SwitchScene(GamePlacement.Game)`, Setting -> `PanelManager.OpenForget<PopupSetting>()`.
  - `Assets/Project Files/Game/Scripts/UI/HomeController.cs` — theo đúng pattern `GameController`: `uiController.Init()` + `InitPages()` rồi `ShowPage<UIHome>()`.
  - Menu `WaterFlow/Setup/Create Home Scene` dựng hierarchy + wire reference, dùng lại helper `EnsureCanvasStack`/`Stretch`/`GetOrCreateUiRoot`.
  Ràng buộc đã tuân theo: `UIController.Init()` chỉ quét `UIPage` ở **con trực tiếp** của canvas, và `UIPage` có `[RequireComponent(Canvas, GraphicRaycaster)]`
  nên page phải là nested canvas. Home KHÔNG tự tạo EventSystem — nó đến từ `Initializer.prefab` ở scene Loading qua `DontDestroyOnLoad`.
  - Thêm hiển thị level: `UIHome.levelText` set bằng `LevelLabel.Current()` trong `PlayShowAnimation()` (refresh mỗi lần mở Home,
    không set trong `Init()`, vì người chơi quay lại Home sau khi xong level). Dùng lại `LevelLabel` — class này tồn tại đúng để
    HUD/win/lose/home không lệch format; nó đọc `ActiveSession.Current.Save.DisplayLevelIndex + 1`, cùng nguồn với `LevelPanel` trong game.
  Art hiện là placeholder màu trơn (Image/TMP), thay art thật sau.
- [x] **Scene Game từ FlowJam (2026-09-01):** dùng transitive dependency closure thay vì copy tay, để "chỉ lấy thứ cần" có cơ sở đo được.
  Cách làm: build index guid->path của FlowJam (11.636 asset), BFS từ seed là các mảnh UI gameplay, dừng ở guid DATN đã có,
  và chặn không mở rộng vào hệ thống DATN đã bỏ (regex loại: I2/AutoLocalize/PreBooster/Tutorial/Gold Mode/CheatManager/_team_effect/BattlePass/Journey/Leaderboard/Shop/LiveOps).
  Script còn ở `scratchpad/closure.sh` + `copy_closure.sh` nếu cần chạy lại.
  Kết quả: **copy 55 file** (31 png, 10 prefab, 7 mat, 3 asset, 2 ogg, 2 file Spine) — **0 script**. Loại 3 file (I2 Localize, AutoTranslate, board_tut.png).
  Đáng chú ý trong 55 file: `Resources/UI/Level Panel.prefab`, `Resources/UI/PowerUpItemView.prefab`,
  `Resources/Panel/PowerUpPopup.prefab`, `Resources/Panel/PopupTooltip.prefab`, `FreezeEffect _ Screen.prefab`, Spine `Hard_LV`.
  Đây là lớp UI prefab mà DATN hoàn toàn chưa có (Task 7/8/9).
- [x] `Assets/Scenes/Game.unity` = bản FlowJam (195KB), **giữ meta DATN** nên guid `99c9720a...` không đổi, build settings vẫn đúng.
- [x] Trỏ lại `CameraControllerConfig` -> `a4a3a390...` (bản DATN). `BlocksVisualsNew` trỏ sang Simple nhưng thực ra là serialized data chết:
  `LevelController` của DATN chỉ còn field `BlocksVisualsSimple`, `LoadBLockVisualData()` return nó vô điều kiện -> Unity sẽ tự bỏ dòng đó.
- [x] 33 ref còn thiếu đã phân loại hết: **9** built-in/package (CanvasScaler, Button, Image, GraphicRaycaster, TMP, UIParticle...) không cần làm gì;
  **22** thuộc hệ đã bỏ (PreBooster + Clock_Rig/Booster_Wand2, 3 script Tutorial + PointerHolder + 2 sprite tut, Level Panel Gold Mode,
  CheatManager, I2 x2, star booster trail x2) -> đúng là không copy; **2** đã trỏ lại.
- [x] Menu dọn dẹp tổng quát hoá: `CleanImportedScene(scenePath, rebuildTransitionCanvas)` + thêm `WaterFlow/Setup/Fix Imported Game Scene`.
  Chỉ scene Loading cần dựng transition canvas vì `BaseTransition.Awake()` có `DontDestroyOnLoad` + singleton -> Home/Game dùng lại đúng instance đó.
- [ ] **Cần chạy trong Unity:** `WaterFlow/Setup/Fix Imported Game Scene`. Sau khi strip script, các object rỗng của hệ đã bỏ
  (PreClock, PreWand, Clock_Rig, SK_Gauntlet, Tutorials, First Level Tutorial, Tutorial Overlay, UI Dev Panel) có thể còn lại -> xoá tay trong Hierarchy.
- [x] **Bỏ art theo độ khó (2026-09-01)** theo yêu cầu chủ project. Xoá khỏi `LevelPanel.cs`: class `LevelDifficultyConfig`,
  field `difficultyConfigs`, `levelAnim` (SkeletonGraphic Spine), `levelAddTimeParticle`, `scalePunch`/`timePunch`, `levelType`,
  `currentConfig`, const `BlockUIKey`, và các method `SetSkin()` / `OnLevelAnimComplete()` / `UpdateBg()`.
  `PlayLevelAnimation()` -> đổi tên `ShowLevelIntro()` vì giờ mọi level đi cùng một đường (delay rồi announce obstacle mới).
  Giữ nguyên: obstacle unlock popup, level text, `TimerVisualiser`.
  Lưu ý: bỏ field trong code KHÔNG xoá object con trong prefab -> thêm menu `WaterFlow/Setup/Strip Level Difficulty Visuals`
  để xoá `BackgroundHard`, `BackgroundSuperHard`, `Level Difficulty Anim` khỏi `Assets/Resources/UI/Level Panel.prefab`.
- [ ] **Art chết sau khi strip** (đã verify chỉ được tham chiếu bởi các object vừa xoá) — xoá được sau khi chạy menu trên:
  Spine `Animations/LevelMode/Hard_LV.*` (6 file), `Resources/Audio/Warning_Hard.ogg`, `Warning_VeryHard.ogg`,
  `Sprites/InGame/bar_time_purple.png`, `bar_time_red.png`, `Fonts & Materials/Mikado-Black SDF Material Purple.mat`,
  `Jp_Tw/NotoSans SDF Material Red.mat`, `Level Hard_Red.prefab`, `Level Hard_Violet.prefab`,
  `Shape _ Glow ADD_Red.prefab`, `Shape _ Glow ADD_Violet.prefab`.
  **PHẢI GIỮ:** `Mikado-Black SDF Material Red.mat` (PowerUpItemView.prefab dùng), `Add Time.prefab` +
  `Blink_blue_sheet_01.png` (là hiệu ứng "+time" chung của `TimerVisualiser`, do `LevelController.AddTime` / `BombEffectBehavior` gọi, không thuộc độ khó).
- [x] **Bỏ nhạc theo độ khó (2026-09-01):** xoá `GameController.ResolveIngameMusic()`; `UpdateGameMusic()` giờ luôn dùng
  `private const AudioId INGAME_MUSIC = AudioId.BGM_Ingame_Funny`. Không xoá giá trị enum `AudioId` nào (file đó generate từ template).
  Sau thay đổi này `LevelType` không còn ảnh hưởng presentation ở đâu nữa — chỉ còn 2 chỗ dùng nó:
  `LevelLoaderSystem.GetLevelType()` (metadata load level) và `GameplayConfigService.GetWinReward(LevelType)` (**tiền thưởng theo độ khó — giữ, thuộc Task 15**).
- [x] **Đổi data từ prefab Simple sang Base (2026-09-01)** theo yêu cầu chủ project.
  Chỉ đúng **1 asset** tham chiếu prefab Simple: `Assets/Project Files/Data/Skins/Simple Blocks Visuals Data.asset`
  -> đổi 32/32 guid từ `Blocks/Simple/<X> Simple.prefab` sang `Blocks/Base/<X> Base.prefab`.
  Backup: `scratchpad/SimpleBlocksVisualsData.asset.bak`.
  Hệ quả hình ảnh: block giờ render model `Models/Blocks/Base Block/Model_Block_*_New.fbx` thay vì
  `Models/Blocks/Simple Block/Model_Block_*.fbx`, và `outerRenderer` không còn bị variant clear về 0 nữa.
  Vẫn CẦN chạy `WaterFlow/Setup/Repair Block Mesh Renderers`: prefab Base hiện đang giữ giá trị `meshRenderer` SAI
  do tool đoán trước đó (Square: `449785902275705987`) — nó resolve được nên không throw Unassigned, nhưng `Validate` sẽ
  fail vì `meshRenderer.parent.localPosition != colliderParent.localPosition`.
- [x] **ROOT CAUSE `meshRenderer` null trên 32 block prefab (2026-09-01).**
  Mỗi `Blocks/Base/*.prefab` có `m_RemovedGameObjects` **xoá 1 GameObject** khỏi FBX instance của block model —
  chính object mang model MeshRenderer. Bản tương ứng ở FlowJam (`Blocks/New/*.prefab`) xoá **0** object.
  Chuỗi bằng chứng (mất 3 vòng đoán sai trước khi tới được đây, ghi lại để khỏi lặp):
  1. Dump trong Unity: `Corner LB Base` chỉ có **4** MeshRenderer — `BLS_BG` (=outerRenderer), `BLS_Glass` (=meshGlass),
     `BLS_Water` (=waterRenderer), `Block_BubbleEffect`. Ba cái hợp lệ đã bị 3 field chiếm hết -> không còn ứng viên nào cho `meshRenderer`.
     `Block_BubbleEffect` có `parent localPosition = (0,0,0)` != `colliderParent (0.5,0,0.5)` -> đúng lý do `Validate` fail.
  2. FBX `Model_Block_L_Short_New.fbx`: **md5 identical** giữa 2 project. `.fbx.meta`: **identical**. -> không phải lỗi art/import.
  3. Duyệt từng `PrefabInstance`: DATN 32/32 có 1 removal trên instance FBX; FlowJam 32/32 có 0.
  Fix: clear `m_RemovedGameObjects` về `[]` trên cả 32 prefab (`scratchpad/restore_block_model.sh`,
  backup `scratchpad/block-prefab-backup2/`). Đây là **xoá** entry list nên Unity nhận bình thường —
  khác với lần thử thêm entry `stripped` bằng tay, Unity im lặng bỏ.
- [x] **`meshRenderer` XONG (2026-09-01):** `Repair Block Mesh Renderers` báo `32 repaired, 0 unresolved`.
  Verify trên disk: **32/32 fileID khớp chính xác** bản gốc FlowJam (`Blocks/New/*.prefab`). Lần này Unity tự ghi reference
  qua `SaveAsPrefabAsset` nên hợp lệ thật, không phải YAML viết tay bị Unity bỏ.
- [x] **Fix "Prefab for block type X is not assigned" x32 (2026-09-01).** Đây là lỗi do chính bước đổi Simple->Base của tôi.
  Reference tới prefab trong YAML là `{fileID: <root GameObject>, guid, type: 3}` — fileID trỏ root GameObject **bên trong**
  prefab đó và **khác nhau ở từng prefab**. Tôi chỉ đổi guid, giữ nguyên fileID của root prefab Simple -> mọi entry thành null.
  Kéo theo `MissingReferenceException` ở `LevelRepresentation.SpawnBlock` dòng 181 (Instantiate prefab null).
  Fix: resolve root GameObject thật của từng prefab Base (Transform không-stripped có `m_Father: {fileID: 0}` -> lấy `m_GameObject`),
  rewrite cả 32 fileID (`scratchpad/fix_visuals_fileids.sh`). Verify 32/32 trỏ đúng GameObject tồn tại.
  Backup: `scratchpad/SimpleBlocksVisualsData.beforeFileIdFix.asset`.
  **Bài học:** đổi guid trong reference asset thì phải đổi luôn fileID; chỉ swap guid là tạo reference chết mà YAML nhìn vẫn "có giá trị".
- [x] **Nguồn spam "referenced script is missing":** 4 prefab đã copy còn tham chiếu I2 Localization (không port):
  `Resources/UI/Level Panel.prefab`, `Resources/UI/PowerUpItemView.prefab`, `Resources/Panel/PopupTooltip.prefab`,
  `Resources/Panel/PowerUpPopup.prefab`. Thêm menu `WaterFlow/Setup/Strip Missing Scripts In Resources Prefabs`
  dùng `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`. Vô hại nhưng che mất lỗi thật nên cần dọn.
- [ ] `KeyLockDebugVisualizer` tự tạo singleton trong scene — chỉ là debug visualizer, không phải lỗi.
- [x] **Copy audio + popup prefab (2026-09-01):** log báo thiếu `Audio/BGM_Ingame_Funny`, `Audio/Block_Pick`,
  `Panel/PowerUpUnlockNotifyPopup`. Không vá lẻ mà xác định đúng tập cần:
  - **Audio**: đối chiếu 93 giá trị enum `AudioId` với `Resources/Audio` của FlowJam -> 92 khớp (cái lệch là `None`, giá trị 0).
    Audio là leaf asset nên copy thẳng. `Resources/Audio` giờ có 94 file.
  - **Popup**: grep code DATN tìm popup thật sự được mở (`OpenForget`/`OpenPanelAsync`/`EnqueuePanel`) -> 11 class, cả 11 đều có
    prefab ở FlowJam. Đo closure **từng popup** để thấy giá thật trước khi copy:
    | popup | file | script | shop/pack |
    | PowerUpUnlockNotifyPopup | 23 | 0 | 0 |
    | ObstacleUnlockNotifyPopup | 23 | 0 | 0 |
    | PopupToast | 2 | 0 | 0 |
    | PopupSetting | 25 | 1 | 0 |
    | PopupPreWin | 32 | 0 | 0 |
    | PopupWin | 30 | 1 | 0 |
    | PopupLose | 43 | 2 | 0 |
    | PopupReward | 14 | 1 | 1 |
    | PopupBuyBooster | 22 | 1 | 0 |
    | PopupCollectMultipleReward | 1 | 0 | 0 |
    | **PopupRevive** | **104** | **22** | **31** |
  Đã copy 10 popup (union closure 125 file) + 92 audio = **217 file** (214 mới, 3 ghi đè trùng nội dung).
  Script lẻ được kéo theo đều là helper UI vô hại: `SettingButton`, `SkeletonIconController`, `CanvasAutoLayer`,
  `GridLayoutGroup` (BeardyGridLayoutGroup), `UICoin`; cùng `Prefabs/Shop/Reward.prefab` mà `PopupReward` cần
  (không thuộc hệ Shop — đã verify bằng reference).
  Sau copy còn 13 ref thiếu: 8 built-in/package (vô hại), 2 I2 Localize (dùng menu strip), 3 sprite cố ý loại
  (`board_tut.png`, `pbg_bar_purple/red.png` — chỉ render trống).
- [ ] **CHỜ QUYẾT ĐỊNH: `PopupRevive`.** Một mình nó kéo 104 file / 22 script / 31 thứ thuộc `PackManager` + `Shop`
  (`UIShopPackBase`, `UICustomShopPack`, `PackCooldown`, `PageSlider`...) vì luồng revive của FlowJam bán IAP pack.
  DATN không có IAP -> **không copy**. Hai hướng: (a) tự dựng PopupRevive tối giản (chỉ nút revive bằng coin + nút bỏ qua),
  hoặc (b) port prefab rồi xoá hẳn phần pack. Chưa làm gì cho tới khi chủ project chọn.
- [x] **Bỏ `PopupPreWin` + `PopupReward` (2026-09-01)** theo yêu cầu chủ project.
  `PopupReward` vốn đã là dead code (không caller nào, chỉ còn trong comment). `PopupPreWin` chỉ là màn animation
  mừng thắng, `Close()` của nó mở `PopupWin` -> đã sửa `GameController` mở `PopupWin` trực tiếp.
  Xoá 2 script + 2 prefab + 34 file chỉ thuộc riêng chúng (Spine `Win1`, VFX confetti/firework, `Prefabs/Shop/Reward.prefab`,
  `GridLayoutGroup.cs`, material Orange). Tập "riêng" tính bằng cách trừ closure của mọi popup được giữ nên không đụng thứ dùng chung.
  Đã verify: không class nào kế thừa 2 popup, không code nào còn tham chiếu. Backup: `scratchpad/prewin-reward-backup/`.
- [x] **Popup không dùng Spine animation (2026-09-01):** chủ project cố ý xoá các Spine anim (`Win2`, `Lose`, `Logo`, `LevelMode`),
  chỉ cần popup thường. Nhưng `PopupWin.Open()` và `PopupLose.Open()` deref `CoinAnim.GetAnimationState()` **không guard**
  -> NRE khi mở popup. Đã guard cả hai bằng `var coinAnimation = CoinAnim ? CoinAnim.GetAnimationState() : null;`
  (phủ cả trường hợp thiếu component lẫn thiếu skeleton data), phần reward/button phía dưới chạy độc lập.
  Tôi có copy lại `Animations/Lose` + `Win2` một lần do hiểu sai là mất ngoài ý muốn — đã undo, `Animations/` giờ đúng
  2 folder booster mà chủ project giữ.
- [x] **Xoá `SettingButton.cs` + `UICoin.cs` (2026-09-01).** Đây là lỗi của tôi: closure có kéo `.cs` mà tôi không verify
  compile, nên 2 file bản-trước-rename (`SonatCore`/`SonatFramework`/`Sonat.Enums`) lọt vào — đúng loại lỗi như 2 file
  effect ở đầu session. Lý do xoá thay vì port:
  - `UICoin` : hành vi duy nhất của nó là click mở `PopupShop` — DATN **không có** Shop. Và không code nào dùng `UICoin`/`UICoin.Main`.
  - `SettingButton` : cần base `ToggleButton` mà DATN không có, **và** DATN đã có sẵn `SettingsElement`
    (`WaterFlow.Framework.Systems.SettingsManagement`) làm đúng việc đó qua `AudioService`/`VibrationService`.
    Port vào là tạo hệ song song, trái nguyên tắc "không dựng hệ thống song song".
  Backup: `scratchpad/SettingButton.cs`, `scratchpad/UICoin.cs`.
  Các `.cs` copy còn lại đều sạch (`SkeletonIconController.cs`, `CanvasAutoLayer.cs` — chỉ `using UnityEngine`).
  Đã verify: **0** file còn namespace `Sonat*` trong toàn project.
- [ ] **Hệ quả cần wiring prefab (không chặn Play):**
  - `PopupSetting.prefab` : chỗ từng dùng `SettingButton` thành missing script -> sau khi strip thì 3 toggle
    Sound/Music/Vibration mất hành vi. DATN có `SettingsElement` nhưng nó dùng `Toggle` chuẩn Unity, còn prefab FlowJam
    dùng button ảnh -> không drop-in được. Hai hướng: (a) thay khu settings bằng 3 `Toggle` + `SettingsElement`,
    hoặc (b) viết 1 component nhỏ của DATN toggle bằng Button gọi cùng service (giữ được look của prefab).
  - `PopupBuyBooster.prefab` : mất `UICoin` -> thay bằng `UICurrency` (bản của DATN, hoạt động sẵn).
- [x] **Xoá `SkeletonIconController.cs` (2026-09-01).** Nó dùng `icon.AnimationState` — API spine-unity <=4.2 đã bị bỏ.
  Phiên trước đã giải quyết chuyện này bằng `Assets/Scripts/UI/SpineGraphicExtensions.cs`: spine-unity 4.3 tách
  `SkeletonGraphic` thành renderer thuần + component animation riêng, nên `GetAnimationState()` trong project là
  **extension method của DATN**, không phải API Spine — và nó trả null an toàn khi thiếu component animation
  (đúng cái guard tôi thêm vào `PopupWin`/`PopupLose` dựa vào).
  Xoá thay vì sửa 1 chữ vì chủ project đã bỏ Spine anim cho 2 popup đó -> component không còn gì để chạy.
  Chỉ `PopupWin.prefab` + `PopupLose.prefab` dùng nó, không code nào tham chiếu class. Backup: `scratchpad/SkeletonIconController.cs`.
  Nếu sau này muốn dùng lại: đổi `icon.AnimationState` -> `icon.GetAnimationState()` và đổi `namespace FlowOut` -> `WaterFlow.Game`.
- [x] **Sweep compile-blocker (2026-09-01):** 0 file dùng `.AnimationState` sai, 0 file còn namespace `Sonat*`,
  0 file còn `namespace FlowOut`. Đây là 3 dấu hiệu nhận biết script bản-trước-rename lọt vào từ FlowJam —
  nên kiểm 3 cái này mỗi lần copy `.cs` từ project nguồn.
- [x] **Gắn tường cho board (2026-09-01):** `EnvironmentData.spawnBorders = 1` và `BorderRuleConfig.asset` có đủ 16 entry
  đã gán, nhưng **14/15 prefab tường thiếu** — chúng nằm ở `Assets/BorderArt/Prefabs/Ingame/Wall/` của FlowJam,
  folder đó chưa từng được import. Closure ra 31 file (14 prefab + 6 mat + 5 png + 5 fbx), đã copy 30.
  Loại `Scripts/Border/Data/BorderRuleConfig.cs` khỏi copy: DATN đã có class đó **cùng guid** — nó lọt vào vì
  `m_Script` guid nằm trong asset nên bị tính là seed (seed không qua bộ lọc DATN_KNOWN). Copy vào là duplicate class.
  Verify: 0 border ref còn thiếu.
- [ ] **Đổi model block sang Simple:** thêm menu `WaterFlow/Setup/Switch Block Models To Simple`
  (`Assets/Editor/WaterFlowSetup/BlockModelThemeSwitcher.cs`). Cách làm: giữ nguyên prefab `Blocks/Base/*`
  (đã verify wiring đúng: colliderParent, meshRenderer, water module) và chỉ đổi `MeshFilter.sharedMesh`
  từ `Models/Blocks/Base Block/Model_Block_X_New.fbx` sang `Models/Blocks/Simple Block/Model_Block_X.fbx`
  (13 fbx, tên map 1-1). Đúng thứ mà prefab variant Simple từng override — material/transform/reference giữ nguyên.
  KHÔNG trỏ data về prefab variant Simple: variant có `m_RemovedGameObjects` riêng xoá 1 object trong FBX instance,
  chưa xác định được nó có phải object mang `meshRenderer` hay không, nên tránh rủi ro lặp lại lỗi cũ.
- [x] **Check "không thấy màu nước" (2026-09-01): KHÔNG phải bug — block bắt đầu rỗng theo thiết kế.**
  Đã verify toàn bộ chuỗi và không có mắt nào đứt:
  - `Simple Blocks Visuals Data.colors` : 19 entry, mỗi entry đủ `material`/`pipeMaterial`/`glassMaterial`/
    `waterMaterial`/`waterInPipe`/`flowMaterial` + `color`.
  - 114 material màu tham chiếu -> **0 thiếu** trong DATN.
  - `waterVisualModule` gán ở **32/32** prefab Base; `WaterMinMaxConfig` có đủ 3 asset và map đúng theo kích thước block
    (16 prefab -> block2x2, 13 -> block3x3, 3 -> block1x1).
  - `LevelRepresentation.SpawnBlock:192` **có** gọi `blockBehavior.SetColor(colorData)`
    -> `ChangeBodyMaterial(colorData.Material)` + `SetInnerColor` -> `waterModule.Init(colorData, min, max)`.
  Lý do nước không hiện lúc mới vào level:
  - `WaterVisualModule.SetupFillBounds()` set `_FillAmount = 0f`
  - `LevelBlockBehavior.Init()` kết thúc bằng `MeshWater.enabled = false`
  - `BlockFillController.InitMaxPoints()` chỉ set **dung lượng** (`primaryColorMaxPoint = figure.ActivePoints`), không set nước
  - Level data (`Level 001.asset`) chỉ có `blockType` + `blockColor`, **không có** field nước/fill ban đầu
  Tức block là bình rỗng, nước chảy vào từ gate khi chơi. Viền màu chỉ báo block thuộc màu nào.
  Cách kiểm: kéo block vào gate cùng màu -> nước phải chảy vào và hiện màu. Nếu lúc đó vẫn không hiện thì mới là bug,
  và sẽ nằm ở đường gate/flow (`GateBehavior`, `BlockFillController.Fill*`), không phải ở data màu.
- [x] **Check "nước trong ống không có màu" (2026-09-01): đã loại trừ toàn bộ chuỗi asset, KHÔNG thiếu gì.**
  Chuỗi nước-trong-ống: `GateBehavior` tạo `WaterBlock` từ `waterPrefab` -> `WaterBlock.Init(colorData)` ->
  `graphicsMeshRenderer.material = LevelController.GetBlockColorData(color).WaterInPipeMaterial`.
  Đã verify từng mắt:
  - `Water.prefab` : `graphicsMeshRenderer` gán `8555090578905781836`, **resolve được** (stripped từ `Models/Water.fbx`, có trong DATN), `m_RemovedGameObjects: []`
  - `M_WaterInPipe_Red.mat` : `_MainColor = (0.96, 0.067, 0.10, 1)` đỏ tươi alpha 1; là material variant, **material cha `BaseWaterInPipe.mat` có**
  - `_FillAmount` không bị override -> dùng default shader = **1.0** (đầy)
  - `Level 001` có 2 gate với data thật: `color: 1` (đỏ) + `color: 7` (xanh), mỗi cái `colorCount: 4` -> ống ĐÁNG RA có màu
  - material + shader **identical với FlowJam**, cả 2 project đều URP 17.3.0
  Giả thuyết đã thử và **bị loại**: shader `WaterFlow_DualFill_Dissolve.shader` dùng `CGPROGRAM` + `UnityCG.cginc`
  (include của Built-in RP) nên tưởng không render dưới URP — nhưng pass của nó **không có `LightMode` tag**,
  dưới URP sẽ chạy như `SRPDefaultUnlit`, tức vẫn render. Không phải nguyên nhân.
  Khác biệt so với nước trong block (đang hiện được): block dùng Shader Graph `LiquidEffect_Normal 1.shadergraph`
  (URP-native), ống dùng shader viết tay.
- [ ] **Cần chạy để kết luận:** `WaterFlow/Debug/Dump Pipe Water` **trong lúc Play**
  (`Assets/Editor/WaterFlowSetup/PipeWaterDump.cs`). Nó in cho từng `WaterBlock`: color/count, activeInHierarchy,
  localScale, worldPos, renderer.enabled, bounds, material + shader + `shader.isSupported`,
  `_MainColor`, `_FillAmount`, `_FillAmountBottom`. Đủ để phân biệt: shader không compile được / renderer tắt /
  segment scale-0 hay nằm ngoài ống / material về null.
- [x] **"Nước trong ống không có màu" — đã xác định KHÔNG phải lỗi màu/shader (2026-09-01).**
  Dump runtime (Level 5, 8 segment) cho thấy 4 segment màu đều **đang render đúng**:
  material `M_WaterInPipe_GreenLight/Orange/Yellow/Green`, `shader=Custom/WaterFlow_DualFill_Dissolve`,
  `shaderValid=True`, `renderer.enabled=True`, `_MainColor` đúng màu, `_FillAmount=1.000`, `_FillAmountBottom=0.000`.
  4 segment `None` dùng `Toony Colors Pro 2/Hybrid Shader 2` -> chính là màu trắng thấy trong ống.
  Giả thuyết đã LOẠI: shader dùng include Built-in RP (`CGPROGRAM`/`UnityCG.cginc`) nhưng pass **không có `LightMode` tag**
  nên dưới URP chạy như `SRPDefaultUnlit` -> vẫn render, và `shaderValid=True` xác nhận. `Water.fbx` + `.meta`
  **identical với FlowJam**. Material + shader cũng identical. Nên không thiếu/không sai asset nào.
  Vấn đề là **vị trí**: segment màu ở offset 0 (miệng gate), segment `None` ở offset 12; mỗi segment dài 12 world unit
  (`localScale=(6,1,1)`, `bounds.z=12.03` -> mesh 2 unit/count, khớp `waterBlockSize = levelConfig.WaterPipeSize = 2`).
  Luồng đúng theo thiết kế: gate tạo `initialEmptyBlock` (count 3) trước, rồi `RemoveWaterInPipe(3, None)` lúc start
  tiêu thụ nó và queue dịch lên, đưa segment màu về miệng gate.
- [ ] **Cần chạy lại `WaterFlow/Debug/Dump Pipe Water` (đã thêm `boundsCenter` + range z/x + vị trí gate).**
  Câu hỏi duy nhất còn lại: mesh pivot ở giữa hay ở đầu. Nếu `boundsCenter.z` ≈ vị trí gate thì segment màu straddle
  mép board -> nửa nằm dưới board, nửa trong ống; nếu `bounds.min.z` bắt đầu từ gate và đi ra ngoài thì nó phải hiện
  và lúc đó là vấn đề che khuất (board inner ground / border vừa thêm vẽ đè lên ống).
- [x] **ROOT CAUSE "nước trong ống không có màu" (2026-09-01): material kính bị convert sang URP Lit + premultiplied alpha.**
  Dấu vết quyết định là quan sát của chủ project: **nhìn từ dưới thì thấy đúng màu** -> nước render đúng, bị che từ trên.
  Vật che là object `Glass` trong `Gate.prefab` (material `M_Glass.mat`).
  So với FlowJam, **22 material kính** của DATN bị đổi:
  | | DATN | FlowJam |
  | shader | URP **Lit** (`933532a4fc`) | URP **Unlit** (`650dd95267`) |
  | `_SrcBlend` | `1` (One) + `_ALPHAPREMULTIPLY_ON` | `5` (SrcAlpha) |
  | `m_Floats` | full set (`_Surface:1`, `_ZWrite:0`, queue 3000) | `[]` (default) |
  | `m_CustomRenderQueue` | 3000 | -1 |
  `Blend One OneMinusSrcAlpha` với RGB trắng **chưa** premultiply -> cộng thẳng trắng lên nền thay vì nhuộm nền
  -> kính thành trắng đục, che nước trong ống VÀ làm block trông nhợt nhạt.
  Fix: copy 22 material từ FlowJam (`scratchpad/fix_glass_materials.sh`), **giữ nguyên `.meta`** nên guid không đổi,
  mọi reference vẫn resolve. Verify: 0 material kính còn dùng URP Lit. Backup: `scratchpad/glass-materials-backup/`, `scratchpad/M_Glass.mat.bak`.
  Giả thuyết đã LOẠI trên đường đi: shader `WaterFlow_DualFill_Dissolve` không tương thích URP (sai - `shaderValid=True`,
  pass không có `LightMode` nên chạy như `SRPDefaultUnlit`); segment nằm dưới board (sai - `bounds z:-12..0` và `7..19`,
  đúng đoạn ống ngoài board); URP pipeline asset khác nhau (sai - cùng guid, diff chỉ MSAA/shadow/reflection/volume profile).
- [ ] **Đã ghi nhận, chưa sửa:** 15 material `Liquid/*` (nước trong block) trỏ shader graph khác FlowJam —
  DATN dùng `LiquidEffect_Normal 1.shadergraph` (bản **nhân đôi**, hậu tố " 1"), FlowJam dùng bản gốc.
  Nước trong block đang hiện đúng nên **không đổi** để tránh phá thứ đang chạy; chỉ là nợ kỹ thuật cần dọn sau.
  Tool so sánh: `scratchpad/compare_materials.sh` (quét 230 material, tìm lệch shader/blend/keyword).
- [ ] **"Nước trong ống không màu" — CHƯA xong. Fix material kính KHÔNG phải nguyên nhân (2026-09-01).**
  Chủ project đã tắt/bật lại Unity (chắc chắn reimport) và vẫn như cũ. 22 material kính vẫn nên giữ bản đã sửa
  (chúng lệch thật so với FlowJam) nhưng không liên quan bug này. Ghi lại để không lặp: tôi đã sửa dựa trên suy luận
  so-sánh-FlowJam mà **chưa có bằng chứng runtime**, khác với các lần trước có dump.
  Đã loại trừ thêm trong lượt này:
  - `Pipe_Head` dùng `None.mat` (`Block.shader`, `_Color` xám 0.906 **opaque**) — **identical FlowJam**
  - `_FillDirection` trên `M_Pipe_.mat`: shader **không dùng** property này (dữ liệu sót từ shader khác)
  - Fill thuần theo `i.uv.y` (`o.uv = v.uv`), và `Water.fbx` + `.meta` identical FlowJam
  - **Không** có shader trùng tên trong Assets (chỉ 1 `WaterFlow_DualFill_Dissolve.shader`)
  - `M_WaterInPipe_*` **không** nằm trong 36 material lệch -> identical FlowJam, kể cả việc `None` dùng Toony
    và các màu dùng custom shader. Nghĩa là ở FlowJam custom shader đó **có** render.
  Map renderer trong `Gate.prefab`: `Arrow`->Arrow.mat, `Glass`->M_Glass.mat, `Water_fakeLight`->WaterFakeLight.mat,
  `Pipe_Head`->None.mat. (`Pipe` không có MeshRenderer -> chỉ là transform.)
- [ ] **A/B test đang chạy:** đã tạm cho `M_WaterInPipe_Orange.mat` dùng **nguyên material của `M_WaterInPipe_None`**
  (shader Toony, cái chắc chắn render được), giữ `.meta` nên guid không đổi. Backup: `scratchpad/M_WaterInPipe_Orange.mat.bak`.
  Segment cam ở pipe **x=1, z=-12..0** (ống dưới-trái).
  - Nếu segment đó HIỆN -> custom shader `WaterFlow_DualFill_Dissolve` là nguyên nhân (không draw dù `isSupported=True`)
  - Nếu VẪN KHÔNG hiện -> là che khuất tại vị trí đó, và phải soi renderer nào phủ lên (dump đã thêm phần
    "Gates: renderers by height" để trả lời)
- [x] **"Nước trong ống không có màu" — KHÔNG phải bug, là gameplay (2026-09-02).** Level 5 có **3 gate băng**.
  Dump runtime chỉ ra material của từng gate:
  | gate | Glass | Pipe_Head | nước |
  | (1,0) | `Ice_Pipe` | `Ice_Pipe_Head` | Orange |
  | (1,7) | `Ice_Pipe` | `Ice_Pipe_Head` | DarkGreen |
  | (4,7) | `Ice_Pipe` | `Ice_Pipe_Head` | LightGreen |
  | (4,0) | `M_Glass` | `M_Pipe_None` | Yellow |
  Level data khớp chính xác: gate (1,0)/(1,7)/(4,7) có `gateEffects` trỏ `IceGateEffectData`, riêng (4,0) là `gateEffects: []`.
  Và trong ảnh gameplay đúng là **chỉ gate vàng hiện màu** — chính là (4,0), cái duy nhất không băng.
  Băng phủ ống nên không thấy màu nước cho tới khi phá băng. Muốn kiểm màu nước ống thì mở level không có ice gate,
  hoặc phá băng trong lúc chơi.
- [x] **Đã dọn scaffolding (2026-09-02):** xoá `PipeWaterDump.cs`, `BlockRendererDump.cs`,
  `BlockMeshRendererRepair.cs`, `BlockModelThemeSwitcher.cs` (2 cái sau đã chạy xong việc: gán 32/32 meshRenderer
  và đổi model sang Simple). Còn lại `WaterFlowClassicSetup.cs` với các menu setup dùng về sau.
  Đã revert `M_WaterInPipe_Orange.mat` về nguyên bản (custom shader, `_MainColor` cam) — A/B test không cần nữa.
- [ ] **Nợ lại từ vụ này:** 22 material kính tôi đã đổi từ URP Lit -> URP Unlit theo FlowJam. Chúng **lệch thật**
  so với FlowJam nên giữ bản sửa, nhưng **không phải** nguyên nhân bug và tôi đã sửa khi chưa có bằng chứng runtime.
  Nếu thấy kính/khối hiển thị khác ý, backup ở `scratchpad/glass-materials-backup/`.
- [ ] **Lưu ý vận hành:** hàng loạt `Import Error Code:(4) Build asset version error ... modification time` là do
  script ghi file asset **trong lúc Unity đang import** (SourceAssetDB ghi mtime X, file trên disk mtime Y).
  Vô hại, không mất dữ liệu, clear bằng Ctrl+R hoặc restart Unity. Lần sau sửa asset bằng script thì nên
  để Unity ở trạng thái rảnh (hoặc đóng Unity) trước khi chạy.

**Điểm cần bạn import/config tiếp theo:**

- `EnvironmentData.asset`: đã gán prefab cho gate, inner obstacle/blocker, inner tile 1/2, border/ground, generator và extra layer/lift handler.
- `BlocksVisualsData`: đã thêm `Simple Blocks Visuals Data` và gán vào `Game.unity`; DATN hiện chỉ dùng theme Simple.
- `PowerUp Config`: Pump/Hammer/Freeze/Expand đã có icon/behavior/effect/audio cơ bản; cần kiểm trong Play Mode từng item.
- Obstacle scope bám theo mục 2.2.3.4/2.2.4.5 trong `DATN_NguyenHoangHiep.docx`; Rope/Scissors đã loại khỏi DATN scope.
- UI/popup prefab: cần `UIGame`, `LevelPanel`, `PopupSetting`, `PopupWin`, `PopupPreWin`, `PopupRevive`, `PopupBuyBooster`, booster item view theo layout/art bạn muốn.

## Task 1: Xác nhận Unity compile sạch

**Mô tả:** Recompile Unity sau các fix gần nhất và xử lý toàn bộ lỗi C# còn lại trước khi wiring scene/prefab.

**Tiêu chí nghiệm thu:**
- [x] Unity Console không còn lỗi C#.
- [x] `Editor.log` không có `error CS` mới sau lần recompile mới nhất.
- [x] Không còn lỗi duplicate asmdef hoặc missing namespace.

**Cách kiểm tra:**
- [x] Focus Unity Editor để trigger recompile.
- [x] Scan `Editor.log` mới nhất và xác nhận không còn lỗi C#.

**Phụ thuộc:** Không có

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\`
- `D:\Study\DA\Project\DATN_WaterFlow\Packages\manifest.json`
- `D:\Study\DA\Project\DATN_WaterFlow\ProjectSettings\ProjectSettings.asset`

**Độ lớn:** Vừa

## Task 2: Dựng lại runtime Service Manager asset

**Mô tả:** Dọn cấu hình `ServicesManager` để chỉ còn các service WaterFlow đang tồn tại và cần cho Classic gameplay.

**Tiêu chí nghiệm thu:**
- [x] `ServicesManager.servicesObject` không còn GUID bị mất.
- [x] Không còn service SDK: ads, tracking, remote config, Firebase, IAP/shop, lives, network gate.
- [x] Các service cần thiết resolve được: audio, data, user data, inventory, config, gameplay config, transition, resource, booster, time, load-object/panel.

**Cách kiểm tra:**
- [x] Mở Service Manager asset trong Unity và không thấy entry Missing.
- [ ] Vào Play Mode, `GameSystem.InitializeServices()` chạy xong không null exception.

**Phụ thuộc:** Task 1

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\[WATERFLOW] SERVICE MANAGER.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\*.cs`

**Độ lớn:** Vừa

## Task 3: Kiểm tra package và scripting symbols

**Mô tả:** Đảm bảo package/define cần cho logic đã port được import đúng, và runtime không cần package SDK đã bỏ.

**Tiêu chí nghiệm thu:**
- [ ] Addressables, Newtonsoft.Json, UniTask, DOTween, Odin, Nice Vibrations, Input System và mob-sakai UI packages import sạch.
- [x] Define symbols cho Android/Standalone/WebGL có đủ symbol WaterFlow cần.
- [x] Không có runtime compile path nào cần I2 Localization, ads, Firebase, IAP, tracking hoặc iOS app code.

**Cách kiểm tra:**
- [ ] Unity package resolver chạy xong không lỗi import package.
- [ ] Unity Console không còn lỗi missing assembly hoặc lỗi do thiếu define.

**Phụ thuộc:** Task 1

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Packages\manifest.json`
- `D:\Study\DA\Project\DATN_WaterFlow\ProjectSettings\ProjectSettings.asset`

**Độ lớn:** Nhỏ

## Checkpoint: Nền Tảng

- [ ] Task 1-3 hoàn tất.
- [ ] Unity vào được Play Mode startup mà không lỗi service-resolution.
- [ ] Không còn missing service reference.

## Task 4: Dựng runtime hierarchy cho một scene

**Mô tả:** Thêm các root object cần cho Classic game vào `Game.unity`: game system, panel manager, main UI canvas, gameplay controller holder, event system và world containers.

**Tiêu chí nghiệm thu:**
- [ ] `Game.unity` có `GameSystem` được cấu hình.
- [ ] `Game.unity` có `PanelManager` được cấu hình.
- [ ] `Game.unity` có `GameController`, `LevelController`, `UIController`, `EventSystem` và UI canvases cần thiết.

**Cách kiểm tra:**
- [ ] Mở `Game.unity` thấy hierarchy đúng.
- [ ] Bấm Play không lỗi missing component trong Awake/Start.
- [ ] Chạy menu `WaterFlow/Setup/Create Classic Bootstrap Scene` trong Unity sau khi compile sạch.

**Phụ thuộc:** Task 2

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Project Init Settings.asset`

**Độ lớn:** Vừa

## Task 5: Cấu hình camera, input, raycast và world containers

**Mô tả:** Wire lớp camera/controller cần cho kéo block và spawn level representation.

**Tiêu chí nghiệm thu:**
- [ ] Main Camera có gameplay camera component và tag/layer đúng.
- [ ] New Input System action asset được gán vào nơi cần.
- [ ] Đường raycast/drag detect được block trong Play Mode.

**Cách kiểm tra:**
- [ ] Trong Play Mode, click/drag block trigger game activation.
- [ ] Không còn lỗi thiếu camera-controller.

**Phụ thuộc:** Task 4

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Input\InputSystem_Actions.inputactions`

**Độ lớn:** Vừa

## Task 6: Wire `GameController` và `LevelController`

**Mô tả:** Gán đầy đủ data asset và UI reference để lifecycle gameplay load, spawn và unload level được.

**Tiêu chí nghiệm thu:**
- [ ] `GameController` có reference `UIController`, `LevelDatabase` và Main UI.
- [ ] `LevelController` có `EnvironmentData`, block visuals data, `LevelDatabase` và `BlockConfig`.
- [ ] Level 1 load qua `LevelLoaderSystem` ở local/editor mode.

**Cách kiểm tra:**
- [ ] Play Mode log load level thành công.
- [ ] Không null reference từ `GameController.OnAwake()` hoặc `LevelController.Init()`.

**Phụ thuộc:** Task 4, Task 5

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\LevelDatabase.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Block\BlockConfig.asset`

**Độ lớn:** Vừa

## Checkpoint: Scene Boot

- [ ] Task 4-6 hoàn tất.
- [ ] `Game.unity` vào Play Mode và spawn được level đầu tiên.
- [ ] Không có missing-reference exception khi startup.

## Task 7: Tạo hierarchy tối thiểu cho `UIGame`

**Mô tả:** Dựng in-game UI page cần bởi `UIGame`: safe area, item overlays, level panel container, normal level panel, message box, settings button và replay button.

**Tiêu chí nghiệm thu:**
- [ ] `UIController.ShowPage<UIGame>()` hiển thị page hợp lệ.
- [ ] Timer và level panel init được.
- [ ] Settings và replay button click được.

**Cách kiểm tra:**
- [ ] Play Mode hiện game UI.
- [ ] Replay button reload/reset game scene không exception.

**Phụ thuộc:** Task 6

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\UI\UIGame.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\UI\*.prefab`

**Độ lớn:** Vừa

## Task 8: Tạo popup prefab cần cho Classic mode

**Mô tả:** Cung cấp các popup prefab gameplay đang gọi, chỉ giữ flow Classic.

**Tiêu chí nghiệm thu:**
- [ ] Các popup cần thiết load được bằng tên qua `PanelManager`.
- [ ] Popup không reference ads, lives, IAP/shop, remote config, Gold Mode, Journey hoặc I2 Localization.
- [ ] Field visual chưa có art được xử lý bằng placeholder hoặc null-safe logic.

**Cách kiểm tra:**
- [ ] Win mở `PopupPreWin`/`PopupWin` sạch.
- [ ] Lose mở revive/settings path không phụ thuộc SDK.
- [ ] Booster purchase popup mở không lỗi missing prefab.

**Phụ thuộc:** Task 7

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\*.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Popup\*.cs`

**Độ lớn:** Vừa

## Task 9: Cấu hình panel/resource loading cho UI prefab

**Mô tả:** Đảm bảo `LoadObjectServiceAsync` và resource loading path tìm được UI page/popup bằng đúng tên class code đang gọi.

**Tiêu chí nghiệm thu:**
- [ ] `PanelManager.OpenForget<T>()` load được panel prefab theo class name.
- [ ] UI prefab nằm đúng Resources hoặc Addressables path theo load service đang dùng.
- [ ] Không lỗi load panel cho settings, win, revive, buy booster hoặc booster unlock popup.

**Cách kiểm tra:**
- [ ] Mở thử từng popup cần thiết trong Play Mode hoặc qua debug button.
- [ ] Console không có lỗi missing resource/addressable.

**Phụ thuộc:** Task 8

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\*.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\SaveLoadObject\*.asset`

**Độ lớn:** Nhỏ

## Checkpoint: UI Flow

- [ ] Task 7-9 hoàn tất.
- [ ] Win, lose, replay, settings và booster popup path hiển thị/chạy được.
- [ ] Runtime UI không hiện feature SDK đã bỏ.

## Task 10: Validate LevelDatabase và 30 level mẫu

**Mô tả:** Kiểm tra active LevelDatabase có reference đủ 30 level mẫu và các level deserialize đúng sau rename namespace.

**Tiêu chí nghiệm thu:**
- [ ] Level 001 đến Level 030 tồn tại và được active `LevelDatabase` reference.
  **Phát hiện 2026-09-01:** `LevelDatabase.asset` được copy nguyên từ project nguồn nên còn rác lớn:
  - `levels`: 650 entry, chỉ 30 resolve (Level 001–030, nằm liền khối ở vị trí 1–30 đúng thứ tự), 620 entry đuôi là missing.
  - `variantEntries`: 423 entry, chỉ 30 có baseLevel hợp lệ; **0/847 variant asset** tồn tại.
  - Tổng 1474 guid ref dangling trong một file → đây là chỗ "thiếu nhiều" thấy trong Inspector.
  - Hệ quả thật (không chỉ cosmetic): `LevelCount => levels.Length` trả **650**, `GetLevel(index)` trả null với index >= 30;
    `GetRandomLevel` random trên toàn 650 → dễ ra null.
  - Fix bằng tool của project, KHÔNG sửa YAML tay: `Tools > Level Editor` → nút **"Populate Levels"**
    (`LevelDatabase.Editor_PopulateLevels`) → resize `levels` về 30, `Editor_PruneVariantEntries()` bỏ 393 entry chết,
    `Editor_GenerateObstacleUnlockData()` regenerate `Obstacle Unlock Database.asset`.
- [ ] `[SerializeReference]` deserialize về class `WaterFlow.Game`.
- [ ] Sample level không reference Gold Mode hoặc data chỉ thuộc obstacle đã bỏ.

**Cách kiểm tra:**
- [ ] Mở Level Database inspector không warning missing type.
- [ ] Load thử ít nhất Level 001-005 trong Play Mode hoặc Level Editor.

**Phụ thuộc:** Task 1

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\LevelDatabase.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\Editor\Levels\*.asset`

**Độ lớn:** Vừa

## Task 11: Cấu hình environment, block visuals, colors, gates và obstacle configs

**Mô tả:** Gán prefab/data visual cần cho blocks, gates, water colors, environment, particle database và obstacle DATN còn dùng.

**Tiêu chí nghiệm thu:**
- [ ] Basic block và gate instantiate với mesh/material hợp lệ.
- [ ] Water colors hiển thị phân biệt.
- [ ] Required obstacle configs tồn tại và được assign đúng nơi code cần.

**Cách kiểm tra:**
- [ ] Level spawn ra nhìn thấy và tương tác được.
- [ ] Không lỗi missing material/prefab/config khi spawn.

**Phụ thuộc:** Task 10

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Block\BlockConfig.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Obstacle\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\*.asset`

**Độ lớn:** Vừa

## Task 12: Xác nhận chỉ obstacle DATN cần còn authorable

**Mô tả:** Giữ level editor khớp mục 2.2 bằng cách ẩn/xóa option obstacle không cần mà không phá serialized level data.

**Tiêu chí nghiệm thu:**
- [x] Level editor picker chỉ hiện obstacle/effect cần dùng.
- [x] Enum retired không bị xóa hoặc đổi số.
- [x] Sample level hiện tại hoặc tránh retired-only effect, hoặc được đánh dấu cần xếp lại.

**Cách kiểm tra:**
- [ ] Mở Level Editor và kiểm tra options block/effect.
- [ ] Save/reload test level không serialization warning.

**Phụ thuộc:** Task 10

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Level System\Editor\BlockEffectTypeDrawer.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\ObstacleRuleConfig.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Level System\Obstacle Unlock Database.asset`

**Độ lớn:** Nhỏ

## Checkpoint: Level Playability

- [ ] Task 10-12 hoàn tất.
- [ ] Ít nhất 5 level mẫu spawn và chơi được.
- [ ] Classic win condition thành công khi clear đủ block yêu cầu.

## Task 13: Cấu hình BoosterService và 4 power-up config

**Mô tả:** Wire các booster Classic project dùng: Pump, Hammer, Freeze Timer và Expand.

**Tiêu chí nghiệm thu:**
- [ ] `BoosterService.boosterConfigs` chỉ gồm booster Classic cần dùng.
- [ ] Mỗi booster config có type, price, default count, unlock level, icon và behavior reference.
- [ ] Không reference pre-booster/Gold Mode/shop config đã bỏ.

**Cách kiểm tra:**
- [ ] Booster data init được từ `InventoryService`.
- [ ] Unlock condition chạy ở level đã cấu hình.

**Phụ thuộc:** Task 2, Task 10

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\BoosterService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Data\Power Ups\*.asset`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Configs\BoosterConfig.cs`

**Độ lớn:** Vừa

## Task 14: Wire booster UI và flow mua/dùng

**Mô tả:** Kết nối booster button, count label, locked state, buy popup và runtime power-up behavior vào in-game UI.

**Tiêu chí nghiệm thu:**
- [ ] Booster UI hiện đúng locked/unlocked/empty/usable states.
- [ ] Dùng booster trừ count đi 1.
- [ ] Mua booster trừ coin và tăng count.

**Cách kiểm tra:**
- [ ] Test từng booster từ in-game UI.
- [ ] Console không có lỗi missing prefab/icon/audio từ booster actions.

**Phụ thuộc:** Task 13, Task 7, Task 8

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Power Ups\*.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Power Ups\UI\*.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\UI\PowerUpItemView.prefab`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\Panel\PopupBuyBooster.prefab`

**Độ lớn:** Vừa

## Task 15: Cấu hình win coin reward và lưu inventory

**Mô tả:** Hoàn thành Classic level thì chỉ cộng coin, sau đó lưu coin và booster inventory local.

**Tiêu chí nghiệm thu:**
- [ ] `GameplayConfigService.winRewards` có coin reward cho normal/hard/very hard level type.
- [ ] `PopupWin` hiển thị reward coin mà không cần reward board/liveops khác.
- [ ] Coin và booster balance vẫn còn sau replay hoặc reload scene.

**Cách kiểm tra:**
- [ ] Complete một level và xác nhận coin tăng.
- [ ] Dùng coin mua booster, reload rồi xác nhận balance/count vẫn đúng.

**Phụ thuộc:** Task 2, Task 8, Task 13

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Services\GameplayConfigService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Project Files\Game\Scripts\Game\GameWinFlowService.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scripts\Popup\PopupWin.cs`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\DefaultInventoryService.asset`

**Độ lớn:** Vừa

## Checkpoint: Economy Loop

- [ ] Task 13-15 hoàn tất.
- [ ] Win reward, coin balance, booster buy và booster use tạo thành một vòng hoàn chỉnh.
- [ ] Không còn dependency reward/liveops/ad trong vòng này.

## Task 16: Chạy full Play Mode smoke test

**Mô tả:** Test toàn bộ MVP flow trong Unity từ trạng thái Editor mới mở.

**Tiêu chí nghiệm thu:**
- [ ] Start `Game.unity` từ Play Mode.
- [ ] Chơi level 1 tới khi win.
- [ ] Claim coin reward, continue/replay, dùng ít nhất một booster và mở settings popup.

**Cách kiểm tra:**
- [ ] Unity Console không có error trong smoke test.
- [ ] Save data cập nhật đúng.

**Phụ thuộc:** Task 15

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\`

**Độ lớn:** Nhỏ

## Task 17: Xóa reference SDK/liveops còn sót trong asset đang dùng

**Mô tả:** Sau khi game flow chạy, scan các asset/scene đang dùng và clear reference tới hệ đã bỏ.

**Tiêu chí nghiệm thu:**
- [ ] Không scene/service/prefab nào trỏ tới ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, I2 Localization hoặc iOS app logic.
- [ ] Removed systems không xuất hiện trong active UI/game flow.
- [ ] Vendor package artifact vô hại còn lại được ghi nhận, không wire vào runtime.

**Cách kiểm tra:**
- [ ] Text scan active scene/assets theo tên feature đã bỏ.
- [ ] Unity Console không có missing script warning từ removed systems.

**Phụ thuộc:** Task 16

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Scenes\Game.unity`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\Resources\`
- `D:\Study\DA\Project\DATN_WaterFlow\Assets\WaterFlowFramework\Templates\ServicesSO\`

**Độ lớn:** Vừa

## Task 18: Commit và push phần setup

**Mô tả:** Commit phần setup đã hoàn thành thành các commit sạch, rồi push lên `origin/develop`.

**Tiêu chí nghiệm thu:**
- [ ] Setup changes được tách thành commit rõ ràng.
- [ ] Commit message dùng format project như `[fix]`, `[feat]`, `[chore]`, hoặc `[remove]`.
- [ ] `develop` push thành công.

**Cách kiểm tra:**
- [ ] `git status -sb` sạch sau commit.
- [ ] `git log --oneline -5` có commit setup mới.
- [ ] Sau push, `git status -sb` không còn báo local branch ahead origin.

**Phụ thuộc:** Task 17

**File dự kiến đụng tới:**
- `D:\Study\DA\Project\DATN_WaterFlow\`

**Độ lớn:** Nhỏ

## Checkpoint: Hoàn Tất

- [ ] Task 1-18 hoàn tất.
- [ ] Mở Unity mới compile sạch.
- [ ] `Assets\Scenes\Game.unity` chơi được từ start level, drag, win, coin reward, booster buy/use, replay/continue.
- [ ] Setup work đã commit và push lên `develop`.
