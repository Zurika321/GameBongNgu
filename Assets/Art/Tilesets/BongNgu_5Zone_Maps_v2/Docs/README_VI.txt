BÓNG NGỦ — 5 ZONE (BỘ ẢNH RIÊNG)

NỘI DUNG
- Base/: 5 map nền riêng, mỗi ảnh 1536x1024 px, không chữ/nhãn phân vùng.
- Annotated/: 5 ảnh tương ứng có vùng màu, tên địa điểm, hướng kết nối và rìa map/collider dự kiến.
- OverlayLayers/: 5 lớp RGBA trong suốt chỉ chứa chú thích/vùng phân màu để ghép đè lên map nền.
- Preview/: ảnh tổng hợp thu nhỏ để xem nhanh. Chỉ ảnh preview bị thu nhỏ; ảnh gốc không bị thu nhỏ.
- Docs/Map_Metadata.json: tên khu vực, hướng kết nối và tọa độ gợi ý theo pixel/ô 16x16.

KÍCH THƯỚC
- Từng map: 1536x1024 px.
- Nếu lấy 16x16 px làm đơn vị tham chiếu: tương đương 96x64 tile.
- Không hiểu ảnh nền là tileset production: đây là map tham khảo địa hình. Các chi tiết vẽ không đảm bảo snap hoàn toàn vào từng tile 16x16; muốn dựng game chính xác nên tái tạo địa hình bằng Tilemap riêng.

KẾT NỐI THEO SHEET “So Do Zone”
Zone 1: Nhà Hoang -> Làng Ven Sông -> Đồi Cao (Am Thờ) -> Zone 2 / Nhà Kho Đá.
Zone 2: Nhà Kho Đá -> Quảng Trường Trung Tâm -> Quán Trọ Ven Sông và Con Đường Rừng Sương -> Zone 3 / Ngôi Đền Cổ.
Zone 3: Ngôi Đền Cổ -> Hang Động Ngầm; nhánh Xưởng Rèn Sườn Núi và Bìa Rừng Cháy; nối Zone 4 / Đỉnh Đồi.
Zone 4: Đỉnh Đồi -> Đại Sảnh Nghi Lễ; nhánh Không Gian Tiềm Thức khi ngủ -> Session 11; sau trận cuối nối Zone 5.
Zone 5: Quảng Trường Hậu Chiến -> Hầm Mộ Bí Mật; có đường ngầm bí mật về Đồi Cao (Zone 1) theo điều kiện Session 15.

LƯU Ý COLLIDER
- Viền đỏ/cam là gợi ý rìa ngoài để đặt EdgeCollider2D/PolygonCollider2D, không phải dữ liệu collider tự động.
- Mép nước, vách đá, khe vực vẫn cần collider riêng theo địa hình thực tế.
- Các cổng là ký hiệu định vị tương đối. Hãy chỉnh Transform spawn/destination trong Unity để hai scene nối đúng lối đi.
- Với PPU = 16, một tile 16 px tương đương 1 Unity Unit. Coordinates trong Map_Metadata.json là gợi ý từ góc trên-trái của ảnh; cần kiểm tra quy ước trục Y của project trước khi nhập.
