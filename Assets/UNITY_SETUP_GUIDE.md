# Hướng dẫn dựng scene và GameObject trong Unity

Tài liệu này hướng dẫn tạo bộ khung gameplay tối thiểu từ các script đang có trong
`Assets/Scripts`. Chưa cần có art hoàn chỉnh: có thể dùng Sprite placeholder để kiểm tra
di chuyển, phát hiện mục tiêu, đánh trúng và nhận sát thương.

## Cách nhanh nhất: tạo scene demo tự động

Đã có công cụ dựng scene demo tự động để không phải tạo từng GameObject thủ công:

1. Chờ Unity import xong script trong `Assets/Scripts/Editor`.
2. Nếu đã có scene demo, chọn **Tools > Game Setup > Add RPG Demo Menu To Core Combat Demo** để
   thêm menu vào scene hiện có. Nếu chưa có scene, chọn **Tools > Game Setup > Create Core Combat
   Demo**; lệnh này tạo scene Player–Enemy cùng Canvas menu và NPC mẫu.
3. Chọn **Tools > Game Setup > Build Connected Zones Through Session 5** để dựng lại
   `CoreCombatDemo` từ nội dung cốt truyện và sơ đồ Zone trong `BongNgu_GameDesign.xlsx`.
   Zone 1 nối liền Nhà Hoang, Rừng Thông Ven Sông, Làng Ven Sông và Đồi Cao; Zone 2 nối
   Nhà Kho Đá với Quảng Trường. Session là tiến trình sự kiện, không phải từng map dịch chuyển.
   Các ngoại lệ là cửa Nhà Hoang, cầu thang lên/xuống gác và lối vào Hành Lang Gương; mỗi lối
   dịch chuyển có điểm đi/về riêng. Công cụ tạo placeholder cho nhà xám, cửa đen, quái, rương,
   đồ rơi và cảnh quan, đồng thời tạo scene `MainMenu`/đăng ký Build Settings.
4. Khi chạy game, menu chính có **CHƠI MỚI** và **TIẾP TỤC**; Continue chỉ bật nếu có save.
   Nếu muốn mở thẳng demo trong Editor, nhấn Play trong `CoreCombatDemo`.
5. Nhấn **Play**:
   - Di chuyển Player: `WASD` hoặc phím mũi tên.
   - Trong demo combat cơ bản: `J` cận chiến, `K` đạn bay, `L` cầu lửa, `I` dash attack.
   - `J` chém cơ bản; `Space` né nhanh sau khi đạt Lv2.
   - Chạm vào vật phẩm để nhặt; đến gần rương gỗ trong Nhà Hoang để cất/lấy toàn bộ túi đồ
     bằng `F` hoặc các nút trên bảng trạng thái. Rương gác mái mở bằng `E` và chứa Nhật Ký.
   - Nút **MENU** góc trên bên phải: mở menu; nhấn **ESC** hoặc **RESUME** để đóng.
6. Mở đầu trong căn Nhà Hoang và hạ 3 Chuột Cống Biến Dị; cửa và cầu thang đang khóa, xác
   chưa thể tương tác. Khi đàn chuột bị hạ, vùng sự kiện bên xác được kích hoạt và Khải tự thoại,
   chọn chôn xác/rời nhà hoặc điều tra. Nhánh điều tra mở cầu thang lên gác. Sau khi lấy Nhật Ký,
   Khải tự hỏi có nên đọc hay đốt. Chọn đọc sẽ giữ cuốn sách trong túi, mở trang đọc và gọi nhện;
   có thể đóng sách rồi mở lại bằng **XEM CHI TIẾT** trong Inventory. Chọn đốt sẽ tiêu thụ sách.
   Cầu thang xuống vẫn khóa tới khi giải quyết xong bầy nhện; sau đó lối về và cửa nhà được mở.
   Tiếp tục theo địa hình nối liền qua làng, am thờ, nhà kho/giấc mơ và quảng trường; nói chuyện
   với NPC sau khi dọn quái để xử lý các lựa chọn Session 2A, 3, 4A/4B và 5. Mỗi nơi giữ nguyên
   vị trí trên cùng bản đồ. LT/QT, lựa chọn, EXP, túi đồ, vật phẩm và vị trí lưu ở
   `Application.persistentDataPath/Data/Player.json`.
