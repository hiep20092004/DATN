# Kế Hoạch Triển Khai: Setup Game WaterFlow

## Tổng Quan

Setup DATN_WaterFlow thành bản MVP chơi được ở chế độ Classic, dựa trên logic gameplay, load level, booster, framework và reward đã clone từ WorksSonatFlowJam. Phạm vi chỉ gồm setup runtime và cấu hình: không ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, plugin localization, hoặc logic riêng cho iOS. Mục tiêu đầu tiên là một scene `Game.unity` chạy được: load level local, kéo block, hoàn thành màn, nhận coin, dùng coin mua booster, replay/continue.

## Hiện Trạng

- `Assets/Scenes/Game.unity` hiện gần như là scene mặc định: chỉ có Main Camera, Directional Light và Global Volume.
- `ProjectSettings/EditorBuildSettings.asset` hiện chỉ thêm `Assets/Scenes/Game.unity`.
- Core init settings đã có tại `Assets/Project Files/Data/Project Init Settings.asset`.
- Level data đã có tại `Assets/Project Files/Level System/LevelDatabase.asset`, kèm level mẫu trong `Assets/Project Files/Level System/Editor/Levels/`.
- Gameplay scripts đã có trong `Assets/Project Files/Game/Scripts/`, gồm `GameController`, `LevelController`, `UIGame`, level representation, power-ups và editor tooling.
- `Assets/WaterFlowFramework/Templates/ServicesSO/[WATERFLOW] SERVICE MANAGER.asset` vẫn còn nhiều GUID bị mất file, cần dọn trước khi Play Mode đáng tin cậy.
- Art/prefab polish đang để bạn config sau; nếu cần unblock logic thì dùng prefab/visual placeholder tối thiểu.

## Quyết Định Kiến Trúc

- Theo flow 3 scene của FlowJam, đúng như framework đã mã hoá sẵn: `GamePlacement { Loading, Home, Game }` trong `Assets/Scripts/Enums/GameMode.cs` có comment "Must same with Scene Build List", và `GamePlacementSceneNames.ToBuildSceneName()` map sang asset tên `Loading`, `Home`, `Game`. Build list bắt buộc theo thứ tự Loading(0) -> Home(1) -> Game(2). Chuyển scene đi qua `TransitionService.SwitchScene(GamePlacement)`.
- Boot chạy từ scene Loading: một GameObject mang `GameLoading` + `Initializer` (+ `LoadingGraphics`) kích `ProjectInitSettings`, trong đó `SaveInitModule` gọi `SaveController.Init()`. Mọi thứ đọc save (`ActiveSession`, `BoosterService`) không được chạy trước mốc đó. Hệ thống persistent (`GameSystem`, `PanelManager`, `UIController`) nằm ở scene Loading và sống nhờ `DontDestroyOnLoad`.
- Dùng lại entry point có sẵn: `GameSystem` cho services, `PanelManager` cho popup, `UIController` cho page, `GameController` cho gameplay boot, `LevelController` cho vòng đời level.
- Chỉ dùng data local. Load level qua editor/local asset, không remote config hay SDK.
- Giữ ổn định giá trị enum của level data. Obstacle không dùng thì ẩn khỏi editor, không xóa hoặc đổi thứ tự enum vì level asset serialize bằng số.
- Dùng placeholder art khi cần để test logic; phần hình ảnh/prefab đẹp có thể thay sau.

## Danh Sách Công Việc

### Pha 1: Nền Tảng Compile và Config

- [ ] Task 1: Xác nhận Unity compile sạch
- [ ] Task 2: Dựng lại runtime Service Manager asset
- [ ] Task 3: Kiểm tra package và scripting define symbols cần thiết

### Checkpoint: Nền Tảng

- [ ] Unity Console không còn lỗi C#.
- [ ] Service Manager không còn reference service bị mất.
- [ ] Vào Play Mode tới frame đầu tiên mà không lỗi resolve service.

### Pha 2: Bootstrap Scene Game

- [ ] Task 4: Dựng hierarchy runtime cho một scene
- [ ] Task 5: Cấu hình camera, input, raycast và world containers
- [ ] Task 6: Wire reference cho `GameController` và `LevelController`

### Checkpoint: Scene Boot

- [ ] Bấm Play trong `Assets/Scenes/Game.unity` khởi tạo được core services.
- [ ] Level 1 load từ local/editor level data.
- [ ] Không có missing-reference exception khi scene start.

