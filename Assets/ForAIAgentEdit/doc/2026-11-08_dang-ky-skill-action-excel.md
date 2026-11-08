# Đăng ký Skill Action từ Excel — 08/11/2026

Yêu cầu trực tiếp của người dùng: sửa SkillActions, tạo và đăng ký các action trong Excel. Người dùng đã đồng ý cấu hình recipe qua Inspector, xác nhận lưu scene và chọn giữ trạng thái chưa commit.

## Thay đổi

- Thêm SkillActionCatalog/ConfiguredSkillAction/SkillActionEventRelay trong SkillEdit và SkillActionCatalog.asset (43 action, 66 bước).
- Thêm SkillHitboxGeometry/SkillActionDamage/SkillActionEffectArea; mở rộng geometry cone, callbacks defense/QTE và damage provenance, armor penetration, guard flags.
- SkillActions đăng ký catalog cùng action cũ; registry đồng bộ ID từ catalog đang được scene tham chiếu. Chỉ thêm field catalog vào CoreCombatDemo/Zone3/Zone4/Zone5 và thêm ID registry.
- Context/Input/Enemy gửi mục tiêu, held-input, damage overrides và guard flags; AttackSpeed thêm cuối enum để giữ dữ liệu cũ.
- Cập nhật CLAUDE.md và tiến trình. Không sửa AI_RULES, ToDo, workbook, save schema, LastEventData, Choice/session ID.

## Inspector

- SkillConfig: chọn ACT_* ở actionId; skillId khớp tên animation JSON. %damage riêng theo skill/vũ khí: Context → Action Damage Overrides (stepIndex tính từ 0).
- SkillActionCatalog.asset: chỉnh timing, số lần, geometry, %damage và module settings; đọc setupNotes từng recipe. Có nút Validate.
- Summon: gán prefab ở Modules/Summon hoặc recipe. Caster dùng buff phải có SelfBuff + stat receiver phù hợp. Không tự gắn các component này lên enemy chưa được chỉ định.
- Screen/QTE: gán receiver trong scene trên Target và nối UI/Input/event thắng-thua. Tham số/nhánh truyện chưa có không được tự bịa.
- SkillActionEventRelay: gán actor/target/actionId/context; CastOnDeath cho tự nổ, EndPhase cho gỡ buff phase, BeginHold/EndHold cho channel. Player channel đặt ChargeTime=0 để bắt đầu lúc giữ nút.
- Reload scene sau khi Unity import để nhận catalog mới. Không thay thế các cấu hình người dùng vừa lưu.

## Kiểm tra

- Runtime/Editor compile thành công qua project Validation riêng; 24 kiểm tra catalog/timing/trigger và trường hợp cấu hình sai đã qua.
- Đối chiếu đủ 43 ID với Excel; kiểm tra references catalog trong 4 scene và registry.
- Chưa mở/chạy Unity; chưa xác nhận physics, hiệu ứng hay UI thực tế.
- Excel vẫn còn nhãn trạng thái cũ; không sửa workbook ở lượt này.

## Commit

Chưa tạo commit theo lựa chọn của người dùng. Giữ nguyên cả các thay đổi chưa commit từ trước; không git add/push/reset.
