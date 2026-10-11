# Hướng dẫn AI/Admin: Story Event, Session 1–5

Tài liệu này mô tả luồng đang được triển khai trong `CoreCombatDemo`, cách cấu hình bằng
Unity Inspector và cách chuyển nội dung cốt truyện thành bảng sự kiện trong Excel. Dùng
`Assets/cot-truyen-bong-ngu.md` làm nguồn cốt truyện. Demo hiện nối gameplay đến hết Session 5;
Session 6 trở đi trong file cốt truyện chưa thuộc phạm vi scene demo này.

Scene: `Assets/Scenes/Test/CoreCombatDemo.unity`.

## 1. Các hệ thống và thứ tự xử lý

- **Story Event Settings / `StoryEventDirector`** là danh mục event của scene. Mỗi event có ID,
  One Shot, profile hội thoại tùy chọn, action chạy khi event bắt đầu và action theo từng choice.
- **`StoryEventTrigger`** đặt trên GameObject có Collider2D `Is Trigger`. Khi Player đi vào vùng,
  script gọi `StoryEventDirector.TriggerEvent(eventId)`. Trigger chờ nếu event chưa thể chạy và
  tắt sau khi One Shot hoàn tất.
- **`ZoneEnemySpawner`** tạo encounter, kiểm tra cờ yêu cầu khi có lệnh spawn, theo dõi quái sống,
  EXP/loot và gọi `eventIdOnCleared` khi encounter được dọn sạch.
- **`ConnectedStoryDirector`** nối các lựa chọn đã lưu với tiến trình session: đặt cờ, hoàn tất
  session, cập nhật NPC nào được tương tác và chuyển mục tiêu. Đây là bộ nối của demo; không thay
  thế danh mục event trong `StoryEventDirector`.
- **`PlayerProgressionService`** lưu lựa chọn, cờ, session, trạng thái event, HP/vị trí và LastEvent.
  Continue phục hồi trạng thái trước, sau đó chỉ phát lại encounter còn sống hoặc dialogue còn
  chờ lựa chọn.

### Luồng event tiêu chuẩn

`Player chạm trigger` → `StoryEventTrigger(eventId)` → `StoryEventDirector` kiểm tra One Shot →
chạy `On Triggered` (bật/tắt object, đặt/xóa cờ, gọi spawner, mở dialogue) → ghi save.

Encounter được gọi như sau:

`StoryEventDirector` hoặc `ConnectedStoryDirector` gọi `spawner.Spawn()` →
`ZoneEnemySpawner` kiểm tra `Required Flag` → spawn →
Player hạ hết quái → `eventIdOnCleared` gọi event kết quả → event đặt cờ/bật object kế tiếp.

### Cờ không tự gọi event

- `Set Flag` chỉ lưu cờ. Nó **không tự gọi** `StoryEventTrigger` và không spawn ngay, trừ khi có
  một `ZoneEnemySpawner` bật `Spawn When Player Nearby` và cờ đó khớp `Required Flag`.
- `Required Flag` được kiểm tra trong `ZoneEnemySpawner.Spawn()`; thiếu cờ thì spawn bị từ chối
  (có cảnh báo trong Console).
- Muốn cờ mở đường cho người chơi đi tới rồi chạm vùng: bật GameObject trigger bằng `Activate
  Objects`, sau đó cấu hình trigger gọi event riêng.
- Muốn spawn ngay tại một event: đưa spawner vào `Spawn Spawners` của action. Nếu spawner có
  `Required Flag`, cờ phải được đặt trước lệnh spawn trong luồng đó.
- Nếu event vừa bật cờ vừa gọi spawner trong cùng `On Triggered`, action hiện chạy theo thứ tự
  bật/tắt object → đặt/xóa cờ → gọi spawner; vì vậy spawner có thể dùng cờ vừa được đặt.

## 2. Tạo event trong Inspector

1. Mở scene demo, chọn GameObject **Story Event Settings**.
2. Trong component **Story Event Director**, thêm phần tử vào `Events`.
3. Điền một `Event Id` duy nhất, ổn định và không đổi sau khi đã phát hành save.
4. Chọn `One Shot` cho event cốt truyện chỉ chạy một lần.
5. Gán `Dialogue Profile` nếu event mở hội thoại; để trống nếu chỉ làm gameplay.
6. Trong `On Triggered`, cấu hình các mảng `Activate Objects`, `Deactivate Objects`,
   `Spawn Spawners`, `Flags To Set`, `Flags To Remove`, `Item To Remove` hoặc
   `Readable Item To Open` khi cần.
