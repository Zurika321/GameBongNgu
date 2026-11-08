using UnityEngine;

/// <summary>
/// Cách DOT được kích hoạt cho 1 skill.
/// None = không dùng DOT. Immediate = áp DOT ngay lên mục tiêu vừa bị hitbox thường trúng (không cần zone).
/// Zone = tạo ra 1 vùng tồn tại theo thời gian, ai đứng trong đó (đúng tag) sẽ bị áp DOT.
/// </summary>
public enum DotActivationType
{
    None,
    Immediate,
    Zone
}

/// <summary>
/// Hình dạng vùng DOT (chỉ áp dụng khi ActivationType = Zone).
/// </summary>
public enum DotZoneShape
{
    Box,
    Circle
}

/// <summary>
/// Toàn bộ thông số của 1 hiệu ứng DOT (Damage over Time) - admin chỉnh trực tiếp trong Inspector
/// (nằm bên trong PlayerSkillSlot / EnemySkillSlot), rồi được gán vào SkillContext.DotConfig khi skill chạy.
/// </summary>
[System.Serializable]
public class DameDotConfig
{
    [Header("Loại kích hoạt")]
    public DotActivationType activationType = DotActivationType.None;

    [Header("Thông số DOT áp lên mục tiêu")]
    [Tooltip("DOT kéo dài bao nhiêu giây trên mục tiêu kể từ lúc dính (hoặc kể từ lần dính gần nhất nếu bị refresh).")]
    public float dotDuration = 3f;
    [Tooltip("Khoảng thời gian (giây) giữa mỗi lần gây dame (mỗi lần tick).")]
    public float tickInterval = 1f;
    [Tooltip("Dame gây ra mỗi lần tick.")]
    public float damagePerTick = 1f;

    [Header("Thông số Zone (chỉ dùng khi ActivationType = Zone)")]
    public DotZoneShape zoneShape = DotZoneShape.Circle;
    [Tooltip("Kích thước vùng DOT. Circle: chỉ dùng zoneSize.x làm bán kính.")]
    public Vector2 zoneSize = new Vector2(2f, 2f);
    [Tooltip("Zone tồn tại bao nhiêu giây trước khi tự huỷ (khác với dotDuration - đó là thời gian DOT dính TRÊN mục tiêu).")]
    public float zoneLifetime = 5f;
}
