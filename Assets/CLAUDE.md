# CLAUDE.md — Bóng Ngủ (Sleep Shade)

> File này giúp AI (Claude Code) hiểu cấu trúc và quy ước của dự án để hỗ trợ code,
> review, refactor mà không cần hỏi lại nhiều lần.
> Cập nhật file này mỗi khi thêm hệ thống mới hoặc đổi cấu trúc thư mục.
> Tài liệu tham chiếu dự kiến nằm cùng thư mục Assets: `cot-truyen-bong-ngu.md` (cốt truyện,
> 15 session, các kết thúc) và `BongNgu_GameDesign.xlsx` (GDD chi tiết: bảng level,
> database quái/boss/item/NPC, cây kỹ năng, sơ đồ zone). Nếu thiếu các file này, hãy bổ sung
> trước khi dựa vào chúng để triển khai nội dung.

## 1. Tổng quan dự án

- **Tên game**: Bóng Ngủ (Sleep Shade)
- **Thể loại**: Kinh dị tâm lý 2D, góc nhìn top-down, hành động sinh tồn, nhiều kết thúc theo lựa chọn
- **Engine**: Unity — công cụ chỉnh sửa/edit chính là Unity Editor, code bằng VS Code
- **Render Pipeline**: (điền khi xác định — URP / Built-in)
- **Độ phân giải mục tiêu**: Full HD 1920x1080 (16:9), Camera orthographic
- **Nền tảng**: PC trước tiên (mobile/khác tính sau nếu có)
- **UI**: Đã có menu, pause menu, dialogue UI và HUD demo. Kiểm tra scene references trước khi thay
  UI hiện có; LT/QT có thể hiển thị trong công cụ demo, không tự xem đó là UI gameplay cuối.

### Tóm tắt cốt truyện

Nhân vật chính **Khải** — thợ săn quái vật lang thang — mắc "Lời Nguyền Song Miên": mỗi khi
ngủ, con quỷ **Vô Diện** chiếm xác anh, để lại ký ức đứt đoạn và máu khi tỉnh dậy. Chi tiết
đầy đủ 15 session, nhánh rẽ và 7 kết thúc: xem `cot-truyen-bong-ngu.md` nếu file đã được bổ sung.

### Cơ chế cốt lõi (2 chỉ số song song)

- **Lý Trí (LT)**: tăng khi giữ nhân tính, giảm khi chứng kiến/gây kinh hoàng.
- **Quỷ Tính (QT)**: tăng khi để Vô Diện kiểm soát/dùng sức mạnh quỷ, giảm khi kháng cự.
- Tổ hợp LT/QT tích lũy quyết định lựa chọn khả dụng và 1 trong 7 kết thúc.

### Chiến đấu

- **Cận chiến** là chủ lực: dao găm → rìu → kiếm khắc quỷ (chế tạo)... nâng cấp qua hệ thống
  lên cấp (EXP từ giết quái + hoàn thành sự kiện cốt truyện, Lv1–20, xem sheet `He Thong Level`).
- **Tầm xa** (nỏ/súng lục cũ dùng Băng Đạn Cũ/Đồng/Bạc) rất mạnh nhưng đạn cực hiếm
  (giới hạn 1–3 viên/khu vực), buộc người chơi cân nhắc thời điểm dùng — đặc biệt để phá
  khiên boss (vd Vệ Binh Đá Cổ) hoặc trong Phase 2 trận cuối Vô Diện.
- 3 nhánh kỹ năng độc lập nhưng lên song song được: **Cận Chiến (Công)**, **Sinh Tồn (Thủ)**,
  **Quỷ Tính (Rủi Ro)** — chi tiết đầy đủ trong sheet `Cay Ky Nang`.

## 2. Cấu trúc thư mục (Assets/)

