using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Hình dạng vùng phát hiện cho từng skill - admin chọn Box hoặc Circle trong Inspector.
/// </summary>
public enum DetectionShape
{
    Box,
    Circle
}

/// <summary>
/// 1 dòng trong mảng skill của Enemy. Admin chỉnh trực tiếp trong Inspector, không cần sửa code.
/// </summary>
[System.Serializable]
public class EnemySkillSlot
{
    [Header("Tên skill (khớp với tên đăng ký trong SkillActions)")]
    public string skillName;

    [Header("Hồi chiêu")]
    public float cooldown = 2f;

    [Header("Vùng phát hiện mục tiêu (để tự tung skill)")]
    public DetectionShape shape = DetectionShape.Circle;
    public Vector2 detectionSize = new Vector2(3f, 3f); // Circle: chỉ dùng detectionSize.x làm bán kính
    public Vector2 detectionOffset = Vector2.zero;       // lệch tâm vùng phát hiện so với object con này
    public string detectTag = "HitBoxPlayer";            // tag cần phát hiện để kích hoạt skill

    [Header("Thông số skill truyền vào SkillContext")]
    public float damage = 10f;
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    public float range = 1f;
    public float speed = 10f;
    public float duration = 0.2f;
    [Min(0f)] public float stunTime;

    [Header("Sprite animation của Enemy")]
    [FormerlySerializedAs("imageSkill")]
    public Sprite[] characterAnimationSprites; // frames animation của Enemy khi cast skill

    [Header("Debug")]
    public bool debug = true;   // true = skill hiện hitbox debug (LineRenderer) trực tiếp trong Game

    [Header("Tuỳ chọn xuyên (dùng cho skill đạn bay)")]
    public bool canPierce = false; // true = xuyên qua mục tiêu, false = dính là dừng+xoá

    [Header("DOT (Damage over Time) - để None nếu skill không gây DOT")]
    public DameDotConfig dotConfig = new DameDotConfig();

    // runtime, không serialize xuống scene/prefab
    [System.NonSerialized] public float lastUsedTime = -999f;
}

/// <summary>
/// Gắn lên 1 GameObject con của Enemy (VD: Enemy/AttackController).
/// Tạo mảng skill trong Inspector, mỗi skill tự sinh ra 1 vùng phát hiện (Box/Circle Collider2D trigger).
/// Khi mục tiêu đúng tag đi vào vùng đó và skill hết hồi chiêu -> tự động Activate skill qua SkillActions.
/// Không cần enemy "biết" player ở đâu theo kiểu thủ công, toàn bộ dựa vào va chạm collider.
///
/// LƯU Ý QUAN TRỌNG (Cách A - fix lỗi lệch/ngược hướng khi flip):
/// Script này dùng localPosition cho detector (đúng chuẩn, không có lỗi).
/// Nếu vùng phát hiện bị lệch hoặc bật ngược hướng khi enemy quay trái/phải,
/// nguyên nhân KHÔNG nằm ở file này mà nằm ở script điều khiển flip hướng của Enemy
/// (thường là EnemyController hoặc script Animator flip).
/// Nếu script đó đang flip bằng cách đổi transform.localScale.x = -1 trên CHÍNH
/// object cha chứa EnemySkillController (hoặc bất kỳ object cha nào phía trên nó),
/// thì mọi localPosition/localScale của các object con - bao gồm cả Detector -
/// sẽ bị nhân theo scale âm đó, gây lệch/ngược hướng.
///
/// Cách sửa (Cách A): chỉ flip SpriteRenderer (hoặc 1 object con riêng chỉ chứa hình ảnh),
/// KHÔNG flip scale của object cha chứa Rigidbody2D + Collider + EnemySkillController.
/// Giữ object cha đó luôn ở scale (1,1,1) để mọi collider/detector con luôn đúng vị trí.
/// </summary>
public class EnemySkillController : MonoBehaviour
{
    [SerializeField] private EnemySkillSlot[] skillSlots;

    [Tooltip("Để trống thì script tự lấy GameObject gốc (transform.root) làm caster.")]
    [SerializeField] private GameObject ownerOverride;

    private GameObject ownerRoot;
    private IFacingProvider facingProvider;
    private EnemyController enemyController;

    [System.NonSerialized] private IHealth health;

    private void Awake()
    {
        enemyController = GetComponentInParent<EnemyController>();
        ownerRoot = ownerOverride != null
            ? ownerOverride
            : enemyController != null ? enemyController.gameObject : transform.root.gameObject;
        facingProvider = GetComponentInParent<IFacingProvider>();

        if (facingProvider == null)
        {
            Debug.LogWarning($"[EnemySkillController] Không tìm thấy IFacingProvider trên parent của '{name}'. " +
                            "Hướng đánh sẽ mặc định là (1,0) - xem hướng dẫn thêm interface vào EnemyController.");
        }

        if (enemyController == null) // MỚI
        {
            Debug.LogWarning($"[EnemySkillController] Không tìm thấy EnemyController trên parent của '{name}'. " +
                            "Enemy sẽ không tự động đuổi theo target.");
        }

        health = GetComponentInParent<IHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[EnemySkillController] Không tìm thấy component implement IHealth trên parent của '{name}'. " +
                            "Script này sẽ không tự disable khi enemy chết, cần tự handle bên ngoài.");
        }

        IgnorePhysicalCollisionWithPlayer();

        if (skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] == null || string.IsNullOrWhiteSpace(skillSlots[i].skillName))
            {
                Debug.LogWarning($"[EnemySkillController] Skill slot {i} has no valid skill name.", this);
                continue;
            }

            SetupDetector(i);
        }
    }

