# Đăng ký level, skill config và prefab enemy/boss — 08/11/2026

## Phạm vi và quyết định đã xác nhận

- Theo yêu cầu đăng ký LvConfig/SkillConfig từ BongNgu_GameDesign.xlsx; tạo lại prefab enemy/boss và cho enemy dùng cùng SkillActions.
- Người dùng chọn **He Thong Level** khi mốc mở khóa khác Cay Ky Nang. ATK của 6 boss, HP/ATK Thú Gù Tím và Thú Giáp Tinh Thể để gán Inspector; không tự đặt số.
- Giữ nguyên chưa commit theo lựa chọn trước. Workbook chỉ đọc trong lượt này; không thay ID/save schema/LastEventData/cờ truyện.

## Đã thực hiện

- `Resources/Lv Config.asset`: đủ 20 cấp, EXP/HP/ATK/DEF/ammo đúng Excel; lưu skillId. Lv20 mở toàn bộ 18 mục cây kỹ năng theo He Thong Level. Điều kiện riêng của skill truyện vẫn hiển thị trong SkillConfig; chưa tự gắn vào choice/session ID không được xác nhận.
- `Resources/SkillConfig.asset`: 27 mục player (24 mục cây/level và 3 loại đạn), có tên/nhánh/bậc/mô tả/action/context. Giữ BasicSlash/DodgeRoll và icon/legacy audio/animation variants đã có của BasicSlash. Chiêu dùng ACT_*; không thay skillId để chạy theo actionId.
- `Resources/EnemySkillConfig.asset`: 52 mục enemy/boss/summon. Tách khỏi danh sách player để tránh trang bị nhầm hoặc debug unlock chiêu quái.
- `Resources/EnemyCombatConfigs/`: 27 asset chứa HP/ATK, slot skill, trigger, phase, priority, range, cooldown condition và ghi chú thiết lập.
- `Prefabs/Enemies/`: 18 prefab quái (16 Database Quai + 2 mẫu Skill Enemy); `Prefabs/Bosses/`: 6 boss; `Prefabs/Enemies/Summons/`: 3 prefab sói con/bản sao. Các thư mục này chưa có prefab enemy/boss để ghi đè. Không tự đặt các prefab vào scene/spawner hay sửa drop/EXP.
- Mỗi prefab có HealthController, EnemyController, CharacterAnimation, EnemySkillController, EnemySkillScheduler, EnemyPhaseController, RuntimeStatModifierStore, SelfBuff, Rigidbody2D/collider, hurtbox tag HitBoxEnemy và DetectionZone/ZoneSee được gán sẵn. Chưa gán hình/JSON vì chưa có art enemy/boss đã xác nhận; health bar tắt để không tự sinh UI.
- `EnemySkillController`: resolve SkillConfig/context, aim target, gọi PlayAnimationSkill(skillId), khóa di chuyển trong lúc cast, cooldown theo AttackSpeed, save key legacy giữ nguyên; slot runtime clone riêng mỗi instance. Không tạo Detector GameObject lúc chạy. Enemy cũ không có scheduler vẫn poll collider theo cấu hình cũ.
- `EnemySkillScheduler`: chọn chiêu theo target của ZoneSee, range/min range, priority, initial delay, trigger vào combat/phase/chết và once-per-combat. Không cùng lúc spam nhiều chiêu. Buff có điều kiện không bị restart mỗi frame và bật lại được khi điều kiện tái xuất hiện.
- `EnemyPhaseController`: chuyển HP theo các ngưỡng đã có (Sói 40%, Vệ Binh 50%, Vô Diện 60%, Thầy Hạo 45%); phase chưa đủ điều kiện để manual/event. Hủy action/buff/defense cũ khi đổi phase/rời combat/chết.
- `RuntimeStatModifierStore.cs` tách khỏi SkillSystemCore để Unity gắn được MonoBehaviour đúng tên file vào prefab. Không đổi các index stat cũ; thêm StunResistance/DotResistance ở cuối. Kháng choáng 20%, độc/máu 30% dùng đường modifier thực; DOT gửi typed damage để phân biệt.
- Summon gán prefab cụ thể, truyền target và ATK từ chủ, giới hạn số còn sống không tính xác chết; bản sao không có skill tự nhân bản. ATK clone hiện nhận từ chủ; cần chỉnh thiết kế nếu muốn trần/giảm sát thương riêng. Sói con chưa có HP trong Excel: gán Max Health trong combat config trước khi test.
- `ConfiguredSkillAction`: hitbox melee lifetime=0 tồn tại tối thiểu một physics tick, tránh hitbox combo nằm mãi. `SkillActions/DefenseWindow`: gỡ cửa sổ giáp/phản của đúng action khi kết thúc combat/phase.
- Player controller báo rõ và chặn mục Needs Inspector Setup; passive chưa đủ adapter không được âm thầm áp sai modifier. `SkillConfigValidator` in ghi chú của các mục đang chờ. `EnemyCombatConfigEditor` hiển thị cảnh báo số liệu thiếu và cách gán hình/context.
- PlayerProgressionService hỗ trợ tên resource hiện có `Lv Config` khi scene chưa gán serialized reference, tránh nạp bảng mặc định không có unlock.

## Gán trong Inspector trước khi test