```
Assets/
├── CLAUDE.md
├── UNITY_SETUP_GUIDE.md            # Hướng dẫn tạo scene, GameObject, tags, collider và gán script trong Unity
├── Scripts/
│   ├── Controller/                # Điều khiển Player/Enemy + quản lý state + interface damage/health
│   │   ├── PlayerController.cs
│   │   ├── EnemyController.cs
│   │   ├── IFacingProvider.cs
│   │   ├── IDamage.cs             # TakeDamage(float damage, GameObject source)
│   │   ├── IHealth.cs             # Health state + invincibility
│   │   └── HealthController.cs    # Generic implementation of IDamage + IHealth
│   │
│   ├── Animation/                 # Script animation — kéo thả Sprites, chỉnh frame/khoảng cách
│   │   ├── CharacterAnimation.cs  # Dùng chung cho Player/Enemy; states include Hurt/Stun
│   │   ├── ItemAnimation.cs
│   │
│   ├── Core/                       # GameManager, SceneLoader, Singleton pattern
│   ├── Combat/                     # Skill, hitbox, damage calculation
│   │   ├── ArcAttackSkill.cs
│   │   ├── DameDotConfig.cs
│   │   ├── DameDotEffect.cs
│   │   ├── DameDotZone.cs
│   │   ├── DashAttackSkill2.cs
│   │   ├── FlyAttackSkill.cs
│   │   ├── FireballAttackSkill.cs
│   │   ├── EnemySkillController.cs
│   │   ├── EnemySkillDetector.cs
│   │   ├── NormalAttackSkill.cs
│   │   ├── PlayerSkillController.cs
│   │   ├── HitboxDebugDrawer.cs
│   │   ├── HitboxReporter.cs
│   │   ├── HitboxShape.cs
│   │   ├── ISkill.cs
│   │   ├── ProjectileHitboxReporter.cs
│   │   ├── ProjectileHitboxSpawner.cs
│   │   ├── SkillHitbox.cs
│   │   ├── SkillActions.cs         # GameObject registry: skillName + skillSprites cấu hình riêng từng skill
│   │   ├── SkillContext.cs         # Chứa CharacterAnimationSprites của Player/Enemy, không chứa sprite riêng skill
│   │   ├── ZoneHitboxSpawner.cs
│   │   └── SessionSkills.cs        # Chém cơ bản, né nhanh và kỹ năng quái Session 1–2
│   ├── AI/                         # Enemy behaviour, pathfinding, aggro
│   │   ├── ZoneSee.cs
│   │   └── ZoneEnemySpawner.cs     # Spawn theo vùng/session, EXP và drop
│   ├── Dialogue/                   # DemoDialogueAsset: ScriptableObject thoại theo session
│   ├── Narrative/                  # StoryEventDirector: event, dialogue, spawner và replay khi Continue
│   │   ├── StoryEventDirector.cs
│   │   └── StoryEventTrigger.cs
│   ├── Inventory/                  # Item, túi đồ, vật phẩm cốt truyện/nghi lễ
│   │   ├── WorldItemPickup.cs      # Pickup trong scene, tỉ lệ xuất hiện và cờ đã nhặt
│   │   └── InventoryStorage.cs     # Rương cất/lấy vật phẩm theo container ID
│   ├── Interaction/                # Interactable base class cho object tương tác trong world
│   ├── Leveling/                   # EXP, Lv1-20, mở khóa kỹ năng theo Cay Ky Nang
│   ├── UI/                         # Menu, hội thoại và gameplay UI
│   │   └── MainMenuController.cs   # New Game / Continue, mở scene đã lưu
│   ├── Audio/                      # AudioManager, SFX/BGM
│   ├── SaveLoad/                   # Serialize dữ liệu, save slot
│   │   ├── GameData.cs             # PlayerSaveData, LastEvent và snapshot cốt truyện
│   │   ├── PlayerProgressionService.cs # SaveData, EXP/level, LT/QT, inventory và rương
│   │   ├── ConnectedStoryDirector.cs # Khôi phục vị trí theo zone, cập nhật zone và Cinemachine Confiner
│   │   └── SessionDemoDirector.cs  # Script session 1–2 còn trong project; kiểm tra references trước khi xóa
│   ├── Utils/                      # Helper, extension methods
│   └── Editor/                     # Custom editor tools (không build vào game)
│
├── cot-truyen-bong-ngu.md          # Tài liệu dự kiến; bổ sung nếu chưa có
├── BongNgu_GameDesign.xlsx         # Tài liệu dự kiến; bổ sung nếu chưa có
│
├── Prefabs/
│   ├── Player/
│   ├── Enemies/                    # Theo Zone 1-5 (xem So Do Zone)
│   ├── Bosses/
│   ├── Interactables/
│   ├── Items/
│   └── Effects/
│
├── Scenes/
│   ├── Boot/
│   ├── MainMenu/
│   ├── Zones/                      # Zone1_RungThong, Zone2_ThiTran, Zone3_DenCo, Zone4_DaiSanh, Zone5_HamMo
│   └── Test/
│
├── Art/
│   ├── Sprites/
│   │   ├── Characters/             # Khải, Vô Diện, NPC (Nhi, Thầy Hạo, Én...)
│   │   ├── Enemies/
│   │   ├── Environment/
│   │   └── Effects/
│   ├── Tilesets/
│   └── Animations/
│
├── Audio/
│   ├── Music/
│   └── SFX/
│
├── Data/                           # ScriptableObject: khớp với Database Item/Quai/Boss/NPC trong xlsx
│   ├── Items/
│   ├── Enemies/
│   ├── Bosses/
│   ├── Dialogues/
│   └── Skills/
│
└── Settings/                       # Input System asset, URP settings, Tag/Layer config
```