7. Đi gần NPC để hiện gợi ý **Press E to talk**; nhấn `E` để mở hội thoại. Player bị khóa di chuyển
   khi trò chuyện. Nhấn `E` để bỏ qua hiệu ứng chữ; nhấn `E` lần nữa để tiếp tục/kết thúc. Không có
   nút Next/Close; chọn nhánh bằng lựa chọn hiển thị trong hội thoại. Nhấn `Esc` để đóng sớm.
   Bảng thông số bên trái có thể ẩn/hiện bằng `F1`. Nút **MENU** mở Inventory, World Map và Settings.
8. Bản đồ lấy vị trí Player làm tâm; polygon vùng màu chỉnh được trong Inspector. Các vùng rừng,
   làng, đồi, nhà kho và quảng trường được đặt cạnh nhau để đi bộ qua lại.

Inventory có lưới 6×4, kéo-thả/đổi chỗ vật phẩm, stack, chi tiết/chỉ số, dùng vật phẩm hồi máu,
và trang bị/bỏ trang bị. Dùng hết item sẽ tự dồn các item phía sau lên lấp ô trống. Ô đang trang bị hiện `EQUIPPED`; slot index và tên trang bị được lưu
trong save. Chọn `Resources/ItemDefinitions` để chỉnh loại/chỉ số và `Resources/ItemIcons` để
thay sprite vuông placeholder; từng `WorldItemPickup` cũng giữ reference `Item Definition` trong
Inspector để admin cấu hình trực tiếp.

### Cấu hình sự kiện cốt truyện

- `Story Event Settings` là điểm quản lý sự kiện trong Hierarchy. `StoryEventDirector > Events`
  cho phép thêm ID sự kiện, profile hội thoại, thao tác bật/tắt GameObject, bật cờ tiến trình,
  tiêu thụ vật phẩm, mở sách hoặc gọi một hay nhiều spawner.
- Thêm `StoryEventTrigger` cùng một `Collider2D` bật `Is Trigger` vào GameObject vùng kích hoạt;
  nhập `Event Id` khớp với ID trong `Story Event Settings`. Khi Player chạm vùng, event sẽ chạy.
- Câu hỏi có lựa chọn chỉ hiện khi `sessionId` của choice trùng `PlayerSaveData.currentSession`
  và session đó chưa có choice trong save. Xem cách cấu hình chi tiết, bật/tắt NPC và nối event tại
  [`StoryEventDirector_Guide.md`](./StoryEventDirector_Guide.md).
  Event một lần tự lưu trạng thái để không lặp sau khi load save.
- Với chuỗi sau chiến đấu, chọn spawner và đặt `Event Id On Cleared`. Sự kiện đó tự chạy khi
  toàn bộ quái của spawner đã bị hạ; có thể dùng nó để mở cổng, bật NPC hoặc bắt đầu encounter
  tiếp theo. Cấu hình số quái, prefab, vị trí, bán kính và tỉ lệ loot trực tiếp trên
  `ZoneEnemySpawner`.

Skill `DashAttack2` cho nhân vật lướt xuyên qua hitbox mục tiêu, bật bất tử và ẩn thanh máu
trong lúc lướt. Khi dash kết thúc, nhân vật hiện lại và hết bất tử.

Khi menu pause mở, gameplay được pause toàn cục: Player/quái, physics, projectile, DOT, animation
và thời gian cooldown đều đứng yên. Các skill bị chặn không cho kích hoạt; đóng menu mới cho thời
gian gameplay chạy tiếp. Hội thoại NPC là giao diện độc lập, không pause gameplay.