7. Nếu có lựa chọn, thêm từng `Choice` với `Choice Id` khớp chính xác `DemoDialogueChoice.choiceId`.
   Action của choice chỉ chạy sau khi người chơi chọn.
8. Gọi event bằng collider `StoryEventTrigger` hoặc từ `Event Id On Cleared` của spawner.

Không dùng cùng một Event Id cho hai event. One Shot đã được lưu sẽ không tự chạy lại khi sửa
Inspector; dùng **New Game** hoặc xóa save thử nghiệm để kiểm tra từ đầu.

## 3. Tạo trigger chạm Player

1. Tạo GameObject tại vị trí người chơi cần đi qua; đặt đúng kích thước Collider2D.
2. Thêm `BoxCollider2D` (hoặc Collider2D phù hợp), bật `Is Trigger`.
3. Thêm `Story Event Trigger`.
4. Điền `Event Id` khớp với event trong **Story Event Settings**; có thể kéo Story Event Director
   vào field `Director`, nếu không script sẽ tự tìm trong scene.
5. Đảm bảo Player có tag `Player`, `PlayerController`, Collider2D và Rigidbody2D tương thích với
   physics 2D. Trigger có thể nằm trên object inactive; event trước đó có thể bật object này.
6. Không bật `Spawn When Player Nearby` cho cùng encounter nếu trigger/action đã gọi spawner. Tránh
   hai cơ chế cạnh tranh hoặc spawn ngoài ý muốn.

## 4. Luồng cốt truyện và cấu hình Session 1–5

Các tên cờ và Event Id bên dưới phân biệt chữ hoa/thường; giữ đúng chính tả trong Inspector,
dialogue profile và save.

### Session 1 — Tỉnh dậy trong máu (Nhà Hoang)

1. Player bắt đầu ở tầng trệt căn nhà hoang. Đàn chuột **chỉ spawn khi Player chạm trigger**
   `Session1RatsEncounter`; spawner `Spawn Zone - Chuột Nhà Hoang` không còn tự kích hoạt theo
   khoảng cách.
2. `Session1RatsEncounter.On Triggered` gọi spawner chuột. Lúc mới game không cần cờ gate cho
   spawner này.
3. Spawner chuột có `Event Id On Cleared = Session1RatsCleared`. Khi hạ hết 3 con, event này đặt
   `Session1Ready` và bật `Event Trigger - Khải tự thoại bên xác`.
4. Chạm trigger bên xác mở profile `CorpseDecision`; choices của session `1`:
   - `A`: chôn xác/âm thầm rời đi → `Session1RouteA`, mở đường cửa (`HouseExitOpen`).
   - `B`: điều tra xác → `Session1RouteB`, mở cầu thang (`AtticStairsOpen`).
5. `ConnectedStoryDirector` đọc choice đã lưu, đặt cờ và chuyển `currentSession` sang nhánh 2A
   hoặc 2B. Chỉ cho NPC Mira/đích đến đúng nhánh tương tác ở thời điểm tương ứng.

### Session 2A — Kẻ trốn chạy (Làng Ven Sông)

1. Nhánh này bắt đầu sau choice `1/A` và cờ `Session1RouteA`.
2. Spawner Chó Săn Ma có `Required Flag = Session1RouteA`; khi cờ được lưu và Player tới gần vùng,
   encounter mới spawn. Có thể thay proximity bằng trigger gọi event có `Spawn Spawners`, nhưng
   không bật đồng thời cả hai cách.
3. Khi hạ hết chó, `VillageHoundCleared` đặt `Session2AReady`.
4. Trưởng Làng Tứ chỉ được bật hội thoại sau điều kiện nhánh và encounter. Choice session `2A`:
   `A1` nói dối, `A2` thú nhận một phần; cả hai dẫn tới Session 3. ConnectedStoryDirector đánh dấu
   `Session3Unlocked` và hoàn tất Session 2A.

