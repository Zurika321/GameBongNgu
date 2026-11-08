# Sửa Die phát lại sau khi kết thúc — 08/11/2026

- JSON player hiện tại có Die.loop=false. PlayerController.FixedUpdate và EnemyController đều gọi Play(AnimState.Die) lặp lại khi IsDead.
- Nguyên nhân trong CharacterAnimation.Play(string): chỉ chặn phát lại khi isPlaying=true. Kết thúc one-shot đặt isPlaying=false nên yêu cầu Die tiếp theo khởi động từ đầu và phát lại SFX.
- Sửa riêng trường hợp animation Die/Death đã hoàn tất: khi restart=false và vẫn cùng animation, giữ pose/frame cuối. Không đổi JSON, controller, scene/prefab hoặc hành vi replay Hurt/skill khác. Play(..., restart:true) vẫn phát lại; chuyển Idle khi hồi sinh rồi chết lần nữa vẫn phát bình thường.
- Build runtime + Editor thành công. Validation/NewUpdate3/DeathChecks.csproj có 9 kiểm tra production Play/Update với API doubles: hoàn tất/giữ frame cuối qua 60 yêu cầu, SFX không lặp, restart rõ ràng, hồi sinh/chết lại, alias Death, Hurt và Idle không đổi hành vi.
- Chưa thử Unity Play Mode. Không cần gán thêm Inspector. Chưa commit theo lựa chọn của người dùng.