Scene demo dùng sprite màu placeholder và không thay thế art/animation thật. Muốn đổi sprite,
chọn object `SkillActions` để chỉnh sprite riêng trong `Skill Registrations`, hoặc chọn Player/
Enemy để thay sprite nhân vật. Scene hiện tại chỉ bị thay thế sau khi bấm xác nhận trong hộp
thoại; scene demo đã tồn tại cũng cần xác nhận riêng trước khi ghi đè.

Nếu menu chưa xuất hiện, mở Console và sửa lỗi compile trước. Nếu muốn tự dựng layout hoặc hiểu
cách gắn từng component, tiếp tục phần hướng dẫn thủ công bên dưới.

### Chỉnh nội dung NPC và vùng bản đồ demo

- Chọn NPC trong Hierarchy > `DemoNpcDialogueProfile`: sửa tên, nội dung trong `Dialogue`, các lựa chọn,
  phản hồi và `Next Node Index`. Đánh số node từ `0`; giá trị `-1` kết thúc nhánh.
- Kéo các frame sprite vào `Portrait Frames` và chỉnh `Portrait Frame Rate` để đổi animation ảnh
  đại diện trong khung nói chuyện. Không cần Animator hoặc Canvas riêng cho NPC.
- Chọn `Demo Menu Canvas` > `DemoNpcDialogueController` để chỉnh `Interaction Range` và các
  reference UI. Khi Player ở trong khoảng tương tác, hiện prompt; `E` bắt đầu hội thoại.
  Đóng hội thoại bằng `Esc` hoặc nút **CLOSE**. Hội thoại vẫn tiếp diễn khi gameplay đang chạy.
- Chọn object `Map Zone - ...` và sửa các điểm trong `PolygonCollider2D > Edit Collider` để đổi
  hình đa giác. `MapZoneRegion` có tên/màu hiển thị; collider này bị tắt khi Play nên chỉ dùng
  làm dữ liệu hiển thị bản đồ, không chặn Player.
- `Demo Menu Canvas` và toàn bộ nút, panel, prompt tương tác, khung thoại, ảnh, text, lựa chọn,
  map viewport, slider đều được tạo sẵn và lưu trong scene khi chạy công cụ Editor. Mở rộng
  `Demo Menu Canvas` trong Hierarchy rồi chỉnh `Rect Transform`, `Image`, `Text`, sprite và màu
  trực tiếp trong Inspector. `DemoPauseMenu` và `DemoNpcDialogueController` chỉ tham chiếu các
  object đã tạo, không tự dựng Canvas/UI lúc Play.
- Để áp dụng lại giao diện sau khi đổi script, chọn **Tools > Game Setup > Add RPG Demo Menu To
  Core Combat Demo**. Lệnh này thay Canvas demo cũ bằng một cây UI scene mới rồi lưu scene; chỉnh
  lại layout sau đó trong Hierarchy. Không cần mở Play để tạo UI.
- Trong demo tới Session 5, tab Inventory hiển thị lưới 24 ô và đồng bộ tên,
  số lượng, mô tả và chỉ số từ `PlayerSaveData`/`ItemDefinition`. Rương có container ID riêng và dữ liệu
  vẫn còn sau khi lưu/tải. Tab Settings có ví dụ chỉnh master volume và fullscreen.

## 1. Chuẩn bị project và Input

1. Mở project bằng Unity Editor. Thư mục hiện tại cần có `ProjectSettings` và `Packages`;
   nếu chưa có, hãy tạo project Unity 2D trước rồi chép thư mục `Assets` vào project đó.