### Session 2B — Người điều tra (Gác Mái Nhà Hoang)

1. Nhánh này bắt đầu sau choice `1/B`, cờ `Session1RouteB` và `AtticStairsOpen`. Cửa/cầu thang và
   teleport scene dùng các cờ yêu cầu riêng; không gỡ gate trước khi nội dung tầng trên hoàn tất.
2. Rương tầng áp mái dùng điều kiện `AtticStairsOpen`; mở rương đặt `AtticChestOpened` và làm
   cuốn nhật ký có thể tương tác.
3. Event `DiaryFound` mở hội thoại/choice nhật ký. Choice `B1` đọc và giữ sách (`DiaryRead`), choice
   `B2` đốt và xóa vật phẩm (`DiaryBurned`). Action choice gọi spawner nhện; `ZoneEnemySpawner`
   không spawn trùng nếu một luồng khác cũng yêu cầu spawn.
4. Spawner nhện hoàn tất bằng `AtticSpidersCleared`. Chỉ sau khi đã có choice nhật ký và dọn sạch
   nhện, tiến trình đặt `Session2BReady`, `HouseExitOpen`, `Session3Unlocked`, hoàn tất Session 2B
   và chuyển sang Session 3.

### Session 3 — Gặp thầy trừ tà Hạo (Đồi Cao / Am Thờ)

1. Bắt đầu sau khi một trong hai nhánh Session 2 đặt `Session3Unlocked`.
2. Spawner quạ ở Am Thờ dùng `Required Flag = Session3Unlocked`; hạ hết quạ gọi
   `ShrineCrowsCleared`, đặt `Session3Ready`.
3. Khi session mở và encounter hoàn tất, profile Thầy Hạo được bật. Choice session `3`:
   - `A`: nhận bùa → `HaoTalismanAccepted`, mở `Session4AUnlocked`.
   - `B`: từ chối → `HaoTalismanRefused`, mở `Session4BUnlocked`.
4. Mỗi nhánh chỉ mở khu/encounter Session 4 tương ứng. Không bật cả hai encounter chỉ bằng vị trí;
   giữ `Required Flag` để ngăn spawn sai nhánh.

### Session 4A — Đêm bị khóa (Hành Lang Gương)

1. Chỉ mở khi `Session4AUnlocked` (nhánh nhận bùa).
2. Spawner Bóng Gương dùng `Required Flag = Session4AUnlocked`. Khi hạ hết, event
   `DreamShadesCleared` đặt `Session4AReady`.
3. Sau đó mở hội thoại Vô Diện. Choice session `4A`: `A` lắng nghe (`ListenedToVoDien`), `B` chống
   lại (`ResistedVoDien`). Cả hai mở `Session5Unlocked` và dẫn Session 5.

### Session 4B — Tự lực cánh sinh (Nhà Kho Đá)

1. Chỉ mở khi `Session4BUnlocked` (nhánh từ chối bùa).
2. Spawner Dơi Máu dùng `Required Flag = Session4BUnlocked`. Khi hạ hết, event
   `WarehouseBatsCleared` đặt `Session4BReady`.
3. Sau đó mở hội thoại Én. Choice session `4B`: `A` tự trói ở nơi hoang vắng; `B` nhờ bạn canh giữ
   và đánh dấu `EnAtRisk`. Cả hai mở `Session5Unlocked` và dẫn Session 5.

### Session 5 — Thị trấn Rệu Rã (Quảng Trường Trung Tâm)

1. Mở sau lựa chọn cuối của Session 4 (`Session5Unlocked`).
2. Hai encounter ở quảng trường (Xác Sống Thị Dân và Quạ Xác Thối) đều gate bằng
   `Session5Unlocked`.
3. Chỉ khi **cả hai** spawner đã clear, ConnectedStoryDirector đặt `Session5Ready`; khi đó mới bật
   profile Bà Tư Bán Thuốc.
4. Choice session `5`: `A` nhận lời bảo vệ thị trấn (`DefendedTown`), `B` âm thầm rời đi
   (`LeftTown`). Cả hai hoàn tất demo ở Session 5. Cốt truyện gốc đi tiếp sang Session 6A/6B, nhưng
   demo hiện chưa triển khai hai session đó.

