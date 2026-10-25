import os
import numpy as np
from PIL import Image

def remove_background(target_color, tolerance=30):
    """
    Tự động quét thư mục hiện tại chứa file .py, loại bỏ màu nền chỉ định 
    và lưu trực tiếp vào chính thư mục đó với tiền tố 'a_'.
    """
    # Lấy đường dẫn thư mục hiện tại chứa file python
    current_dir = r"D:\UnitiProject\My project\Assets\Art\edit"
    # current_dir = os.path.dirname(os.path.abspath(__file__)) if '__file__' in locals() else os.getcwd()

    target = np.array(target_color, dtype=np.int16)

    count = 0
    for filename in os.listdir(current_dir):
        # Chỉ quét các file PNG và bỏ qua các file đã được xử lý (có sẵn tiền tố 'a_')
        if filename.lower().endswith('.png') and not filename.startswith('a_'):
            input_path = os.path.join(current_dir, filename)
            
            try:
                # Mở ảnh và chuyển sang RGBA
                img = Image.open(input_path).convert("RGBA")
                arr = np.array(img, dtype=np.int16)
                
                # Tách kênh màu RGB và alpha
                rgb = arr[:, :, :3]
                alpha = arr[:, :, 3]
                
                # Tính độ chênh lệch màu so với màu mục tiêu
                diff = np.abs(rgb - target)
                distance = np.sum(diff, axis=-1)
                
                # Xác định các pixel thỏa mãn điều kiện tolerance
                mask = distance <= tolerance
                
                # Đặt alpha = 0 (trong suốt) cho các pixel nền
                arr[:, :, 3] = np.where(mask, 0, alpha)
                
                # Lưu ảnh kết quả ngay tại thư mục hiện tại với tiền tố 'a_'
                output_img = Image.fromarray(arr.astype(np.uint8), "RGBA")
                output_filename = f"a_{filename}"
                output_path = os.path.join(current_dir, output_filename)
                
                output_img.save(output_path, "PNG")
                print(f"Đã xử lý: {filename} -> {output_filename}")
                count += 1
            except Exception as e:
                print(f"Lỗi khi xử lý {filename}: {e}")

    print(f"\nHoàn tất! Đã xử lý thành công {count} ảnh.")

if __name__ == "__main__":
    # Cấu hình màu sắc cần xóa
    TARGET_COLOR = [255, 255, 255]  # Mã màu RGB cần xóa (Ví dụ: [255, 255, 255] là màu trắng)
    TOLERANCE = 30                  # Độ dung sai màu sắc

    remove_background(TARGET_COLOR, TOLERANCE)