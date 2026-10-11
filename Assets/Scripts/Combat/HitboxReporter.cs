using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn tự động lên hitbox do ZoneHitboxSpawner tạo ra.
/// CHỈ phát hiện va chạm đúng tag và báo callback — KHÔNG tự gây sát thương / áp DOT.
/// Class Skill (nơi gọi ZoneHitboxSpawner.Spawn) tự quyết định làm gì khi trúng đòn
/// bên trong callback onHit của chính nó — tách biệt hoàn toàn khỏi hitbox.
/// Mỗi target chỉ được báo 1 lần trong suốt vòng đời hitbox (phù hợp skill AOE 1 phát ăn nhiều mục tiêu).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HitboxReporter : MonoBehaviour
{
    private GameObject owner;
    private string targetTag;
    private Action<GameObject, Vector2> onHit;
    private readonly HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();

    public void Init(GameObject owner, string targetTag, Action<GameObject, Vector2> onHit)
    {
        this.owner = owner;
        this.targetTag = targetTag;
        this.onHit = onHit;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;
        if (string.IsNullOrEmpty(targetTag) || !other.CompareTag(targetTag)) return;
        if (hitTargets.Contains(other)) return;

        hitTargets.Add(other);
        onHit?.Invoke(other.gameObject, transform.position);
    }
}