// MỚI - toàn bộ hàm này
private void IgnorePhysicalCollisionWithPlayer()
{
    // Lấy tất cả collider vật lý (không phải trigger) của Enemy - thường là collider chính trên object cha
    Collider2D[] enemyColliders = ownerRoot.GetComponentsInChildren<Collider2D>();

    GameObject player = GameObject.FindGameObjectWithTag("Player");
    if (player == null)
    {
        Debug.LogWarning("[EnemySkillController] Không tìm thấy GameObject có tag 'Player' để bỏ va chạm vật lý.");
        return;
    }

    Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();

    foreach (Collider2D enemyCol in enemyColliders)
    {
        if (enemyCol.isTrigger) continue; // bỏ qua các detector (đã là trigger, không cần xử lý)

        foreach (Collider2D playerCol in playerColliders)
        {
            if (playerCol.isTrigger) continue; // bỏ qua hitbox trigger của player, chỉ xử lý collider vật lý

            Physics2D.IgnoreCollision(enemyCol, playerCol, true);
        }
    }
}

    private void SetupDetector(int index)
    {
        EnemySkillSlot slot = skillSlots[index];

        GameObject detectorObj = new GameObject($"Detector_{(string.IsNullOrEmpty(slot.skillName) ? index.ToString() : slot.skillName)}");
        detectorObj.transform.SetParent(transform.parent, false);
        detectorObj.transform.localPosition = slot.detectionOffset;
        detectorObj.tag = "Skill";
        
        Collider2D col;
        if (slot.shape == DetectionShape.Circle)
        {
            CircleCollider2D circle = detectorObj.AddComponent<CircleCollider2D>();
            circle.radius = slot.detectionSize.x;
            col = circle;
        }
        else
        {
            BoxCollider2D box = detectorObj.AddComponent<BoxCollider2D>();
            box.size = slot.detectionSize;
            col = box;
        }
        col.isTrigger = true;

        // Dùng Kinematic thay vì bỏ hẳn Rigidbody2D:
        // - Không bị đẩy/giật như Dynamic
        // - Không bị lỗi compound collider méo khi Enemy cha có scale âm (flip hướng) hoặc scale khác 1
        Rigidbody2D rb = detectorObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        EnemySkillDetector detector = detectorObj.AddComponent<EnemySkillDetector>();

        detector.Init(
            this,
            index,
            slot.detectTag,
            enemyController
        );
    }

    /// <summary>
    /// Được EnemySkillDetector gọi ngược lại khi có mục tiêu đúng tag trong vùng phát hiện của slot này.
    /// </summary>
    public void TryCastSlot(int index, GameObject detectedTarget)
    {
        if (GameplayPause.IsPaused) return;
        if (skillSlots == null || index < 0 || index >= skillSlots.Length || detectedTarget == null) return;
        if (enemyController != null && enemyController.IsActionLocked) return;
        if (health != null && health.IsDead)
        {
            // Enemy đã chết, không cast skill nữa
            return;
        }

        if (SkillActions.Instance == null)
        {
            Debug.LogWarning("[EnemySkillController] Chưa có SkillActions trong scene.");
            return;
        }

        EnemySkillSlot slot = skillSlots[index];
        if (slot == null || string.IsNullOrWhiteSpace(slot.skillName))
        {
            Debug.LogWarning($"[EnemySkillController] Skill name is empty at slot {index}.", this);
            return;
        }

        if (Time.time - slot.lastUsedTime < slot.cooldown) return; // còn hồi chiêu

        slot.lastUsedTime = Time.time;

        Vector2 direction = facingProvider != null ? facingProvider.FacingDirection : Vector2.right;

        SkillContext context = new SkillContext
        {
            Caster = ownerRoot,
            CasterType = SkillCasterType.Enemy,
            Position = ownerRoot.transform.position,
            Direction = direction,
            Damage = slot.damage,
            Size = slot.skillSize,
            Range = slot.range,
            Speed = slot.speed,
            Duration = slot.duration,
            StunTime = slot.stunTime,
            Debug = slot.debug,
            CharacterAnimationSprites = slot.characterAnimationSprites,
            CanPierce = slot.canPierce,
            DotConfig = slot.dotConfig
        };

        SkillActions.Instance.Activate(slot.skillName, context);
    }
}