2. Chờ Unity import hết script, rồi mở tab **Console** và sửa tất cả lỗi đỏ trước khi tạo scene.
3. Mở **Edit > Project Settings > Player > Other Settings > Configuration**:
   - Đặt **Active Input Handling** thành **Both** hoặc **Input Manager (Old)**.
   - Script di chuyển Player hiện gọi `Input.GetAxisRaw("Horizontal"/"Vertical")`; nếu chỉ bật
     Input System mới, các lệnh đọc Input Manager cũ sẽ không hoạt động đúng.
   - Nếu chọn **Both**, Unity có thể yêu cầu khởi động lại Editor.
4. Trong **Project Settings > Input Manager**, kiểm tra các axis mặc định `Horizontal` và
   `Vertical` còn tồn tại. Mặc định của Unity đã có WASD và phím mũi tên.

## 2. Tạo Tags trước khi tạo nhân vật

Mở **Edit > Project Settings > Tags and Layers** và thêm các tag sau (đúng chữ hoa/thường):

| Tag | Gắn vào | Dùng ở đâu |
|---|---|---|
| `Player` | GameObject gốc Player | EnemySkillController tìm Player để bỏ va chạm vật lý |
| `HitBoxPlayer` | Collider trigger của Player | ZoneSee và skill của Enemy phát hiện Player |
| `HitBoxEnemy` | Collider trigger của Enemy | Skill của Player tìm mục tiêu có thể nhận sát thương |
| `Skill` | Có thể tạo trước để thống nhất | Hitbox, projectile, detector và DOT zone được tạo lúc chạy |

`Skill` phải là tag đã được tạo trong project trước khi chạy: các script gán tag này cho
GameObject runtime. Đừng gắn tag `HitBoxPlayer`/`HitBoxEnemy` lên root thay cho collider con,
vì skill tìm tag trên chính collider va chạm.

Layers có thể để mặc định khi bắt đầu. Riêng sorting layer `Decor` chỉ cần tạo nếu muốn các
đường debug hitbox/DOT được xếp đúng lớp hiển thị.

## 3. Tạo scene test và đối tượng dùng chung

Tạo thư mục `Assets/Scenes/Test` nếu chưa có, rồi tạo scene `CoreTest`.

Trong Hierarchy, tạo:

```text
CoreTest
├── Main Camera
├── SkillActions
├── Player
└── Enemy_Test
```

### Main Camera

- Projection: **Orthographic**.
- Position ban đầu: `(0, 0, -10)`.
- Chỉnh `Size` để nhìn được cả Player và Enemy.
- Game hiện chưa có script follow camera; lúc đầu đặt hai nhân vật trong khung camera.

### SkillActions

1. Tạo **Create Empty**, đặt tên `SkillActions`.
2. Thêm component `SkillActions`.
3. Trong **Skill Registrations**, giữ hoặc tạo đúng các dòng:
   `ArcAttack`, `NormalAttack`, `FlyAttack`, `Fireball`, `DashAttack2`.
4. Tên phải khớp chính xác với tên được nhập trong skill slot của Player/Enemy.
5. Kéo sprite skill vào `Skill Sprites` ở chính dòng đăng ký tương ứng (chi tiết ở mục 7).
6. Đặt object này trong scene khởi đầu. Component sẽ đăng ký singleton và gọi
   `DontDestroyOnLoad`, do đó scene sau không cần tạo thêm một bản thứ hai. Có thể tạo một
   scene Boot sau này để chứa object này.

`SkillActions` chỉ nhận các tên đang có trong `CreateSkill()` của script. Muốn thêm loại skill
mới cần có class implement `ISkill` và thêm mapping trong `CreateSkill()`; chỉ thêm tên ở
Inspector sẽ không tự tạo class.

## 4. Tạo Player

Tạo GameObject gốc tên `Player`, đặt Tag là `Player`, Position ví dụ `(-2, 0, 0)`.
Gắn các component sau lên **Player root**:

| Component | Cấu hình |
|---|---|
| `Rigidbody2D` | Body Type `Dynamic`, Gravity Scale `0`, Freeze Rotation Z |
| `CapsuleCollider2D` hoặc `BoxCollider2D` | `Is Trigger` tắt; đây là collider thân/vật lý |
| `SpriteRenderer` | Kéo sprite tạm của nhân vật vào; có thể để sprite placeholder |
| `HealthController` | Max Health và Current Health, ví dụ `100` |
| `CharacterAnimation` | Clips animation của Player; Sprite Renderer tham chiếu cùng object |
| `PlayerController` | Move Speed; Character Animation tham chiếu component cùng root |

`HealthController` tự tạo thanh máu khi vào Play, không cần thêm Canvas hay kéo UI vào scene.
Trong phần **Health Bar** của component, bật/tắt thanh và chỉnh offset, kích thước, màu nền,
màu phần máu còn lại và sorting order. Thiết lập này dùng được cho cả Player và Enemy. Khi HP
về 0, các Collider2D trên nhân vật và object con sẽ tắt để projectile bay xuyên qua. Gọi
`HealthController.Heal(amount)` để hồi máu; nếu nhân vật đang chết, hồi lên trên 0 sẽ bật lại
các collider vốn đang bật trước lúc chết. Khi đang Play, chỉnh `Current Health` trực tiếp trong
Inspector từ 0 lên trên 0 cũng khôi phục collider và cập nhật thanh máu để test Enemy hồi sinh.

Tạo hai object con dưới Player:

```text
Player
├── HitBoxPlayer
└── AttackController
```

### HitBoxPlayer

- Tag: `HitBoxPlayer`.
- Thêm `BoxCollider2D` hoặc `CircleCollider2D`, bật **Is Trigger**.
- Chỉnh collider bao quanh vùng thân có thể bị trúng.
- Không cần Rigidbody2D riêng; collider con dùng Rigidbody2D ở Player root.
- `HealthController` đặt trên root để `GetComponentInParent<IDamage>()` tìm thấy khi hitbox
  con bị đánh trúng.

### AttackController

- Để làm object con trực tiếp của Player.
- Thêm component `PlayerSkillController`.
- Inspector `Skill Slots`: tạo một phần tử cho mỗi kỹ năng cần dùng.
- Để `Owner Override` trống để script lấy Player root làm caster.
- Không cần tạo hitbox tấn công thủ công; các skill tạo collider runtime.

Ví dụ slot để bắt đầu:

| Skill Name | PC Key | Damage | Skill Size | Range | Speed | Duration |
|---|---:|---:|---|---:|---:|---:|
| `NormalAttack` | `j` | 10 | `(1.5, 1)` | 1 | 5 | 0.25 |
| `FlyAttack` | `k` | 10 | `(0.5, 0.5)` | 0.5 | 8 | 2 |
| `Fireball` | `l` | 15 | `(0.6, 0.6)` | 0.5 | 6 | 2 |

`Stun Time` là thời gian làm choáng mục tiêu khi skill đánh trúng. Để `0` nếu không muốn stun:
mục tiêu vẫn nhận `Hurt`, bị khóa di chuyển và không cast skill trong `0.1` giây. Nếu `Stun Time`
lớn hơn `0`, animation `Stun` chạy và thanh xanh dưới thanh máu giảm dần; hết thanh thì hết stun.
Gán đủ sprite bốn hướng cho state `Hurt`/`Stun` trên `CharacterAnimation` để thấy đúng animation.

Các trường khác:

- `UI Button`: để trống nếu chưa làm UI.
- `Mouse Button`: `None` nếu chỉ dùng phím.
- `Character Animation Sprites`: chỉ sprite animation của Player khi đánh; **không** bỏ sprite
  projectile/skill vào đây.
- `Debug`: bật khi muốn nhìn đường viền hitbox trong Game view.
- `Can Pierce`: tắt để projectile dừng ở mục tiêu đầu tiên.
- `Dot Config`: giữ `None` nếu chưa muốn dùng sát thương theo thời gian.