1. Kéo prefab vào scene hoặc trường enemyPrefab của spawner cần dùng. Scene phải có SkillActions đã gán SkillActionCatalog (4 scene đã được gán ở lượt trước).
2. Chọn prefab → EnemySkillController → Combat Config: gán **Attack** cho boss; **Max Health + Attack** cho hai quái thiếu chỉ số. Cờ Needs Attack/Health ghi nguồn Excel còn thiếu; tự được chấp nhận khi nhập giá trị dương, không cần đổi cờ.
3. Chọn CharacterAnimation → **Unity Json Asset** của đúng quái. Tên animation skill phải trùng skillId trong EnemySkillConfig. Ảnh đạn/skill cast/end nằm ở `defaultContext.effectAnimation`; charge đã nằm trong JSON nhân vật.
4. Chỉnh moveSpeed/stopDistance/collider/DetectionZone theo kích thước nhân vật thực. Các giá trị movement/collider cơ bản giữ kiểu cấu hình sẵn có, không phải số thiết kế đã chốt trong Excel. Tầm chiêu lấy từ recipe, cooldown không ghi giữ 0 và ghi chú để tự chỉnh theo animation.
5. Sói con: gán HP còn thiếu. Cắn Xé Liên Hoàn: gán wallMask đúng layer tường và tắt Needs Inspector Setup. Các summon hiện giữ radius/lifetime/count/HP fraction của catalog; prefab override ở EnemySkillConfig/modules/summon.
6. Người Gác Hang phase2 chưa có ngưỡng: gán healthBelow rồi tắt Manual Only, hoặc gọi SetPhase(2). Vô Diện phase3 cần cờ QT cao xuyên suốt game thật + dưới25%, hoặc event truyện gọi SetPhase(3); không tự coi QT hiện tại là QT toàn game.
7. Blind cần ScreenEffect/CanvasGroup trên target; QTE/Chuyển Nguyền cần ChallengeMeter/Input/UI và event kết quả truyện, drain/gain/duration/cờ miễn nhiễm. Các slot QTE chờ Needs Inspector Setup để tránh tự đoán cốt truyện. Replay Thầy Hạo vẫn cần nguồn lịch sử Session12 xuyên scene; replay hiện dùng lịch sử runtime có giới hạn của hệ sẵn có.
8. Những mục player chờ cấu hình/adapter bên dưới đã đăng ký nhưng chưa bật gameplay; đọc Setup Notes trước khi tắt Needs Inspector Setup. Không chỉ tắt cờ đối với mục chưa có consumer/adapter.

| SkillId player đang chờ | Việc còn thiếu |
| --- | --- |
| IronWill | Cần cờ LT cao xuyên suốt game và cấp cờ miễn nhiễm dùng 1 lần cho ChallengeMeter; Excel chưa nêu ngưỡng LT. |
| Rage | Gán Modules/Condition: HitCount, Required Count, Within Seconds; Excel chưa chốt số đòn tích nộ. |
| BorrowedPower | Cần nối lựa chọn A Session 7B; +15% CHỈ cận chiến và QT +1 mỗi 3 trận cần adapter theo vòng đời trận. |
| Symbiosis | Excel chưa xác định action/gameplay; cần cờ Ending Cộng Sinh và NG+. |
| WeaponUpgrade | Excel chưa có công thức nâng 1 bậc và bảng chỉ số vũ khí theo bậc. |
| MeleeCritical | Đã đăng ký +15% CriticalChance. Hệ damage hiện chưa roll chí mạng cận chiến; cần nối consumer trước khi bật passive. |
| GuardBreak | Cần cổng giới hạn 1 lần/trận; Excel chưa định nghĩa ranh giới trận, không cho spam phá khiên. |
| SilverMastery | Cần consumer phân loại đạn bạc/boss để cộng 25% đúng mục tiêu. |
| SurvivalInstinct | Cần metadata nhận đòn chí mạng để tự né 10%; không biến thành active né thủ công. |
| ShotOld | Cần gán loại vũ khí/đạn và nối tiêu hao đạn trước khi bật; không cho bắn miễn phí. |
| ShotCopper | Cần gán loại vũ khí/đạn và nối tiêu hao đạn trước khi bật; không cho bắn miễn phí. |
| ShotSilver | Cần gán loại vũ khí/đạn và nối tiêu hao đạn trước khi bật; không cho bắn miễn phí. |

- Hoàn Thiện Bản Thân là marker mở cây trong LvConfig, không cộng stat riêng. Đòn chí mạng, loại đạn/tiêu hao, nâng bậc vũ khí, điều kiện truyện và các cơ chế không có consumer chưa được coi là hoàn tất chỉ vì đã có entry.
- Quạ Xác Thối vẫn dùng làm mới DOT, chưa stack (đúng giới hạn đã ghi của module ở lượt trước). AI tàng hình/ánh sáng, tách NPC, cả nhóm bao vây, ẩn bản thể sau phân thân, save phase/QTE xuyên phiên, drop/EXP và snapshot replay xuyên scene không được thêm trong yêu cầu đăng ký prefab này.

## Kiểm tra

- Runtime + Editor assemblies biên dịch qua project Validation/SkillActions (không ghi vào Library/Temp/obj).
- Validation/EnemySkills/validate_assets.py đối chiếu 20 cấp, 79 skill, 27 prefab/component graphs, HP/ATK, phase/manual gate, link summon, GUID nội bộ và icon cũ. Material URP dùng GUID package được kế thừa từ Player có sẵn.
- Validation/EnemySkills/Checks.csproj: 35 kiểm tra source thật của controller/scheduler/phase qua Unity API test doubles; không phải Unity physics/Play Mode.
- Bộ kiểm tra catalog cũ vẫn được chạy để xác nhận model/timing không thay đổi.
- Chưa chạy Play Mode hoặc xác nhận render/physics/UI/QTE trong Unity Editor; cần reload rồi test theo các trường còn thiếu phía trên.

Commit: không tạo theo yêu cầu giữ nguyên chưa commit.