## 5. Bảng Event Id và quan hệ kích hoạt

| Event Id | Nguồn kích hoạt | Kết quả/cờ chính |
|---|---|---|
| `Session1RatsEncounter` | Player chạm trigger tầng trệt | Gọi spawner chuột |
| `Session1RatsCleared` | `Event Id On Cleared` của spawner chuột | Đặt `Session1Ready`, bật trigger xác |
| `CorpseDecision` | Player chạm trigger xác | Mở dialogue; choice `1/A` hoặc `1/B` mở nhánh |
| `DiaryFound` | Tương tác với nhật ký sau khi mở rương | Choice đọc/đốt, gọi spawner nhện |
| `AtticSpidersCleared` | `Event Id On Cleared` của spawner nhện | Mở đường ra và Session 3 cho nhánh 2B |
| `VillageHoundCleared` | `Event Id On Cleared` của spawner Chó Săn Ma | Đặt `Session2AReady` |
| `ShrineCrowsCleared` | `Event Id On Cleared` của spawner quạ Am Thờ | Đặt `Session3Ready` |
| `DreamShadesCleared` | `Event Id On Cleared` của spawner Bóng Gương | Đặt `Session4AReady` |
| `WarehouseBatsCleared` | `Event Id On Cleared` của spawner Dơi Máu | Đặt `Session4BReady` |
| `PlazaUndeadCleared` | `Event Id On Cleared` của spawner xác sống | Báo encounter xác sống hoàn tất |
| `PlazaCrowsCleared` | `Event Id On Cleared` của spawner quạ xác thối | Báo encounter quạ hoàn tất |

Các cờ `Session1RouteA/B`, `Session3Unlocked`, `Session4AUnlocked`, `Session4BUnlocked`,
`Session5Unlocked` là điều kiện nhánh/spawn. `Session1Ready`, `Session2AReady`, `Session2BReady`,
`Session3Ready`, `Session4AReady`, `Session4BReady`, `Session5Ready` báo encounter hoàn tất hoặc
NPC/choice đã sẵn sàng. Không dùng cờ “Ready” thay cho cờ “Unlocked” nếu cốt truyện yêu cầu mở
nhánh trước khi spawn.

## 6. Cách lập bảng Excel cho event mới

Excel là tài liệu thiết kế; scene hiện **không tự đọc một bảng Excel event để sinh cấu hình Unity**.
Sau khi thống nhất nội dung trong Excel, phải cấu hình cùng Event Id ở Inspector, trigger, spawner
và dialogue profile.

Nên có các cột:

| Cột | Ý nghĩa |
|---|---|
| `EventId` | ID duy nhất; giữ nguyên trong save |
| `SessionId` | `1`, `2A`, `2B`, `3`, `4A`, `4B` hoặc `5` |
| `NarrativeBeat` | Mô tả beat theo file cốt truyện |
| `TriggerType` | `PlayerTouch`, `SpawnerCleared`, `DialogueChoice`, `ItemInteraction` |
| `TriggerObject` | Tên GameObject/trigger/spawner/profile |
| `RequiredFlags` | Tất cả cờ phải có trước khi chạy |
| `OneShot` | Có ghi hoàn tất và chặn chạy lặp không |
| `DialogueProfile` / `ChoiceId` | Profile mở hoặc lựa chọn mà action xử lý |
| `SpawnSpawners` | Các spawner cần gọi |
| `SetFlags` / `RemoveFlags` | Thay đổi tiến trình |
| `ActivateObjects` / `DeactivateObjects` | Collider, cổng, cầu thang, NPC profile... |
| `NextEvent` | Event tiếp theo hoặc event được gọi khi clear |
| `Save/Replay` | Còn pending khi thoát game thế nào; phần nào Continue được replay |

Với mỗi encounter, ghi riêng event bắt đầu và event kết thúc. Ví dụ:

| EventId | TriggerType | RequiredFlags | Action |
|---|---|---|---|
| `Session1RatsEncounter` | `PlayerTouch` | — | Spawn `Spawn Zone - Chuột Nhà Hoang` |
| `Session1RatsCleared` | `SpawnerCleared` | — | Set `Session1Ready`; activate trigger xác |

## 7. NPC, choice và cách tách hiển thị khỏi hội thoại