Các phím chỉ là ví dụ; có thể đổi trong Inspector. `PlayerController` hiện di chuyển bằng
Horizontal/Vertical (WASD hoặc mũi tên), không cần thêm component Input riêng.

## 5. Tạo Enemy

Tạo GameObject gốc `Enemy_Test`, đặt ví dụ `(2, 0, 0)`. Không có script hiện tại nào cần tag
`Enemy` trên root; tag cần thiết nằm ở collider con.

Gắn lên root:

| Component | Cấu hình |
|---|---|
| `Rigidbody2D` | Dynamic, Gravity Scale `0`, Freeze Rotation Z |
| `CapsuleCollider2D` hoặc `BoxCollider2D` | `Is Trigger` tắt |
| `SpriteRenderer` | Sprite placeholder |
| `HealthController` | Max/Current Health, ví dụ `100` |
| `CharacterAnimation` | Sprite Renderer và clips của Enemy |
| `EnemyController` | Move Speed, Stop Distance; Character Animation tham chiếu cùng root |

Tạo các object con:

```text
Enemy_Test
├── HitBoxEnemy
├── DetectionZone
└── AttackController
```

### HitBoxEnemy

- Tag: `HitBoxEnemy`.
- Thêm `BoxCollider2D` hoặc `CircleCollider2D`.
- Bật **Is Trigger**.
- Chỉnh collider vừa vùng thân Enemy.
- Không thêm Rigidbody2D riêng.

### DetectionZone

- Thêm `CircleCollider2D`, bật **Is Trigger**.
- Thêm script `ZoneSee`.
- Đặt `Radius`, ví dụ `4`.
- Có thể để `Enemy Body` và `Enemy Controller` trống: script tự tìm parent khi Player đi vào.
- Đặt làm object con trực tiếp Enemy để script theo dõi đúng vị trí.

ZoneSee bắt đầu đuổi khi collider tag `HitBoxPlayer` vào vòng tròn; khi ra khỏi vùng thì
controller xóa target. Vì thế Player phải có child collider `HitBoxPlayer` ở mục trước.

### AttackController

- Đặt làm object con **trực tiếp** của Enemy root.
- Thêm `EnemySkillController`.
- Để `Owner Override` trống.
- Trong `Skill Slots`, thêm các skill Enemy được phép tự dùng.
- `Detect Tag`: `HitBoxPlayer`.
- `Shape`, `Detection Size`, `Detection Offset` điều khiển vùng phát hiện riêng của slot đó.
- `Skill Name` phải trùng tên đã đăng ký trong `SkillActions`.
- Các thông số Damage/Size/Range/Speed/Duration truyền cho skill khi cast.
- `Stun Time`: thời gian stun mục tiêu khi skill trúng; `0` dùng Hurt mặc định `0.1` giây.
- `Character Animation Sprites`: frames animation của Enemy khi dùng skill (nếu skill như
  `NormalAttack` cần animation nhân vật).

`EnemySkillController` tự tạo Detector collider con khi bắt đầu Play; không tự tạo Detector
trước trong Hierarchy. Script cũng tìm root Player theo tag `Player` lúc khởi tạo để bỏ va chạm
vật lý giữa thân Enemy và thân Player. Hãy có Player trong scene trước khi nhấn Play.

## 6. Thiết lập animation

Trên `CharacterAnimation` của Player/Enemy, mỗi state có bộ sprite riêng theo bốn hướng:

1. Gán `Sprite Renderer` của chính GameObject đang có component `CharacterAnimation`.
2. Tạo phần tử trong `Clips` cho từng state cần dùng: `Idle`, `Run`, `Die`; thêm `Hurt`, `Stun`,
   `Attack` hoặc `Skill` khi đã có sprite tương ứng.