## 3. Hệ thống Controller — quy ước bắt buộc

### State Machine (Player & Enemy dùng chung logic)

Mỗi Controller (Player/Enemy) quản lý một tập state, ví dụ:
`Idle, Move, Attack, UseSkill, Locked, Invincible, Die`

- **Move**: gọi hàm `Move()` → đổi state sang `Move` → gửi state + tốc độ hiện tại
  (editable field, vd `moveSpeed`) sang script Animation tương ứng để chạy đúng animation.
- **Locked** (đang khóa di chuyển): kích hoạt khi đang thực hiện skill/đòn đánh không cho
  phép di chuyển — controller chặn input Move cho tới khi skill/đòn kết thúc thì trả lại
  quyền di chuyển (unlock).
- **Invincible**: cờ tạm thời bỏ qua `TakeDamage` (dùng cho i-frame né đòn, hoặc buff).
- **Die**: khi `HealthController` báo HP <= 0 → controller chuyển state `Die` → gọi Animation
  script chạy animation chết, khóa mọi input.

### Interface bắt buộc

- **`IDamage`**: mọi entity nhận sát thương phải implement.

  ```csharp
  public interface IDamage
  {
      void TakeDamage(float damage, GameObject source);
  }
  ```

  `source` là GameObject gây sát thương (dùng để biết nguồn attack — player, enemy, bẫy...).

- **`IHealth`**: quản lý máu, sinh ra thanh máu đơn giản hiển thị phía trên đầu Player/Enemy.
  Khi `TakeDamage` làm HP về 0, tự động cập nhật và báo cho Controller để chuyển state `Die`
  → gọi animation die tương ứng.

### Luồng liên kết giữa các script

```
Input → PlayerController (đổi state, gọi Move()/Attack()/UseSkill())
      → gửi state + tốc độ sang PlayerAnimation (chọn đúng frame theo state)
      → nếu trúng đòn: IDamage.TakeDamage() trên HealthController
      → HealthController cập nhật máu, nếu <=0 báo Controller chuyển state Die
      → PlayerAnimation chạy animation Die
```

Animation script (`PlayerAnimation.cs`, `EnemyAnimation.cs`...) chỉ chịu trách nhiệm:

- Giữ tham chiếu Sprites (kéo thả/chỉnh sửa trong Inspector)
- Chạy đúng frame theo state nhận được từ Controller
- Không tự ý đổi state — chỉ Controller mới đổi state

## 4. Quy ước code

- Namespace theo thư mục: `Game.Controller`, `Game.Animation`, `Game.Combat`, `Game.Narrative`, ...
- Đặt tên: PascalCase cho class/method, camelCase cho biến private (`_moveSpeed` nếu cần
  phân biệt với property public `MoveSpeed`)
