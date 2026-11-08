# GroupBlock: bố cục theo Inspector và sửa kéo thả


## 08/11/2026 — Làm lại giao diện GroupBlock và sửa kéo thả

- Bản web dùng tiếp: BoneAnimationStudio/BoneAnimation_Unity_GroupBlock_NewUpdate4.html; giữ BoneAnimation_Unity.html gốc.
- Đầu tab hiển thị từng hàng: bên trái group-<tên group>, bên phải dropdown biến thể/None. Bỏ dropdown chọn group và dropdown biến thể trùng ở phần chỉnh sửa.
- Phía dưới hiển thị từng group và từng biến thể thành khung riêng, có ô thả block và nút Thêm block đang chọn. Có thể thả vào bộ chưa được chọn/đang ẩn, kể cả khi preview là None.
- Sửa nguyên nhân kéo thả: Skeleton effectAllowed=move nhưng ô nhận cũ yêu cầu dropEffect=copy. Bản mới thống nhất move, gửi thêm MIME application/x-bone-block, giữ text/plain fallback, dọn trạng thái khi dragend.
- Kéo block sang bộ khác chuyển liên kết độc quyền sang bộ đó; không xóa block thật. Có Undo, báo trạng thái ngay trong tab, từ chối Bone/ảnh làm block.
- Schema groupBlocks/defaultVariant/variants/blockIds giữ nguyên; không sửa Unity C#, scene, prefab hoặc JSON animation.
- Kiểm tra: node --check và 18 kiểm tra Chrome headless đã qua, gồm native drag từ Skeleton vào hai ô nhận, None, group độc lập, chuyển liên kết/Undo, đổi tên, save/load và Unity payload giữ block ẩn. Không cài thêm thư viện.
- Chưa commit theo lựa chọn của người dùng.

Kiểm tra lưu ở Validation/GroupBlockWeb/browser-check.cjs, browser-result.json và groupblock-redesign.png. Bản trước khi sửa được lưu tại Validation/GroupBlockWeb/before-redesign.html; script sửa chỉ ghi bản HTML riêng.

Cách dùng: mở bản HTML riêng → GroupBlock → tạo group → thêm biến thể → thả block vào ô của biến thể. Dropdown trên cùng dùng để đổi bộ đang xem. Không cần gán thêm Inspector Unity trong lượt này.