### Pha 3: Gameplay UI và Popup Flow

- [ ] Task 7: Tạo hierarchy tối thiểu cho `UIGame`
- [ ] Task 8: Tạo các popup prefab cần cho Classic mode
- [ ] Task 9: Cấu hình panel/resource loading cho UI prefab

### Checkpoint: UI Flow

- [ ] Game UI hiện level number, timer, settings, replay và booster slots.
- [ ] Win flow mở đúng đường popup pre-win/win.
- [ ] Lose/revive flow không gọi ads, lives, remote config hoặc Gold Mode.

### Pha 4: Level và Obstacle

- [ ] Task 10: Validate LevelDatabase và 30 level mẫu
- [ ] Task 11: Cấu hình environment, block visuals, colors, gates và obstacle configs
- [ ] Task 12: Xác nhận chỉ các obstacle cần cho DATN còn hiện trong editor

### Checkpoint: Level Playability

- [ ] Ít nhất 5 level mẫu spawn được, không lỗi prefab/config.
- [ ] Block kéo, snap, nhận nước từ gate và clear được.
- [ ] Win condition chạy đúng ở Classic mode.

### Pha 5: Booster và Coin Economy

- [ ] Task 13: Cấu hình BoosterService và 4 power-up config
- [ ] Task 14: Wire booster UI và flow mua/dùng
- [ ] Task 15: Cấu hình win coin reward và lưu inventory

### Checkpoint: Economy Loop

- [ ] Win level cộng coin qua `GameWinFlowService`.
- [ ] Số lượng booster lưu qua `InventoryService`/`DataService`.
- [ ] Dùng coin mua booster thành công/thất bại đúng theo số dư.

### Pha 6: Validate Cuối và Dọn Dẹp

- [ ] Task 16: Chạy full Play Mode smoke test
- [ ] Task 17: Xóa reference SDK/liveops còn sót trong asset đang dùng
- [ ] Task 18: Commit và push phần setup

### Checkpoint: Hoàn Tất

- [ ] Mở project mới compile sạch.
- [ ] `Game.unity` chơi được từ start level tới win reward rồi next/replay.
- [ ] Runtime không phụ thuộc ads, tracking, remote config, Firebase, IAP/shop, lives, Gold Mode, Journey, leaderboard, I2 Localization hoặc iOS app logic.
- [ ] Setup đã commit và push lên `develop`.

## Rủi Ro và Cách Giảm

| Rủi ro | Ảnh hưởng | Cách xử lý |
|--------|-----------|------------|
| Scene/prefab thiếu reference sau khi port logic | Cao | Dựng scene theo từng checkpoint nhỏ và test Play Mode sau mỗi nhóm việc. |
| Service Manager còn reference tới SDK đã xóa | Cao | Tạo lại service list chỉ từ các WaterFlow service asset còn tồn tại. |
| Level asset còn trỏ tới obstacle đã retire | Trung bình | Giữ enum ổn định, ẩn type khỏi editor, validate sample levels trước khi xóa data asset. |
| UI prefab từ FlowJam kéo theo hệ thống đã bỏ | Trung bình | Tạo prefab tối thiểu cho Classic-only thay vì copy nguyên UI Home/Shop/Journey. |
| Placeholder visual che mất bug gameplay thật | Trung bình | Tách smoke test logic khỏi pass polish art; verify block/gate trước. |
| Package import chưa ổn | Trung bình | Xem package/asmdef là blocker của Task 1 trước khi setup runtime. |

## Phần Có Thể Làm Song Song

- Scene bootstrap và service asset cleanup phải làm tuần tự vì nhiều hệ runtime phụ thuộc vào chúng.
- Validate level data có thể làm song song với dựng UI prefab sau khi compile sạch.
- Booster config và win reward config có thể chuẩn bị song song sau khi `InventoryService` và `DataService` chạy ổn.

## Câu Hỏi Mở

- Style art/prefab cuối chưa chốt trong plan này; dùng placeholder tối thiểu cho tới khi bạn config art.
- Flow 3 scene (Loading -> Home -> Game) đã được chủ project xác nhận và khớp enum `GamePlacement`; kế hoạch một-scene trước đó là sai và đã bỏ. `Loading.unity` và `Home.unity` được mang từ FlowJam sang để có luôn UI loading thật.