- Ưu tiên ScriptableObject cho dữ liệu tĩnh (item, quái, boss, skill — khớp với các sheet
  trong `BongNgu_GameDesign.xlsx`) để cân bằng số liệu không cần sửa code
- Interface cho hệ thống mở rộng: `IDamage`, `IHealth`, `IInteractable`, `ISaveable`
- Tốc độ di chuyển, sát thương, HP... luôn để field public/serialized để chỉnh trong Inspector,
  không hardcode trong logic

## 5. Ghi chú kỹ thuật

- Camera: Orthographic, canvas/scale theo Full HD 1920x1080
- Input: (điền khi xác định — Input System mới hay Input Manager cũ)
- Save data format: JSON qua `DataManager`, ghi tại `Application.persistentDataPath/Data/Player.json`;
  data thiết kế gốc được đọc từ `Resources/Data/*.json`.
- UI/menu/dialogue đã có bản demo; kiểm tra canvas và scene trước khi thay đổi, và không coi bản demo là UI cuối.

## 6. Dữ liệu tham chiếu nhanh (từ BongNgu_GameDesign.xlsx)

| Sheet              | Nội dung                                                                              |
| ------------------ | ------------------------------------------------------------------------------------- |
| `Chi Tiet Session` | 15 session: bối cảnh, nhiệm vụ, quái/boss, NPC, item rơi, Lv đề xuất, ảnh hưởng LT/QT |
| `He Thong Level`   | EXP, HP/ATK/DEF theo Lv1-20, kỹ năng mở khóa tương ứng session                        |
| `Database Quai`    | Quái thường theo 5 Zone, độ khó, skill, điểm yếu, item rơi                            |
| `Database Boss`    | Boss theo session, HP, số phase, skill từng phase, cách khắc chế                      |
| `Database Item`    | Toàn bộ vật phẩm, độ hiếm, tỉ lệ rơi, nguồn rơi, công dụng                            |
| `Database NPC`     | NPC, vị trí trong map, session xuất hiện, lời thoại theo giai đoạn                    |
| `Cay Ky Nang`      | 3 nhánh kỹ năng (Cận Chiến/Sinh Tồn/Quỷ Tính), điều kiện mở khóa, hiệu ứng            |
| `So Do Zone`       | 5 Zone, địa điểm, kết nối giữa các địa điểm, quái/NPC tại từng nơi                    |

---

### Việc cần bổ sung tiếp (điền khi có quyết định):

1. Unity version + Render Pipeline (URP hay Built-in)
2. Input System đang dùng (Input System mới / Input Manager cũ)
3. Save/Load format
4. Style đồ họa (pixel art / vector / hand-drawn) — ảnh hưởng cách setup Animation script

## 7. Ghi chú hiện trạng và quyết định dự án (ưu tiên khi bắt đầu phiên mới)

### Tài liệu cốt truyện

- Đọc cả `Bóng Ngủ — Sơ đồ Event & Cốt truyện chi tiết.pdf` và `BongNgu_GameDesign.xlsx` trước khi tạo/cập nhật nội dung session.
- PDF có các chỉnh sửa flow/event chi tiết hơn GDD, gồm session `1.5`, Choice ID, required flag, điều kiện phân nhánh và ending. Khi hai tài liệu không khớp, giữ nguyên các mã/flow đang dùng trong `PlayerProgressionService` và kiểm tra phần cập nhật tương ứng trong PDF; không tự gộp nhánh.
- Dùng đúng Choice ID và session ID vì save data lưu `currentSession` và `ChoiceRecord`; thay tên ID có thể làm save/Continue hoặc lựa chọn bị lệch.

### Scene và công cụ build

