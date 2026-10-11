using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn tự động lên GameObject hitbox được skill spawn ra (không cần add tay trong Editor).
/// Kiểm tra va chạm với đúng tag mục tiêu, gây dame 1 lần cho mỗi mục tiêu, tự huỷ sau lifeTime.
/// Có thể kèm thêm DameDotConfig (ActivationType = Immediate) để áp DOT ngay lên mục tiêu vừa trúng đòn.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SkillHitbox : MonoBehaviour
{
    private GameObject owner;
    private string targetTag;
    private float damage;
    private float stunTime;
    private DameDotConfig dotConfig;
    private readonly HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();

    public void Init(GameObject owner, string targetTag, float damage, float lifeTime,
        DameDotConfig dotConfig = null, float stunTime = 0f)
    {
        this.owner = owner;
        this.targetTag = targetTag;
        this.damage = damage;
        this.stunTime = Mathf.Max(0f, stunTime);
        this.dotConfig = dotConfig;

        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (!other.CompareTag(targetTag)) return;
        if (hitTargets.Contains(other)) return;

        hitTargets.Add(other);

        IDamage damageable = other.GetComponentInParent<IDamage>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, owner, stunTime);
        }
        else
        {
            Debug.Log($"[SkillHitbox] '{other.name}' có tag '{targetTag}' nhưng chưa implement IDamage -> {damage} dame không được áp dụng.");
        }

        // Nếu skill có cấu hình DOT kiểu Immediate thì áp/refresh luôn lên mục tiêu vừa trúng đòn này.
        if (dotConfig != null && dotConfig.activationType == DotActivationType.Immediate)
        {
            DameDotEffect.ApplyOrRefresh(other.gameObject, owner, dotConfig);
        }
    }
}