using UnityEngine;

/// <summary>
/// Ai đang cast skill - dùng để tự suy ra tag mục tiêu (HitBoxEnemy / HitBoxPlayer).
/// </summary>
public enum SkillCasterType
{
    Player,
    Enemy
}

/// <summary>
/// Gói toàn bộ dữ liệu cần thiết cho 1 lần thực thi skill.
/// Player hay Enemy đều dùng chung struct này, chỉ khác giá trị truyền vào.
/// Thêm biến mới ở đây nếu sau này skill cần thêm dữ liệu.
/// </summary>
[System.Serializable]
public class SkillContext
{
    [Header("Người cast")]
    public GameObject Caster;
    public SkillCasterType CasterType;

    [Header("Vị trí & hướng")]
    public Vector2 Position;        // vị trí gốc phát skill (thường = transform.position của caster)
    public Vector2 Direction;       // hướng đánh, dạng (x,y) - 1 trong 8 hướng đã chuẩn hoá (vd (1,0), (1,1).normalized...)
    public Vector2 TargetPosition;  // vị trí cụ thể cần bay tới (dùng cho skill vòng cung bay đến 1 điểm)

    [Header("Thông số chiến đấu")]
    public float Damage;
    public Vector2 Size;            // kích thước hitbox / zone (width, height) - zone tròn thì dùng Size.x làm bán kính
    public float Range;             // khoảng cách offset hitbox so với caster (arc trước mặt, khoảng lướt của dash...)
    public float Speed;             // tốc độ bay của đạn / tốc độ lướt
    public float Duration;          // thời gian tồn tại của hitbox / hiệu ứng
    [Min(0f)] public float StunTime; // thời gian khóa và làm choáng mục tiêu khi skill trúng đòn; 0 = chỉ Hurt

    [Header("Tuỳ chọn nâng cao")]
    public string OverrideTargetTag; // để trống thì tự suy ra theo CasterType, set tay nếu muốn ép tag khác

    [Header("Debug")]
    public bool Debug;         // true = skill hiện hitbox debug (LineRenderer) trực tiếp trong Game

    [Header("Sprite animation của Player/Enemy")]
    public Sprite[] CharacterAnimationSprites; // frames animation của Player/Enemy khi skill được cast

    [Header("Tuỳ chọn xuyên (dùng cho skill đạn bay)")]
    public bool CanPierce = false; // true = xuyên qua mục tiêu, false = dính là dừng+xoá

    [Header("DOT (Damage over Time)")]
    public DameDotConfig DotConfig; // để trống hoặc ActivationType = None thì skill không có DOT

    // Gọi lại khi đạn va vào 1 collider (KHÔNG tính tag "Skill" - luôn bị bỏ qua hoàn toàn).
    // Tham số: GameObject vừa va chạm, vị trí va chạm (world position lúc va).
    public System.Action<GameObject, Vector2> OnHitCallback;

    /// <summary>
    /// Tag mục tiêu mà hitbox của skill này sẽ kiểm tra va chạm.
    /// Player đánh -> tìm "HitBoxEnemy". Enemy đánh -> tìm "HitBoxPlayer".
    /// </summary>
    public string ResolveTargetTag()
    {
        if (!string.IsNullOrEmpty(OverrideTargetTag))
            return OverrideTargetTag;

        return CasterType == SkillCasterType.Player ? "HitBoxEnemy" : "HitBoxPlayer";
    }

    /// <summary>
    /// Ép Direction về đúng 1 trong 8 hướng chuẩn (0/45/90/.../315 độ).
    /// Gọi hàm này nếu input hướng đến từ joystick/raw input chưa được snap.
    /// </summary>
    public static Vector2 SnapTo8Directions(Vector2 rawDir)
    {
        if (rawDir == Vector2.zero) return Vector2.zero;
        float angle = Mathf.Atan2(rawDir.y, rawDir.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad = snapped * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }
}