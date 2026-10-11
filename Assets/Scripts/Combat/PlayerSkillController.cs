using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lựa chọn nút chuột dùng để kích hoạt skill. None = không dùng chuột cho skill này.
/// </summary>
public enum MouseButtonSelect
{
    None,
    Left,
    Right,
    Middle
}

/// <summary>
/// 1 dòng trong mảng skill của Player - 3 cách kích hoạt độc lập, admin bật/tắt tuỳ ý trong Inspector:
/// 1) Phím PC gõ dạng string (pcKey)   2) Nút UI (uiButton, có thể để trống)   3) Nút chuột (mouseButton).
///
/// pcKey là string thay vì KeyCode vì Unity's Input.GetKeyDown(string) nhận tên phím dạng chữ,
/// cho phép gõ trực tiếp: "a", "k", "l", "b", "q", "e", "1"..."0", "space", "left shift", "up", "down",
/// và cả phím numpad kiểu "[1]", "[+]", "[enter]"... (những phím mà KeyCode enum khó chọn/khó nhớ tên).
/// Danh sách đầy đủ tên hợp lệ: xem "Input.GetKeyDown(string)" trong Unity Docs (phần Key names).
///
/// Trên PC: khớp bất kỳ 1 trong 3 cách trên đều kích hoạt được skill.
/// Trên mobile: không có bàn phím/chuột vật lý nên chỉ uiButton hoạt động trong thực tế.
/// </summary>
[System.Serializable]
public class PlayerSkillSlot
{
    [Header("Tên skill (khớp với tên đăng ký trong SkillActions)")]
    public string skillName;

    [Tooltip("Để trống nếu skill đã mở từ đầu; có thể nhập tên trong danh sách kỹ năng đã mở khóa của save.")]
    public string requiredUnlockSkill;

    [Header("Hồi chiêu")]
    public float cooldown = 1f;

    [Header("1. Kích hoạt bằng phím PC - gõ tên phím dạng string (vd: \"k\", \"l\", \"b\", \"q\", \"e\", \"1\", \"space\", \"[1]\" cho numpad 1). Để trống = không dùng phím.")]
    public string pcKey = "";

    [Header("2. Kích hoạt bằng UI Button (để trống nếu không có)")]
    public Button uiButton;

    [Header("3. Kích hoạt bằng chuột (None = không dùng chuột)")]
    public MouseButtonSelect mouseButton = MouseButtonSelect.None;

    [Header("Thông số skill truyền vào SkillContext")]
    public float damage = 10f;
    public Vector2 skillSize = new Vector2(1.5f, 1f);
    public float range = 1f;
    public float speed = 10f;
    public float duration = 0.2f;
    [Min(0f)] public float stunTime;

    [Header("Sprite animation của Player")]
    [FormerlySerializedAs("imageSkill")]
    public Sprite[] characterAnimationSprites; // frames animation của Player khi cast skill

    [Header("Debug")]
    public bool debug = true;   // true = skill hiện hitbox debug (LineRenderer) trực tiếp trong Game

    [Header("Tuỳ chọn xuyên (dùng cho skill đạn bay)")]
    public bool canPierce = false; // true = xuyên qua mục tiêu, false = dính là dừng+xoá

    [Header("DOT (Damage over Time) - để None nếu skill không gây DOT")]
    public DameDotConfig dotConfig = new DameDotConfig();

    // runtime, không serialize xuống scene/prefab
    [System.NonSerialized] public float lastUsedTime = -999f;
    [System.NonSerialized] public bool pcKeyInvalid = false; // đánh dấu nếu gõ sai tên phím, tránh spam warning mỗi frame
}