- Scene hiện có: `Assets/Scenes/Zones/CoreCombatDemo.unity`, `Zone1.unity` đến `Zone5.unity`, và `Assets/Scenes/MainMenu/MainMenu.unity`.
- `CoreCombatDemo` là scene chính kết chứa các gameobject chung như player hay setting hay data do người dùng tự chỉnh. **Không đưa scene này vào `QuickCombatDemoSetup`, không build lại, không ghi đè hoặc sửa scene này trừ khi người dùng yêu cầu rõ.**
- `QuickCombatDemoSetup` chỉ dành cho cập nhật Zone 1–5 qua menu `Tools/Game Setup/Update Demo Zones 1-5`. Khi sửa tool, cập nhật scene hiện có, giữ cấu trúc `Zone1Main`…`Zone5Main`/`MapBounous` và tránh tạo nội dung trùng lặp.
- Chỉ thêm scene vào Build Settings khi phù hợp với pipeline scene hiện hành; không tự tạo scene mới thay cho scene đã có.

### Save, chuyển scene, vị trí và camera

- `PlayerSaveData` lưu `currentScene`, `currentZone`, `currentSession`, `playerX` và `playerY`. Chuyển scene qua `FlaggedScenePortal` phải giữ kiểm tra `requiredFlag` và gọi `PlayerProgressionService.LoadScene(scene, zone, arrivalPosition)` để lưu đích đến trước khi chuyển.
- `MainMenuButton` là nút UI riêng để về `MainMenu`; gán `MainMenuButton.GoToMainMenu()` trong Button `On Click`.
- `ConnectedStoryDirector` hiện nhận mảng `{ currentZone, startTransform }`, đặt player theo `currentZone` lúc load (không tìm thấy thì dùng `(0,0,0)`), cập nhật zone từ collider con của `MapBounous`, và gán `PolygonCollider2D` tương ứng vào `CinemachineConfiner2D`.
- Tên vùng trong save, `MapBounous` và `ZoneSpawnPoint.currentZone` phải khớp. Khi đổi tên zone, cập nhật cả ba nơi và kiểm tra vị trí teleport/collider.

### Dialogue theo session

- `DemoDialogueAsset` là `ScriptableObject`; dữ liệu nằm dưới `Assets/Data/Dialogues/Session_<ID>/`. `DemoNpcDialogueProfile` ưu tiên trường `Dialogue Asset`, còn mảng inline `dialogue` chỉ là fallback cho dữ liệu cũ.
- `DemoDialogueAsset.PostDialogueText` là câu NPC nói lại sau khi người chơi đã ghi nhận một lựa chọn trong dialogue đó; nếu để trống, controller dùng lời đáp cũ theo choice.
- `DemoNpcDialogueProfile.InteractionRange` điều khiển riêng khoảng cách hiện prompt E cho từng NPC; `DemoNpcDialogueController` chọn NPC gần nhất trong phạm vi cấu hình của NPC đó.
- Tạo/cập nhật asset từ menu `Tools/Dialogue/Create Session Dialogue Assets` (editor script `Scripts/Editor/DemoDialogueAssetBuilder.cs`). Gán asset đúng session vào `DemoNpcDialogueProfile`; tên NPC lấy từ asset nếu asset có tên.
- Nội dung hiện tại được soạn từ PDF/GDD nhưng asset không tự gán vào mọi NPC/scene. Sau khi build hoặc cập nhật scene, kiểm tra lại trường `Dialogue Asset` trên NPC cần dùng.
- Các lựa chọn điều kiện dùng `requiredFlag`; trước khi thêm flag, xác minh tên flag và điểm set trong flow PDF/`PlayerProgressionService`. Đảm bảo session có lựa chọn vẫn tương thích với bộ lọc current session trong `DemoNpcDialogueController`.

### Trạng thái script cần xác minh trước khi xóa/thay thế

- `SessionDemoDirector.cs` vẫn còn trong project; đừng ghi trong tài liệu hoặc giả định rằng nó đã bị xóa. Kiểm tra scene/prefab references trước khi loại bỏ.
- Trong lần rà soát gần nhất, `ZoneLocationTracker.cs` và `ZoneDemoDialogueUI.cs` không tồn tại; chức năng vị trí/confiner nằm trong `ConnectedStoryDirector`, dialogue runtime chính nằm trong `DemoNpcDialogueController` và `DemoNpcDialogueProfile`.
- `ConnectedStoryDirector` còn các khối story cũ đã comment. Chỉ dọn chúng khi đã xác nhận không còn flow cần dùng và không làm mất dữ liệu Inspector/scene.
