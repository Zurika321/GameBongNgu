# Ảnh xem trước Block Properties / Block Keyframe — 08/11/2026

Yêu cầu trực tiếp: sửa BoneAnimation_Unity.html để ảnh trong Block Properties và Block Keyframe nhìn thấy được, không chỉ hiện tên.

- Đã cập nhật đúng file web C:/Users/NAM VO/OneDrive/1text code/link2/link/BoneAnimationStudio/BoneAnimation_Unity.html theo yêu cầu. Bản GroupBlock_NewUpdate4 không bị ghi đè.
- Block Properties: ảnh xem trước cho Down/Up/Left/Right, nền caro nhìn độ trong suốt, tên đầy đủ xuống dòng; hướng chưa gán báo dùng ảnh Down. Nút Chọn/đổi ảnh vẫn mở thư viện hiện có.
- Block Keyframe: ảnh đang chọn hiển thị ngay dưới ô thay ảnh; thêm mục Chọn ảnh bằng hình với lưới thumbnail có tên và ảnh gốc theo hướng. Bấm thumbnail cập nhật đúng block/frame; chọn ảnh gốc xóa override. Dropdown cũ vẫn hoạt động và cập nhật preview.
- Ảnh giữ tỉ lệ, hiển thị pixelated, lazy loading; thiếu dữ liệu có thông báo. Không đổi schema export/project hoặc logic animation.
- Kiểm tra: node --check thành công; 12 kiểm tra Chrome headless đã qua (4 hướng/fallback, tên chứa dấu nháy, keyframe mặc định/thay thế/reset, dropdown handler, placeholder và không có uncaught error). File áp dụng có SHA256 trùng bản đã kiểm tra.
- Validation/GroupBlockWeb/add-image-previews.py, preview-check.cjs và các bản HTML/JS phục vụ kiểm tra; before-image-previews.html giữ bản trước sửa.
- Người dùng mở lại BoneAnimation_Unity.html hoặc Ctrl+F5, chọn Block → Properties; trong Animation thêm/chọn Block Keyframe để xem ảnh và dùng lưới chọn ảnh.
- Không có thay đổi Inspector/scene/Unity runtime trong lượt này. Chưa commit theo lựa chọn của người dùng.
