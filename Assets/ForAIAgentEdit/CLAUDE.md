# CLAUDE.md — Bóng Ngủ (Sleep Shade)

> File này giúp AI (Claude Code) hiểu cấu trúc và quy ước của dự án để hỗ trợ code,
> review, refactor mà không cần hỏi lại nhiều lần.
> File này nằm tại Assets/ForAIAgentEdit/CLAUDE.md.
> Cập nhật file này mỗi khi thêm hệ thống mới hoặc đổi cấu trúc thư mục.
> Tài liệu tham chiếu ở thư mục Assets được dẫn theo đường dẫn bắt đầu bằng ../: `../cot-truyen-bong-ngu.md` (cốt truyện,
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
đầy đủ 15 session, nhánh rẽ và 7 kết thúc: xem `../cot-truyen-bong-ngu.md` nếu file đã được bổ sung.

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
├── ForAIAgentEdit/                 # Tài liệu làm việc AI, tiến độ và kế hoạch cập nhật
│   ├── CLAUDE.md
│   ├── Tiến trình tạo code game mở rộng.md
│   ├── NewUpdate.txt
│   ├── NewUpdate2.txt
│   └── PromptCuocTroChuyenMoi.txt
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
│   ├── Registry/                   # Attribute và ScriptableObject tra cứu Flag/Item/Session
│   │   ├── RegistryIdAttribute.cs
│   │   └── GameIdRegistry.cs
│   ├── Debug/                      # Công cụ debug chỉ chạy trong Unity Editor
│   │   └── AdminEdit.cs            # F1: điều chỉnh save, scene, flags, inventory, event khi Play Mode
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
│   │   └── ConnectedStoryDirector.cs # Khôi phục vị trí theo zone, cập nhật zone và Cinemachine Confiner
│   ├── Utils/                      # Helper, extension methods
│   └── Editor/                     # CustomPropertyDrawer, registry và custom editor tools
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
│   │   ├── dialogue_localization_migrator.py  # Quét/migrate thoại; chạy --dry-run trước
│   │   └── vn*.json                            # Numbered localization patches for review/manual merge
│   └── Skills/
│
├── Resources/
│   └── GameIdRegistry.asset        # Registry IDs tra cứu trong Inspector
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

- `SessionDemoDirector.cs` đã bị loại bỏ sau khi xác nhận không có scene/prefab reference; script chỉ chứa comment.
- Trong lần rà soát gần nhất, `ZoneLocationTracker.cs` và `ZoneDemoDialogueUI.cs` không tồn tại; chức năng vị trí/confiner nằm trong `ConnectedStoryDirector`, dialogue runtime chính nằm trong `DemoNpcDialogueController` và `DemoNpcDialogueProfile`.
- `ConnectedStoryDirector` còn các khối story cũ đã comment. Chỉ dọn chúng khi đã xác nhận không còn flow cần dùng và không làm mất dữ liệu Inspector/scene.

## 8. Quy trình làm việc và trạng thái hiện tại (30/10/2026)

### Cách làm việc

- Trước khi kết luận hoặc sửa, đọc code, dữ liệu, scene/prefab và các tài liệu liên quan trong Assets/ForAIAgentEdit; quy tắc scene nằm ở Assets/AI_QuyTac.md.
- Phân biệt tài liệu kế hoạch với yêu cầu đang được người dùng giao. NewUpdate2.txt là lộ trình; thực hiện từng phần theo yêu cầu hiện tại, không tự làm toàn bộ lộ trình cùng lúc.
- Trả lời bằng tiếng Việt, ngắn gọn; thay đổi tối thiểu và giữ kiến trúc, gameplay, dữ liệu save cùng các reference Inspector hiện có.
- UI và GameObject phải được bố trí trong scene/prefab theo AI_QuyTac.md; không tự dựng UI/gameplay object lúc runtime.
- Giữ nguyên LastEventData, Choice ID, session ID và tiến trình truyện. Khi thay đổi schema save, phải xem xét tương thích và chuyển dữ liệu cũ có chủ đích.
- Khi báo cáo, nêu file đã sửa, lý do, cấu hình Inspector cần làm và điều gì đã hoặc chưa được kiểm tra. Không khẳng định đã compile hoặc chạy trong Unity nếu chưa làm.

### Trạng thái gần nhất

- Inventory đã có tab Vật phẩm/Kỹ năng; PlayerSaveData lưu các skill đã mở khóa riêng với tối đa ba skill đang trang bị và phím 1–9/Space. PlayerSkillController đồng bộ các skill đã trang bị. BasicSlash và DodgeRoll là hai skill có mapping hành động được ghi nhận.
- Phần skill Inventory này chưa được xác nhận compile hoặc chạy trong Unity Editor/Play Mode; đọc code hiện tại trước khi sửa.
- Việc tiếp theo được mô tả trong Assets/ForAIAgentEdit/NewUpdate2.txt: timing riêng từng frame animation; hai ô vũ khí và điều kiện vũ khí cho skill; tự gỡ skill khi điều kiện không còn đúng; SFX đăng ký theo skill trong SkillActions và được gọi từ code skill.
- Player Hub phải là UI đặt sẵn trong scene để chỉnh bằng Inspector, không được tạo khi mở Inventory. Hub hiển thị ba ô skill, hai ô vũ khí cùng icon và HP/ATK/DEF. Panel thông tin vật phẩm/skill tiếp tục mở khi click; trên PC, hover ô có vật phẩm/skill thì hiện panel và rời ô thì ẩn.
- Đọc NewUpdate2.txt trước khi triển khai các mục trên; giữ tương thích save cũ và không làm hỏng LastEventData.


## Animation / skill integration (2026-11-08)

- CharacterAnimation uses an exported Unity JSON TextAsset. CharacterAnimation_old.cs is an inactive archive.
- PlayAnimationSkill(skillId, holdCharge) plays the rig animation with the exact skillId; BeginSkillCast releases Charge into Cast. Idle/Run cannot interrupt it; Hurt/Stun/Die can.
- PlayerSkillController and PlayerBasicAttackController use the JSON rig for character animation. SkillActions only dispatches actionId to ISkill; it has no sprite or audio registration.
- SkillConfig defaultContext/weaponVariants.context.effectAnimation supplies Cast/flight and End effect sprites to each runtime SkillContext. Fireball splits flight/explosion using these fields. Four-direction packing is Down, Up, Left, Right.
- Animation SFX belongs to CharacterAnimation audioBindings and VolumeManager IDs. Legacy context audio fields remain hidden only to preserve old asset data.
- FootstepAudioPlayer and automatic Player attachment have been removed.
- GameIdRegistry skillIds is rebuilt from SkillActions registrations in serialized scenes/prefabs; SkillConfig actionId uses this dropdown. Unique skillId remains a text field and save IDs are unchanged.
- Compile validation covers runtime and Editor assemblies. Play Mode validation is still required.


## Tổ chức script Combat (08/11/2026)

- `Scripts/Combat/SkillBase/`: StaticDamageZone (trước là ArcAttackSkill), MovingHitbox (FlyAttackSkill), DotOnHit (DameDotEffect), DotZone (DameDotZone), SelfBuff (PlayerBuffController); các spawner/reporter/debug và module tái sử dụng mới.
- `Scripts/Combat/SkillEdit/`: SessionSkills, NormalAttackSkill, FireballAttackSkill, DashAttackSkill2, UtilitySkillActions. Đây là nơi viết action ghép từ module gốc.
- Đã giữ GUID khi chuyển/đổi tên. ActionId ArcAttack/FlyAttack và các ID save cũ vẫn giữ nguyên. Root Combat giữ controller, registry, context và kiểu dữ liệu chung.
- Các module mới: ApplyStatus, SummonEntity, CasterMove, ModifyResource, DefenseWindow, ScreenEffect, ChallengeMeter, ReplayAction, ConditionGate. Cấu hình mỗi skill nằm trong `SkillConfig.defaultContext.modules` hoặc `weaponVariants.context.modules` rồi truyền vào `SkillContext.Modules`.
- DOT tiếp tục dùng context.dotConfig; buff sẵn có dùng buffConfig/passiveConfig và SelfBuff. SelfBuffAction là adapter gọi component buff của caster; không dùng component registry làm nơi lưu buff cho mọi nhân vật.
- Chỉ thêm module registrations vào SkillActions trong CoreCombatDemo/Zone3/Zone4/Zone5, không xóa GameObject hay thay ID story/save. Module UI cần nối CanvasGroup/Input/UnityEvent bằng Inspector; không tự dựng UI.
- Sheet Script Hitbox của BongNgu_GameDesign.xlsx ghi đúng phạm vi đã hỗ trợ và phần còn thiếu. Các ACT_* vẫn là công thức thiết kế, chưa đồng nghĩa đã có đủ class action/gameplay trong SkillEdit.


## Đăng ký action từ Excel (08/11/2026)

- `Combat/SkillEdit/SkillActionCatalog.cs`: ScriptableObject và schema recipe (trigger, timing, số lần, geometry, %damage, module settings). `Resources/SkillActionCatalog.asset` chứa đủ 43 ACT_* / 66 bước từ sheet Skill Action. Không sửa workbook trong lượt này.
- `Combat/SkillEdit/ConfiguredSkillAction.cs`: chạy recipe bằng state riêng từng caster; hỗ trợ timeline, on-hit, projectile-end, parry-success và challenge-failure; có CancelAction/RemoveActionBuffs. Chặn vòng dependency và interval lặp bằng 0.
- `Combat/SkillEdit/SkillActionEventRelay.cs`: bridge Inspector cho event chết/phase/UI hoặc giữ nút; cấu hình actor/target/actionId/context, không tự tạo object scene.
- `Combat/SkillBase/SkillHitboxGeometry.cs`, `SkillActionDamage.cs`, `SkillActionEffectArea.cs`: cone collider, typed damage/armor penetration/guard flags và contact aura/slow. Damage phản tính theo HP thực nhận sau DEF.
- `Editor/SkillActionCatalogEditor.cs`: nút kiểm tra catalog trong Inspector. SkillActions ở CoreCombatDemo/Zone3/Zone4/Zone5 đã gán catalog; registry sync đọc action catalog được scene thực sự tham chiếu. Các action cũ giữ nguyên.
- `SkillContextConfig.actionDamageOverrides`: chỉnh %damage từng bước/nhát theo skill hoặc vũ khí; `breaksGuard`/`bypassesMeleeResistance` là cờ cấu hình. Các ID save và LastEventData không đổi.
- Bổ sung enum AttackSpeed ở cuối danh sách (giữ index cũ). Player đánh thường và enemy cooldown đọc modifier AttackSpeed; enemy có RuntimeStatModifierStore thì đọc buff ATK/MoveSpeed.
- Còn cần gán đúng prefab triệu hồi, SelfBuff/stat receiver của caster, ScreenEffect/ChallengeMeter receiver trên Target và event kết quả truyện; không tự chọn session/cờ lời nguyền hay tạo UI. Một số chi tiết chưa chốt như penalty né lệch, hit-stop, drop/EXP clone hoặc tắt bản thể cần adapter/event thiết kế.
- Sheet Excel còn ghi trạng thái cũ “chưa có action”; đã báo sai lệch, chưa tự sửa vì AI_RULES quy định chỉ đọc Excel ở lượt này.


## Đăng ký level và prefab enemy/boss (08/11/2026)

- Lv Config.asset đã nhập 20 cấp theo He Thong Level, skillId có trong SkillConfig. SkillConfig chứa27 skill player; EnemySkillConfig chứa52 skill riêng của enemy/boss/summon. BasicSlash và dữ liệu đã gán được giữ.
- Combat/EnemyCombatConfig.cs + Resources/EnemyCombatConfigs: dữ liệu HP/ATK, slot/trigger/phase/range/priority; chưa có số liệu để Inspector. EnemySkillController giữ cast/context/cooldown/save; EnemySkillScheduler chọn chiêu; EnemyPhaseController quản lý HP/manual/event phase.
- Prefabs/Enemies:18 quái; Prefabs/Bosses:6 boss; Enemies/Summons:3 mẫu triệu hồi không tự nhân bản. Gán sẵn component, hurtbox/ZoneSee/refs; chưa gán art/Unity JSON. Chưa đưa vào scene/spawner.
- RuntimeStatModifierStore tách thành file riêng để gắn prefab; index stat cũ giữ nguyên, thêm StunResistance/DotResistance. DOT/Status đi qua đường nhận kháng. Buff/giáp/action được hủy khi kết thúc combat/phase; summon nhận target/ATK từ chủ.
- Editor/EnemyCombatConfigEditor hiển thị trường thiếu. SkillConfigEntry.needsInspectorSetup/setupNotes ghi những cơ chế chưa đủ tham số/adapter; controller không thực thi các mục này. Các mục chỉ đăng ký chưa đồng nghĩa mọi gameplay GDD hoàn tất.
- Hướng dẫn đầy đủ, giới hạn và các trường cần gán: doc/2026-11-08_level-skill-prefab-enemy-boss.md. Không sửa workbook, không commit.


## Ghi yêu cầu GroupBlock (08/11/2026)

- ToDo/NewUpdate4.txt: prompt triển khai sau cho GroupBlock trong BoneAnimation_Unity.html, export ảnh/Unity JSON đủ biến thể và dropdown Inspector/API CharacterAnimation. Chỉ ghi yêu cầu, chưa sửa web/C#; chờ người dùng tiếp tục khi có token.


## GroupBlock - xac minh ban nguoi dung cap nhat (08/11/2026)

- NewUpdate4 da duoc nguoi dung thay bang bao cao trien khai. HTML va hai file CharacterAnimation runtime/Editor hien co GroupBlock, schema groupBlocks/defaultVariant/variants.
- Tao HTML rieng cung thu muc web: BoneAnimation_Unity_GroupBlock_NewUpdate4.html, sao nguyen ban hien tai; giu ban goc.
- JavaScript syntax va C# runtime/Editor build da qua. Chua kiem tra truc tiep Unity Play Mode/Inspector trong luot nay. Mapping weaponId va live Edit Mode preview con lai.


## 08/11/2026 — Làm lại giao diện GroupBlock và sửa kéo thả

- Bản web dùng tiếp: BoneAnimationStudio/BoneAnimation_Unity_GroupBlock_NewUpdate4.html; giữ BoneAnimation_Unity.html gốc.
- Đầu tab hiển thị từng hàng: bên trái group-<tên group>, bên phải dropdown biến thể/None. Bỏ dropdown chọn group và dropdown biến thể trùng ở phần chỉnh sửa.
- Phía dưới hiển thị từng group và từng biến thể thành khung riêng, có ô thả block và nút Thêm block đang chọn. Có thể thả vào bộ chưa được chọn/đang ẩn, kể cả khi preview là None.
- Sửa nguyên nhân kéo thả: Skeleton effectAllowed=move nhưng ô nhận cũ yêu cầu dropEffect=copy. Bản mới thống nhất move, gửi thêm MIME application/x-bone-block, giữ text/plain fallback, dọn trạng thái khi dragend.
- Kéo block sang bộ khác chuyển liên kết độc quyền sang bộ đó; không xóa block thật. Có Undo, báo trạng thái ngay trong tab, từ chối Bone/ảnh làm block.
- Schema groupBlocks/defaultVariant/variants/blockIds giữ nguyên; không sửa Unity C#, scene, prefab hoặc JSON animation.
- Kiểm tra: node --check và 18 kiểm tra Chrome headless đã qua, gồm native drag từ Skeleton vào hai ô nhận, None, group độc lập, chuyển liên kết/Undo, đổi tên, save/load và Unity payload giữ block ẩn. Không cài thêm thư viện.
- Chưa commit theo lựa chọn của người dùng.


## 08/11/2026 — Tên animation skill và vũ khí inventory

- Animation/CharacterAnimation.Bindings.cs: alias tên custom animation lưu trên component, khóa AnimState/Default/Death; Inspector sửa tên với Undo/prefab override. JSON/SFX nguồn giữ nguyên.
- SkillContextConfig.animationName / SkillContext.AnimationName tách tên hình ảnh khỏi skillId. Basic attack dùng Attack khi JSON không có tên skill riêng; charge/cast/wait dùng cùng tên.
- PlayerProgressionService.Skills nối equip/unequip, mất đồ và load/đổi scene tới API GroupBlock và refresh đòn đánh thường. Tự nhận group theo tên biến thể ItemDefinition, có Weapon Group/Weapon Variant Bindings trong Inspector; gỡ đồ chọn None, không ảnh hưởng nhóm khác.
- Kiểm tra: runtime/Editor compile; 37 kiểm tra animation binding/equipment và 36 kiểm tra enemy bằng production source với Unity API doubles. Chưa chạy Unity Play Mode.
- Chi tiết và hướng dẫn: doc/2026-11-08_animation-skill-name-inventory-weapon.md. Không commit theo lựa chọn của người dùng.


## 08/11/2026 — Ảnh xem trước cho Block trong web

- BoneAnimationStudio/BoneAnimation_Unity.html: Properties hiện ảnh cho 4 hướng và nhãn fallback; Block Keyframe hiện ảnh đang chọn và lưới thumbnail bấm chọn, gồm quay về ảnh gốc.
- Giữ dropdown/luồng thư viện và schema JSON. node --check + 12 kiểm tra Chrome headless đã qua. Ctrl+F5 để nhận giao diện mới.
- Chi tiết: doc/2026-11-08_web-block-image-preview.md. Không commit.


## 08/11/2026 — Hoàn tất triển khai 7 mục NewUpdate3

- Inventory icon fallback + tên, card skill với close/key/unequip; CoreCombatDemo có nút THÔNG SỐ và PlayerStatusHUD đặt sẵn. HUD đọc đủ chỉ số runtime và LvConfig, không tự dựng object.
- EnemyCombatConfig chứa bảng loot nhập 22 quái/boss Excel; spawner scene nhận config; 20 ItemDefinition thiếu và Registry IDs được bổ sung. Prefabs/Spawer có 22 prefab dùng lại enemy/config/pickup hiện có, count mặc định 1 do Excel chưa chốt encounter. EXP cũ giữ mức thực tế, chỉnh được qua Inspector.
- CharacterAnimation loop thường phát lại SFX. NewUpdate3.txt đã ghi trạng thái từng mục, trường chưa có dữ liệu và hướng dẫn test.
- Build runtime/Editor, 26 logic checks + 1762 asset/reference assertions; chưa Play Mode. Chi tiết: doc/2026-11-08_newupdate3-loi-vat.md. Chưa commit.


## 08/11/2026 — Event Registry theo Zone

- RegistryIdType có constant Event.Zone1…Zone5 và Event.Scene; RegistryIdKind giữ index cũ, thêm loại event. GameIdRegistry.Events.cs lưu 5 nhóm scene/ID/director nhận sync.
- GameIdRegistryEditor có nút đọc ID và thêm event rỗng theo zone; giữ event cũ/không nhân đôi, hỗ trợ Undo trên scene đang mở và save/close scene mở additive. Zone1 có 2 director, mặc định nhận tại Story Event Settings và xét ID ở cả hai.
- Trigger/rương/spawner/definition dùng dropdown Event.Scene; prefab chưa có scene phân nhánh 5 zone. Asset Registry đã nhập 20 ID đang dùng, không sửa ID caller hoặc scene gameplay.
- Build runtime/Editor + 27 checks với API doubles; chưa thao tác tool trong Unity thật. Hướng dẫn: doc/2026-11-08_event-registry-theo-zone.md. Chưa commit.


## 08/11/2026 — Sửa Die phát lại

- CharacterAnimation.Play giữ frame cuối của Die/Death đã hoàn tất khi controller tiếp tục yêu cầu cùng state, trừ restart=true. JSON Die.loop=false được giữ nguyên; player/enemy dùng chung được sửa.
- Build runtime/Editor và 9 regression checks với API doubles đã qua; chưa Play Mode. Chi tiết: doc/2026-11-08_fix-die-animation-replay.md. Chưa commit.


## 08/11/2026 — Cập nhật lệnh AdminEdit

- AdminEdit dùng SkillConfig/ItemDefinition/LvConfig hiện tại; menu F1 giữ nguyên. PlayerProgressionService.Debug.cs là API Editor-only để level/unlock/lock đồng bộ controller/passive, loadout và save.
- Events chọn mọi director theo scene, dùng getter/reset API thay reflection private; Reset chặn khi có hội thoại. Inventory equip đi qua service/GroupBlock.
- Numeric fields chỉ ghi khi edit, tránh buffer cũ đè +EXP/HP. Pause/UI được trả đúng trạng thái trước mở, DemoPauseMenu chặn input khi Admin đang mở.
- Build runtime/Editor + 26 API-double checks đã qua; chưa click thử trong Unity. Chi tiết doc/2026-11-08_adminedit-cap-nhat-lenh.md. Không sửa scene/save thật, không commit.
