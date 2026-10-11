using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn tự động lên hitbox bay do ProjectileHitboxSpawner tạo.
/// Lo việc DI CHUYỂN + PHÁT HIỆN va chạm + báo callback (onHit, onDisappear).
/// KHÔNG tự gây sát thương / áp DOT — class Skill xử lý toàn bộ trong callback onHit.
/// Về cơ bản thay thế projectile cũ nhưng bỏ hẳn phần IDamage.TakeDamage() ra khỏi đây.
/// </summary>
public class ProjectileHitboxReporter : MonoBehaviour
{
    private const string TAG_SKILL = "Skill";

    private GameObject owner;
    private string targetTag;
    private bool canPierce;
    private Vector2 direction;
    private float speed;
    private float lifeTime;
    private Action<GameObject, Vector2> onHit;
    private Action<Vector2> onDisappear;

    private float timer;
    private Rigidbody2D rb;
    private readonly HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();

    public void Init(
        GameObject owner,
        string targetTag,
        bool canPierce,
        Vector2 direction,
        float speed,
        float lifeTime,
        Action<GameObject, Vector2> onHit,
        Action<Vector2> onDisappear = null)
    {
        this.owner = owner;
        this.targetTag = targetTag;
        this.canPierce = canPierce;
        this.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        this.speed = speed;
        this.lifeTime = lifeTime;
        this.onHit = onHit;
        this.onDisappear = onDisappear;

        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject); // hết Duration mà chưa va gì -> OnDestroy tự báo onDisappear
        }
    }

    private void FixedUpdate()
    {
        Vector2 nextPos = (rb != null ? rb.position : (Vector2)transform.position) + direction * speed * Time.fixedDeltaTime;

        if (rb != null) rb.MovePosition(nextPos);
        else transform.position = nextPos;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;
        if (other.CompareTag(TAG_SKILL)) return; // bỏ qua hitbox của skill khác

        bool isValidTarget = !string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag);

        if (isValidTarget && !hitTargets.Contains(other))
        {
            hitTargets.Add(other);
            onHit?.Invoke(other.gameObject, transform.position);
        }

        bool shouldStop = isValidTarget && !canPierce;
        if (shouldStop)
        {
            Destroy(gameObject); // OnDestroy tự báo onDisappear tại đúng vị trí va chạm
        }
    }

    private void OnDestroy()
    {
        onDisappear?.Invoke(transform.position);
    }
}
