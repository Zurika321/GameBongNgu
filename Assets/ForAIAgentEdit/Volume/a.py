import math
import random
import struct
import wave


def save_wav(filename, samples, sample_rate=44100):
    """Lưu danh sách mẫu âm thanh (float từ -1.0 đến 1.0) thành file WAV 16-bit."""
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono (tối ưu cho SFX 2D/3D trong game)
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(sample_rate)

        # Chuyển đổi float sang 16-bit PCM integer
        packed_data = bytearray()
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            packed_data.extend(struct.pack('<h', int(clamped * 32767)))
        wav_file.writeframes(packed_data)
    print(f"Đã tạo thành công: {filename}")


def generate_bong_ngu_bgm(filename="nhac_bong_ngu.wav", sample_rate=44100):
    """Tạo nhạc nền Bóng Ngủ: Giai điệu Music Box ma mị trên nền Pad trầm."""
    # Giai điệu vòng lặp (Tần số Hz, Thời lượng giây) - Âm giai La thứ (A minor)
    melody = [
        (440.00, 0.5),
        (523.25, 0.5),
        (659.25, 0.5),
        (587.33, 0.5),  # A4, C5, E5, D5
        (523.25, 0.5),
        (493.88, 0.5),
        (440.00, 1.0),  # C5, B4, A4
        (329.63, 0.5),
        (392.00, 0.5),
        (440.00, 0.5),
        (523.25, 0.5),  # E4, G4, A4, C5
        (493.88, 0.5),
        (392.00, 0.5),
        (329.63, 1.0),  # B4, G4, E4
    ] * 2  # Lặp lại 2 lần (tổng 16 giây)

    samples = []
    global_time = 0.0

    for freq, duration in melody:
        num_samples = int(duration * sample_rate)
        for i in range(num_samples):
            t = i / sample_rate

            # 1. Tiếng Music Box / Chuông (Tắt dần theo hàm mũ)
            envelope = math.exp(-t * 4.5)
            bell = math.sin(2 * math.pi * freq * t) * 0.35
            bell += (
                math.sin(2 * math.pi * (freq * 2) * t)
                * 0.12
                * math.exp(-t * 8.0)
            )  # Âm bồi

            # 2. Tiếng Ambient Drone Pad (Nền trầm liên tục tạo không khí mộng mị)
            # Sử dụng nốt A2 (110Hz) và E3 (164.81Hz) kết hợp hiệu ứng LFO nhẹ
            lfo = 0.85 + 0.15 * math.sin(2 * math.pi * 0.4 * global_time)
            pad = (
                math.sin(2 * math.pi * 110.0 * global_time) * 0.15
                + math.sin(2 * math.pi * 164.81 * global_time) * 0.1
            ) * lfo

            samples.append((bell * envelope) + pad)
            global_time += 1.0 / sample_rate

    save_wav(filename, samples, sample_rate)


def generate_footstep_sfx(filename="tieng_di_bo.wav", sample_rate=44100):
    """Tạo hiệu ứng tiếng bước chân đơn lẻ (One-shot SFX) để dễ lập trình trong game."""
    duration = 0.25  # 250ms cho 1 bước chân dứt khoát
    num_samples = int(duration * sample_rate)
    samples = []

    # Bộ lọc thông thấp (Low-pass filter) đơn giản để làm ấm tiếng ồn trắng
    last_out = 0.0
    alpha = 0.18  # Chỉnh nhỏ hơn (vd: 0.08) nếu muốn tiếng bước chân trên sàn gỗ/thảm trầm hơn

    for i in range(num_samples):
        t = i / sample_rate

        # Thành phần 1: Tiếng ma sát (Filtered White Noise)
        white_noise = random.uniform(-1.0, 1.0)
        filtered_noise = last_out + alpha * (white_noise - last_out)
        last_out = filtered_noise
        noise_env = math.exp(-t * 28.0)

        # Thành phần 2: Tiếng chạm gót/mũi chân xuống sàn (Low Pitch Drop Thump)
        thump_freq = 110.0 * math.exp(-t * 35.0) + 35.0
        thump = math.sin(2 * math.pi * thump_freq * t)
        thump_env = math.exp(-t * 45.0)

        # Trộn 2 thành phần
        sample = (filtered_noise * noise_env * 0.65) + (
            thump * thump_env * 0.85
        )
        samples.append(sample)

    save_wav(filename, samples, sample_rate)


if __name__ == "__main__":
    generate_bong_ngu_bgm()
    generate_footstep_sfx()