using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều khiển di chuyển + state cho Enemy.
/// KHÔNG tự xử lý skill (đã tách riêng qua EnemySkillController) và KHÔNG tự tìm target —
/// ZoneSee (vùng nhìn thấy của enemy) chịu trách nhiệm phát hiện Player rồi gọi
/// SetTarget()/ClearTarget() xuống đây để enemy biết đường đuổi theo.
/// Implement IFacingProvider để EnemySkillController lấy đúng hướng tấn công hiện tại.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour, IFacingProvider
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Dừng lại khi đã đủ gần target, nhường khoảng cách cho skill (EnemySkillController) xử lý tiếp.")]
    [SerializeField] private float stopDistance = 1f;

    [Header("Refs")]
    [SerializeField] private CharacterAnimation characterAnimation;

    private Rigidbody2D rb;
    private IHealth health;
    private bool isMovementLocked;

    private Transform target;
    private Vector2 facingDirection = Vector2.down;
    private float hitReactionRemaining;
    private float hitReactionDuration;
    private bool isStunned;
    private const float DefaultHurtDuration = 0.1f;
    private Vector2 dashDirection;
    private float dashSpeed;
    private float dashRemaining;
    private SpriteRenderer[] deathRenderers = new SpriteRenderer[0];
    private Color[] deathRendererColors = new Color[0];
    private bool deathSequenceStarted;
    private float deathElapsed;
    private const float CorpseVisibleDuration = 3f;
    private const float DeathFadeDuration = 1f;

    public Vector2 FacingDirection => facingDirection;
    public Transform Target => target;
    public bool IsActionLocked => isMovementLocked || hitReactionRemaining > 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<IHealth>();

        if (health == null)
        {
            Debug.LogWarning($"[EnemyController] '{name}' không có component implement IHealth — sẽ không tự dừng khi chết.", this);
        }

        if (characterAnimation == null) characterAnimation = GetComponent<CharacterAnimation>();
        if (characterAnimation == null)
            Debug.LogWarning($"[EnemyController] '{name}' không có CharacterAnimation.", this);

        CacheDeathRenderers();
    }

    /// <summary>ZoneSee gọi hàm này khi phát hiện target (thường là Player) đi vào tầm nhìn.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>ZoneSee gọi hàm này khi target rời khỏi tầm nhìn.</summary>
    public void ClearTarget()
    {
        target = null;
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

        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            if (characterAnimation != null) characterAnimation.Play(AnimState.Idle);
            return;
        }

        float distance = Vector2.Distance(rb.position, target.position);

        if (distance > stopDistance)
        {
            Vector2 direction = ((Vector2)target.position - rb.position).normalized;
            Move(direction);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            if (characterAnimation != null) characterAnimation.Play(AnimState.Idle);
        }
    }

    private void Update()
    {
        UpdateHitReaction();
        UpdateDeathSequence();
    }

    private void CacheDeathRenderers()
    {
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        List<Color> colors = new List<Color>();
        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer == null || IsHealthBarRenderer(spriteRenderer.transform)) continue;
            renderers.Add(spriteRenderer);
            colors.Add(spriteRenderer.color);
        }

        deathRenderers = renderers.ToArray();
        deathRendererColors = colors.ToArray();
    }

    private bool IsHealthBarRenderer(Transform rendererTransform)
    {
        Transform current = rendererTransform;
        while (current != null && current != transform)
        {
            if (current.name == "HealthBar") return true;
            current = current.parent;
        }

        return false;
    }

    private void UpdateDeathSequence()
    {
        if (health == null) return;
        if (!health.IsDead)
        {
            if (!deathSequenceStarted) return;
            deathSequenceStarted = false;
            deathElapsed = 0f;
            RestoreDeathRendererColors();
            return;
        }

        if (!deathSequenceStarted)
        {
            deathSequenceStarted = true;
            deathElapsed = 0f;
        }

        if (GameplayPause.IsPaused) return;

        deathElapsed += Time.deltaTime;
        if (deathElapsed < CorpseVisibleDuration) return;

        float alpha = 1f - Mathf.Clamp01((deathElapsed - CorpseVisibleDuration) / DeathFadeDuration);
        for (int i = 0; i < deathRenderers.Length; i++)
        {
            if (deathRenderers[i] == null) continue;
            Color color = deathRendererColors[i];
            color.a *= alpha;
            deathRenderers[i].color = color;
        }

        if (alpha <= 0f) Destroy(gameObject);
    }

    private void RestoreDeathRendererColors()
    {
        for (int i = 0; i < deathRenderers.Length; i++)
        {
            if (deathRenderers[i] != null) deathRenderers[i].color = deathRendererColors[i];
        }
    }

    private void Move(Vector2 direction)
    {
        rb.linearVelocity = direction * moveSpeed;
        facingDirection = direction;

        if (characterAnimation != null)
        {
            characterAnimation.SetFacing(direction);
            characterAnimation.Play(AnimState.Run);
        }
    }

    public void LockMovement(bool locked)
    {
        isMovementLocked = locked;
        if (locked) rb.linearVelocity = Vector2.zero;
    }

    public void Dash(Vector2 direction, float speed, float duration)
    {
        if (direction.sqrMagnitude <= 0.001f || duration <= 0f) return;
        dashDirection = direction.normalized;
        dashSpeed = Mathf.Max(1f, speed);
        dashRemaining = duration;
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