- Choice trong `DemoDialogueChoice` phải có `sessionId` khớp session hiện tại và `choiceId` khớp
  action trong Story Event Settings (nếu choice action do StoryEventDirector quản lý).
- ConnectedStoryDirector tra choice đã lưu và mới áp dụng chuyển session/cờ; không nhập thủ công
  cùng một chuyển session vào nhiều event khác nhau nếu không cần.
- ConnectedStoryDirector bật/tắt các profile NPC: xác sau khi chuột clear; Trưởng Làng Tứ ở nhánh
  2A sau khi chó clear; Hạo sau khi mở Session 3 và quạ clear; Vô Diện/Én sau encounter đúng nhánh;
  Bà Tư sau khi Session 5 mở và cả hai encounter clear.
- `Activate Objects`/`Deactivate Objects` tắt GameObject, không tắt riêng component. Nếu profile
  chung object với SpriteRenderer, tắt object sẽ ẩn cả hình; tách `DemoNpcDialogueProfile` thành
  child nếu cần khóa hội thoại mà vẫn giữ sprite.

## 8. Save, Continue và test

- Event mới lưu vào `LastEvent`; Continue khôi phục session/cờ/One Shot/object state trước khi chạy
  lại phần còn pending. Chỉ replay dialogue đang chờ choice hoặc encounter chưa clear.
- Quái replay không cấp lại EXP/loot. Nếu spawner còn quái sống thì Continue không spawn trùng.
- Save thủ công giữ nguyên LastEvent nhưng dùng HP/vị trí tại lúc Save. New Game xóa toàn bộ save.
- Save cũ không có LastEvent vẫn được đọc; Continue không replay event cũ.
- Sau khi đổi event/flag/spawner, kiểm tra save mới bằng **New Game**; save One Shot cũ có thể
  khiến trigger đúng thiết kế nhưng event không chạy lại.

Checklist trong Play Mode:

1. New Game: đàn chuột **không** spawn chỉ vì Player xuất hiện ở gần spawner; đi qua collider
   `Session1RatsEncounter` thì chúng mới spawn.
2. Hạ hết chuột: `Session1Ready` được lưu và trigger xác được bật.
3. Chọn A/B ở xác: đúng cửa/cầu thang và đúng session/flag nhánh được mở.
4. Chọn nhánh 2B, mở rương, đọc/đốt nhật ký: đúng item/flag và nhện chỉ spawn một lần.
5. Kiểm tra session 3, 4A hoặc 4B, rồi 5: spawner chỉ hoạt động khi cờ mở nhánh phù hợp; Session 5
   chỉ Ready sau khi cả xác sống lẫn quạ đã clear.
6. Thoát khi encounter đang pending rồi Continue: đúng spawner hồi lại; không nhận EXP/loot trùng.
7. Mở dialogue có choice rồi thoát trước khi chọn: Continue mở lại dialogue và cho chọn tiếp.

## 9. Xử lý lỗi cấu hình

- **Không spawn:** kiểm tra `Required Flag`, event đã gọi `Spawn`, enemy prefab và Console.
- **Spawn ngay khi vào vùng:** tắt `Spawn When Player Nearby` nếu encounter phải bắt đầu bằng
  collider/event.
- **Spawn hai lần:** không cấu hình proximity lẫn event spawn cho cùng spawner; kiểm tra script
  gameplay khác có gọi `Spawn()` không.
- **Clear event không chạy:** kiểm tra `Event Id On Cleared`, Story Event Director reference,
  Event Id trong danh sách, và bảo đảm spawnCount còn ít nhất một enemy hợp lệ.
- **Trigger không nhận Player:** kiểm tra `Is Trigger`, Player tag/controller/collider/Rigidbody2D,
  object active và event chưa One Shot complete trong save.
- **Đã đặt cờ nhưng chưa spawn:** `Set Flag` không gọi spawn trực tiếp; cần proximity spawner hợp
  lệ hoặc event/action gọi `Spawn Spawners`.
- **NPC/dialogue sai thời điểm:** kiểm tra session choice, flag gate và logic bật profile trong
  ConnectedStoryDirector; không tắt object chứa sprite khi chỉ muốn khóa hội thoại.
