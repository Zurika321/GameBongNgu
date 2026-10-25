GAME BÓNG NGỦ — HỆ THỐNG ĐỔI NGÔN NGỮ + ÂM LƯỢNG UNITY

============================================================
PHẦN A — LOCALIZATION / TRANSITION
============================================================

Cấu trúc:
Assets/
  Scripts/Localization/transition.cs
  Scripts/Localization/LocalizedText.cs
  Scripts/Localization/LanguageButton.cs
  transitions/vn.json
  transitions/en.json
  Resources/transitions/vn.json
  Resources/transitions/en.json

1. Cài đặt manager
- Ở scene khởi động đầu tiên, tạo GameObject root tên PersistentManagers (hoặc tên khác).
- Gắn component transition.cs vào GameObject này.
- Không tạo thêm một transition manager khác trong các scene sau. Script tự tồn tại khi scene chuyển.
- Nếu project dùng TextMeshPro, đảm bảo package TextMeshPro đã được cài (thường Unity đã có sẵn).

2. Đăng ký từng dòng UI cần đổi ngôn ngữ
- Chọn GameObject đang chứa component TextMeshProUGUI/TMP_Text hoặc UI > Text.
- Gắn LocalizedText.cs lên chính GameObject có chữ đó.
- Trong Inspector, nhập Translation Key, ví dụ: new_game.
- Không cần tự đăng ký bằng code. LocalizedText tự đăng ký khi bật và tự hủy đăng ký khi tắt.
- Có thể gắn vào chữ của nút, tiêu đề, nhãn setting, menu, thông báo tĩnh...

Ví dụ Assets/transitions/vn.json và Assets/Resources/transitions/vn.json:
{
  "new_game": "Trò chơi mới",
  "settings": "Cài đặt"
}

Ví dụ Assets/transitions/en.json và Assets/Resources/transitions/en.json:
{
  "new_game": "New Game",
  "settings": "Settings"
}

Mỗi key phải có trong từng file ngôn ngữ. Tên key là giống nhau; value là bản dịch tương ứng.
Sửa cả file trong Assets/transitions và bản sao trong Assets/Resources/transitions.
File Resources là phần cần thiết để JSON được đóng gói khi Build game.

3. Đổi ngôn ngữ từ bất kỳ script nào
transition.DoiNgonNgu("en");
transition.DoiNgonNgu("vn");
// Alias tiếng Anh:
transition.SetLanguage("en");

Ngay sau lời gọi, tất cả LocalizedText đang hoạt động trong mọi scene đang được load
(bao gồm scene additive) nhận sự kiện và cập nhật chữ. Ngôn ngữ được lưu bằng PlayerPrefs.
Nếu một scene đã bị unload, các object UI bên trong scene đó không còn tồn tại để cập nhật;
khi scene ấy được load lại, LocalizedText tự đọc ngôn ngữ hiện tại và hiển thị đúng ngay.

4. Dùng API dịch thủ công cho dialog hoặc text được tạo bằng code
string vietnamese = transition.dich("hi", "vn");
string english = transition.dich("hi", "en");

5. Nút chọn ngôn ngữ (không bắt buộc)
- Gắn LanguageButton.cs lên GameObject của nút.
- Đặt Language Code trong Inspector thành vn hoặc en.
- Trong Button > On Click(), kéo GameObject có LanguageButton vào rồi chọn
  LanguageButton.ChuyenNgonNgu().
- Nếu một script khác cần truyền mã ngôn ngữ, có thể gọi LanguageButton.ChuyenNgonNgu("en").

Lưu ý: LocalizedText dành cho chữ tĩnh/nhãn. Các dòng UI được script khác liên tục ghi đè text
thì script đó nên gọi transition.dich(...) hoặc LocalizedText.SetKey(...) khi nội dung thay đổi.

============================================================
PHẦN B — VOLUME MANAGER
============================================================

Các script đã cài vào Assets/Scripts/Audio:
- VolumeManager.cs
- VolumeSettingsUI.cs
- FootstepAudioPlayer.cs

Âm lượng lưu bằng PlayerPrefs: Master, Music, SFX và trạng thái mute từng kênh.
Manager tự khởi tạo khi game chạy, giữ nhạc nền qua các scene, và tự gắn FootstepAudioPlayer
lên object có Tag Player khi scene được tải. Không cần tạo AudioMixer hoặc kéo tham chiếu manager
vào Inspector.

Âm thanh runtime được đóng gói tại:
Assets/Resources/Volume/Music/nhac_bong_ngu.wav
Assets/Resources/Volume/SFX/tieng_di_bo.wav

Bản mẫu để quản lý trong ForAIAgentEdit:
Assets/ForAIAgentEdit/Volume/Music/nhac_bong_ngu.wav
Assets/ForAIAgentEdit/Volume/SFX/tieng_di_bo.wav

Menu demo tự nối thanh Master có sẵn và tạo hai thanh Music/SFX cùng kiểu trong Settings.
VolumeSettingsUI cũng có các hàm OnMasterVolumeChanged, OnMusicVolumeChanged,
OnSFXVolumeChanged và các hàm mute để nối với UI khác nếu cần.