3. Trong từng clip, kéo frame theo thứ tự vào `Down Frames`, `Up Frames`, `Right Frames` và
   `Left Frames`. Ví dụ frame nhân vật nhìn lên phải nằm trong `Up Frames`. Mỗi hướng chạy độc lập
   và dùng chung `Frame Rate`/`Loop` của state đó.
4. Đặt `Frame Rate` (ví dụ 8–12), bật `Loop` cho Idle/Run; thường tắt Loop cho Hurt/Die/Attack.
   Nếu thiếu hướng, animation tạm dùng bộ frame khác làm fallback và Console cảnh báo khi không
   có frame nào cho state được gọi. Các clip cũ tự giữ sprite cũ trong `Down Frames`; hãy bổ sung
   ba hướng còn lại trong Inspector.

`PlayerController` và `EnemyController` cập nhật hướng nhìn theo trục di chuyển trội: lên, xuống,
trái hoặc phải. Sau đó `CharacterAnimation` chọn đúng bộ frame của state hiện tại cho hướng đó.
Scene demo mới tạo sẵn bốn bộ directional frames placeholder cho mỗi state; thay bằng sprite art
tương ứng trong Inspector.

Hiện `PlayerController` tự gọi `Idle`, `Run`, `Die`; `EnemyController` cũng gọi các state này.
Khi nhận damage, `HealthController` gọi controller để phát `Hurt` trong `0.1` giây mặc định hoặc
`Stun` trong thời lượng `Stun Time` của skill. Cả hai đều khóa di chuyển và chặn cast skill.
Thanh stun hiển thị ngay dưới thanh máu và đếm lùi trong lúc gameplay chạy.

## 7. Sprite riêng cho từng skill

Sprite skill được cấu hình tại **GameObject `SkillActions` > Skill Registrations**, không nhập
trong `SkillContext` hay `PlayerSkillSlot`.

| Tên đăng ký | Sprite cần kéo vào | Ghi chú |
|---|---|---|
| `ArcAttack` | Không cần | Đòn cận chiến dùng hitbox; không có visual riêng trong code hiện tại |
| `NormalAttack` | Không ở registry | Dùng `Character Animation Sprites` của slot Player/Enemy |
| `FlyAttack` | 4 sprite | Thứ tự: Down, Up, Left, Right; hitbox/sprite không tự xoay |
| `Fireball` | 12 hoặc 48 sprite | 12: 6 frame bay + 6 frame nổ; hitbox bay xoay theo hướng, vùng nổ giữ góc 0. 48: bốn nhóm hướng Down/Up/Left/Right, mỗi nhóm 6 frame bay + 6 frame nổ; hitbox không xoay |
| `DashAttack2` | 4 sprite | Thứ tự: Down, Up, Left, Right |

Với `FlyAttack`, nếu chỉ gán một sprite thì sprite và hitbox bay sẽ xoay theo hướng cast.
Fireball dùng 12 sprite nếu art chỉ có một hướng; dùng 48 sprite nếu có animation riêng cho cả bốn
hướng. Với 48 sprite, thứ tự là Down (0–11), Up (12–23), Left (24–35), Right (36–47); trong
mỗi nhóm, 6 frame đầu là bay và 6 frame sau là nổ. Khi dùng bộ bốn hướng, hitbox bay và vùng nổ
không xoay; skill chọn frame đúng hướng. Vùng nổ Fireball luôn giữ góc 0 để không làm lệch vùng
damage.

Nếu bỏ trống các sprite riêng, hitbox/gameplay của skill vẫn có thể chạy, nhưng projectile hoặc
animation tương ứng sẽ không có hình. Sprite hiện được gắn vào `SpriteRenderer` runtime; muốn
đẹp hơn có thể bổ sung chỉnh scale/sorting trong class skill sau khi test.

## 8. Physics và collision

