# Module tạo skill tái sử dụng

`SkillBase` chứa cơ chế gốc; `../SkillEdit` chứa action cụ thể ghép các cơ chế đó. Giữ nguyên actionId khi chỉ đổi tên class/file.

Cấu hình mặc định và theo vũ khí nằm ở SkillConfig → Default Context / Weapon Variants → Modules. Độc dùng Dot Config đã có; BuffAction dùng buffConfig và SelfBuff trên caster. Module SelfBuff có thêm cấu hình Modules → Self Buff để dùng từ action ghép.

- ApplyStatus: gọi Apply(target, config) từ callback trúng đòn; stun/slow có hiệu lực, curse hiện là trạng thái có ID để truy vấn. Không tự áp mọi status mặc định lên mọi đòn.
- SummonEntity: gán prefab trong Modules → Summon. Prefab tự mang AI/phe/tag/collider; OnSummoned dùng cho tùy chỉnh thêm. Giới hạn đếm theo caster.
- CasterMove: dùng Speed/Duration/Direction trong context và cửa sổ bất tử trong Modules → Movement. DodgeRoll đã gọi lại module này.
- ModifyResource: Target trống thì dùng Caster. Hút máu phải gọi Apply với context.DamageDealt đã được hitbox cập nhật. LT/QT không tạo Choice; Persist chỉ lưu khi bật.
- DefenseWindow: bảo vệ Target/Caster; HealthController lọc damage trước khi trừ HP. Chưa phân loại damage cận chiến/đạn bạc.
- ScreenEffect: gán CanvasGroup đã có trong scene hoặc nối OnStrengthChanged tới shader/light adapter. Không tạo UI. Có Target thì tìm receiver ScreenEffect ở Target.
- ChallengeMeter: nối nút/Input Action có sẵn vào AddInput hoặc AddProgress; nối OnProgress tới UI, OnSucceeded/OnFailed tới event truyện. Không tự chuyển Session 13C.
- ReplayAction: SkillActions lưu lịch sử giới hạn; ClearHistory khi bắt đầu session nếu muốn lịch sử tách theo session.
- ConditionGate: Evaluate(context, config) hoặc action branch pass/fail; điều kiện HitCount/DamageTakenCount lấy từ SkillCombatEvents.

SkillContext.Target và DamageDealt chỉ dùng trong runtime; không sửa schema save hay LastEventData. Xem cột “Chưa hỗ trợ / cần nối thêm” trong Excel trước khi áp công thức ACT_*.

Đã compile runtime/Editor; cần test thêm trong Unity Play Mode và gán UI/prefab/event thực tế bằng Inspector.

Kiểm tra bổ sung: 18 kiểm tra logic chạy trên mã nguồn module với Unity API giả lập đã qua (slow/refresh/hết hạn, điều kiện, phòng thủ, QTE, hút máu, replay). Bộ kiểm tra nằm trong Temp/CodexSkillModuleChecks; không thay thế kiểm thử physics/scene/UI trong Unity.
