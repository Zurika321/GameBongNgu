using UnityEngine;

/// <summary>
/// Điều khiển di chuyển + state cho Player.
/// KHÔNG xử lý skill nữa — PlayerSkillController (gameobject con) tự gọi thẳng SkillActions
/// và CharacterAnimation khi cần, hoàn toàn độc lập, không phải đi qua Controller.
/// Controller chỉ lo Move/Idle/Die, cung cấp FacingDirection (IFacingProvider) cho skill dùng,
/// và cho phép khóa/mở di chuyển từ bên ngoài (PlayerSkillController gọi khi cast skill cần đứng yên).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, IFacingProvider
{
    [Header("Movement")]
    [Tooltip("Tốc độ di chuyển — chỉnh trực tiếp trong Inspector.")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Refs")]
    [SerializeField] private CharacterAnimation characterAnimation;

    private Rigidbody2D rb;
    private IHealth health;

    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.down;
    private bool isLocked; // true khi PlayerSkillController đang cast skill cần khóa di chuyển
    private Vector2 dashDirection;
    private float dashSpeed;
    private float dashRemaining;
    private float hitReactionRemaining;
    private float hitReactionDuration;
    private bool isStunned;
    private const float DefaultHurtDuration = 0.1f;

    public Vector2 FacingDirection => facingDirection;
    public bool IsActionLocked => isLocked || hitReactionRemaining > 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<IHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[PlayerController] '{name}' không có component implement IHealth — sẽ không tự dừng khi chết.", this);
        }

        if (characterAnimation == null) characterAnimation = GetComponent<CharacterAnimation>();
        if (characterAnimation == null)
            Debug.LogWarning($"[PlayerController] '{name}' không có CharacterAnimation.", this);
    }

    private void Update()
    {
        UpdateHitReaction();

        if (GameplayPause.IsPaused)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (health != null && health.IsDead) return;

        if (isLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
    }

    private void FixedUpdate()
    {
        if (GameplayPause.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            if (characterAnimation != null) characterAnimation.Play(AnimState.Die);
            return;
        }

        if (IsActionLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dashRemaining > 0f)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

        Move(moveInput);
    }

    private void Move(Vector2 direction)
    {
        rb.linearVelocity = direction * moveSpeed;

        if (direction.sqrMagnitude > 0.01f)
        {
            facingDirection = direction;
            if (characterAnimation != null)
            {
                characterAnimation.SetFacing(direction);
                characterAnimation.Play(AnimState.Run);
            }
        }
        else
        {
            if (characterAnimation != null) characterAnimation.Play(AnimState.Idle);
        }
    }

    /// <summary>
    /// Gọi từ PlayerSkillController khi bắt đầu/kết thúc cast 1 skill cần khóa di chuyển.
    /// Controller không tự biết skill nào đang chạy — chỉ nhận lệnh khóa/mở từ bên ngoài.
    /// </summary>
    public void LockMovement(bool locked)
    {
        isLocked = locked;
        if (locked) rb.linearVelocity = Vector2.zero;
    }

    public void Dash(Vector2 direction, float speed, float duration)
    {
        if (direction.sqrMagnitude <= 0.001f || duration <= 0f) return;
        dashDirection = direction.normalized;
        dashSpeed = Mathf.Max(1f, speed);
        dashRemaining = duration;
        facingDirection = dashDirection;
    }

    public void ApplyHitReaction(float stunTime)
    {
        if (health != null && health.IsDead) return;
        if (isStunned && stunTime <= 0f) return;

        float duration = stunTime > 0f ? stunTime : DefaultHurtDuration;
        if (stunTime > 0f)
        {
            hitReactionDuration = Mathf.Max(hitReactionDuration, duration);
            hitReactionRemaining = Mathf.Max(hitReactionRemaining, duration);
            isStunned = true;
        }
        else
        {
            hitReactionDuration = duration;
            hitReactionRemaining = duration;
            isStunned = false;
        }

        rb.linearVelocity = Vector2.zero;
        if (characterAnimation != null)
        {
            characterAnimation.Play(isStunned ? AnimState.Stun : AnimState.Hurt);
        }

        UpdateStunBar();
    }

    private void UpdateHitReaction()
    {
        if (dashRemaining > 0f && !GameplayPause.IsPaused)
            dashRemaining = Mathf.Max(0f, dashRemaining - Time.deltaTime);

        if (hitReactionRemaining <= 0f) return;
        if (!GameplayPause.IsPaused) hitReactionRemaining = Mathf.Max(0f, hitReactionRemaining - Time.deltaTime);

        if (hitReactionRemaining <= 0f)
        {
            hitReactionDuration = 0f;
            isStunned = false;
        }

        UpdateStunBar();
    }

    private void UpdateStunBar()
    {
        HealthController healthController = health as HealthController;
        if (healthController == null) return;

        float progress = isStunned && hitReactionDuration > 0f
            ? hitReactionRemaining / hitReactionDuration
            : 0f;
        healthController.SetStunProgress(progress);
    }
}