- Player và Enemy đều cần `Rigidbody2D` trên root.
- Collider thân root: `Is Trigger` tắt. Hurtbox con: `Is Trigger` bật.
- Hitbox/Detector/Zone runtime đều là trigger; `ZoneHitboxSpawner` và
  `ProjectileHitboxSpawner` tự tạo Rigidbody2D Kinematic.
- Nếu trigger không chạy: kiểm tra hai collider có bật, có Rigidbody2D trong cặp va chạm, đúng
  tag, đúng layer collision matrix, và Console không báo tag chưa được khai báo.
- Tạm dùng layer mặc định là được. Nếu tự chỉnh **Physics 2D Layer Collision Matrix**, đảm bảo
  layer của Player/Enemy/hitbox vẫn được phép overlap.

## 9. Play test theo thứ tự

1. Lưu scene `CoreTest`; kiểm tra chỉ có một GameObject `SkillActions`.
2. Nhấn Play và di chuyển Player bằng WASD/mũi tên.
3. Đi vào DetectionZone của Enemy: Enemy phải đuổi Player.
4. Đứng trong vùng `EnemySkillController` detect: Enemy thử cast skill khi cooldown sẵn sàng.
5. Nhấn `j` để thử NormalAttack; bật Debug để thấy hitbox.
6. Nhấn `k` để thử projectile; nhấn `l` để thử Fireball nếu đã gán đủ sprite.
7. Quan sát thanh máu phía trên Enemy và `Current Health` trên `HealthController` trong Inspector.
   Đòn đánh phải làm HP giảm; khi về 0, EnemyController chuyển animation sang Die, dừng di chuyển,
   và projectile phải bay xuyên qua vì collider của Enemy đã tắt.
8. Kiểm tra Console. Các lỗi thường gặp:
   - `SkillActions` không tìm thấy skill: sai tên slot hoặc chưa có mapping trong `CreateSkill()`.
   - Không trúng mục tiêu: collider con sai tag, collider không bật, hoặc layer không overlap.
   - Player không di chuyển: Active Input Handling chỉ bật Input System mới hoặc axis cũ bị xóa.
   - Enemy không đuổi: `ZoneSee` không ở dưới Enemy, hoặc Player thiếu collider tag `HitBoxPlayer`.
   - Không có hình projectile: chưa gán skill sprites tại `SkillActions` hoặc sprite sorting/scale.
9. Với menu demo: mở `CoreCombatDemo`, chọn **Tools > Game Setup > Add RPG Demo Menu To Core
   Combat Demo**, rồi chạy scene. Đi gần NPC: prompt tương tác hiện; nhấn `E`: hội thoại mở nhưng
   Player/quái/projectile/DOT/cooldown vẫn chạy. Đóng bằng `Esc`/nút **CLOSE**. Mở menu: gameplay
   phải đứng yên; tab Map phải chỉ Player, Enemy, NPC và các polygon. Đóng menu: gameplay và timer
   tiếp tục.

## 10. Phạm vi demo và lưu ý

- `MainMenu` và `CoreCombatDemo` là scene demo Session 1–2; nội dung/hình ảnh placeholder cần được
  thay bằng art và cốt truyện sản phẩm khi có tài nguyên chính thức.
- Save lưu túi đồ theo slot, vật phẩm đã nhặt, tiến trình, lựa chọn, chỉ số và trang bị. Dữ liệu
  save cũ không có slot sẽ được chuẩn hóa khi tải. Scene demo không thay thế toàn bộ luồng gameplay
  hoặc quest của các session ngoài phạm vi 1–2.
- Script riêng `PlayerHealth`/`EnemyHealth`; cả hai dùng `HealthController` + `IHealth`/`IDamage`.
- Animation controller kích hoạt Hurt/Stun tự động.
- Skill class ngoài các tên đang được map trong `SkillActions.CreateSkill()`.

Hãy hoàn thiện và test scene `CoreTest` trước, sau đó tạo prefab từ Player và Enemy để tái sử dụng
trong các scene/zone.