/// <summary>
/// Gắn lên 1 GameObject con của Player (VD: Player/AttackController).
/// Hỗ trợ song song nhiều kiểu kích hoạt cho mỗi skill: phím PC (string), nút chuột, nút UI (đều tuỳ chọn, độc lập nhau).
/// Có thêm entry point OnAttack() để gắn vào action "Attack" của component PlayerInput
/// (Unity Input System) nếu project có cài package đó - script vẫn chạy được bình thường nếu không cài.
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private PlayerSkillSlot[] skillSlots;

    [Tooltip("Để trống thì script tự lấy GameObject gốc (transform.root) làm caster.")]
    [SerializeField] private GameObject ownerOverride;

    private GameObject ownerRoot;
    private IFacingProvider facingProvider;
    private PlayerController playerController;

    [System.NonSerialized] private IHealth health;

    private void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
        ownerRoot = ownerOverride != null
            ? ownerOverride
            : playerController != null ? playerController.gameObject : transform.root.gameObject;
        facingProvider = GetComponentInParent<IFacingProvider>();

        if (facingProvider == null)
        {
            Debug.LogWarning($"[PlayerSkillController] Không tìm thấy IFacingProvider trên parent của '{name}'. " +
                              "Hướng đánh sẽ mặc định là (1,0) - hãy gắn interface lên PlayerController.");
        }

        health = GetComponentInParent<IHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[PlayerSkillController] Không tìm thấy component implement IHealth trên parent của '{name}'. " +
                              "Script này sẽ không tự disable khi player chết, cần tự handle bên ngoài.");
        }

        BindUiButtons();
    }

    private void BindUiButtons()
    {
        if (skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] == null) continue;

            int capturedIndex = i; // tránh lỗi closure khi dùng biến vòng lặp trong lambda
            Button btn = skillSlots[i].uiButton;
            if (btn != null)
            {
                btn.onClick.AddListener(() => TryActivateSlot(capturedIndex));
            }
        }
    }

    private void Update()
    {
        if (GameplayPause.IsPaused || skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            CheckPcInput(i);
        }
    }

    private void CheckPcInput(int index)
    {
        PlayerSkillSlot slot = skillSlots[index];
        if (slot == null) return;

        bool keyTriggered = !string.IsNullOrEmpty(slot.pcKey) && !slot.pcKeyInvalid && TryGetKeyDown(slot);
        bool mouseTriggered = IsMouseButtonDown(slot.mouseButton);

        if (keyTriggered || mouseTriggered)
        {
            TryActivateSlot(index);
        }
    }

    /// <summary>
    /// Bọc Input.GetKeyDown(string) trong try/catch vì nếu admin gõ sai tên phím,
    /// Unity sẽ throw ArgumentException. Gặp lỗi thì log cảnh báo 1 lần và tắt hẳn phím đó (pcKeyInvalid),
    /// tránh việc throw exception lặp lại mỗi frame gây tốn hiệu năng.
    /// </summary>
    private bool TryGetKeyDown(PlayerSkillSlot slot)
    {
        try
        {
            return Input.GetKeyDown(slot.pcKey);
        }
        catch (System.ArgumentException)
        {
            Debug.LogWarning($"[PlayerSkillController] Tên phím '{slot.pcKey}' cho skill '{slot.skillName}' không hợp lệ. " +
                              "Xem danh sách tên phím hợp lệ tại Unity Docs (Input.GetKeyDown string). " +
                              "Ví dụ đúng: \"a\", \"1\", \"space\", \"left shift\", \"[1]\" (numpad 1). Bỏ qua phím này từ giờ.");
            slot.pcKeyInvalid = true;
            return false;
        }
    }

    private bool IsMouseButtonDown(MouseButtonSelect select)
    {
        switch (select)
        {
            case MouseButtonSelect.Left: return Input.GetMouseButtonDown(0);
            case MouseButtonSelect.Right: return Input.GetMouseButtonDown(1);
            case MouseButtonSelect.Middle: return Input.GetMouseButtonDown(2);
            default: return false;
        }
    }

    /// <summary>
    /// Kích hoạt skill theo index. Gọi từ CheckPcInput, từ UI Button, hoặc từ OnAttack() bên dưới.
    /// Có thể gọi trực tiếp từ bên ngoài (VD script AI, test, hoặc UI khác) nếu cần.
    /// </summary>
    public void TryActivateSlot(int index)
    {
        if (GameplayPause.IsPaused || skillSlots == null || index < 0 || index >= skillSlots.Length) return;
        if (playerController != null && playerController.IsActionLocked) return;

        if (health != null && health.IsDead)
        {
            return;
        }

        PlayerSkillSlot slot = skillSlots[index];
        if (slot == null) return;

        if (!string.IsNullOrWhiteSpace(slot.requiredUnlockSkill)
            && (PlayerProgressionService.Instance == null
                || !PlayerProgressionService.Instance.HasUnlockedSkill(slot.requiredUnlockSkill)))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(slot.skillName))
        {
            Debug.LogWarning($"[PlayerSkillController] Skill name is empty at slot {index}.", this);
            return;
        }

        if (Time.time - slot.lastUsedTime < slot.cooldown)
        {
            float remain = slot.cooldown - (Time.time - slot.lastUsedTime);
            Debug.Log($"[PlayerSkillController] Skill '{slot.skillName}' còn hồi chiêu {remain:F1}s.");
            return;
        }

        if (SkillActions.Instance == null)
        {
            Debug.LogWarning("[PlayerSkillController] Chưa có SkillActions trong scene.");
            return;
        }

        slot.lastUsedTime = Time.time;

        Vector2 direction = facingProvider != null ? facingProvider.FacingDirection : Vector2.right;
        float damage = slot.damage;
        if (slot.skillName == "BasicSlash" && PlayerProgressionService.Instance != null)
            damage = PlayerProgressionService.Instance.GetAttackPower();

        SkillContext context = new SkillContext
        {
            Caster = ownerRoot,
            CasterType = SkillCasterType.Player,
            Position = ownerRoot.transform.position,
            Direction = direction,
            Damage = damage,
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

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Gắn hàm này vào action "Attack" của component PlayerInput (Behavior = Invoke Unity Events)
    /// nếu project dùng Unity Input System package thay vì Input class cũ.
    /// Tự soi control vừa bấm là phím bàn phím hay nút chuột nào rồi khớp với slot tương ứng.
    /// Lưu ý: tên control của Input System (vd "leftShift", "space") không luôn giống hệt string
    /// dùng cho Input.GetKeyDown cũ (vd "left shift" có dấu cách) - hàm dưới đã tự bỏ dấu cách + lowercase
    /// trước khi so sánh nên đa số trường hợp vẫn khớp đúng.
    /// </summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (MatchesControl(skillSlots[i], context.control))
            {
                TryActivateSlot(i);
                return;
            }
        }
    }

    private bool MatchesControl(PlayerSkillSlot slot, InputControl control)
    {
        if (control.device is Mouse)
        {
            switch (slot.mouseButton)
            {
                case MouseButtonSelect.Left: return control.name == "leftButton";
                case MouseButtonSelect.Right: return control.name == "rightButton";
                case MouseButtonSelect.Middle: return control.name == "middleButton";
                default: return false;
            }
        }

        if (control.device is Keyboard && !string.IsNullOrEmpty(slot.pcKey))
        {
            string normalizedSlotKey = slot.pcKey.Replace(" ", "").ToLowerInvariant();
            string normalizedControlName = control.name.Replace(" ", "").ToLowerInvariant();
            return normalizedControlName == normalizedSlotKey;
        }

        return false;
    }
#endif
}