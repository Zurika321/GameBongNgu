using System;
using UnityEngine;

/// <summary>
/// Tạo 1 hitbox BAY theo hướng chỉ định (hình vuông hoặc tròn) — dùng cho skill đạn bay, dash...
/// Thay thế FlyProjectile cũ: CHỈ lo geometry + di chuyển + debug + báo callback khi trúng mục tiêu.
/// KHÔNG tự gây sát thương / áp DOT — class Skill gọi hàm Spawn() tự xử lý trong callback onHit.
/// </summary>
public static class ProjectileHitboxSpawner
{
    public static GameObject Spawn(
        GameObject owner,
        Vector2 spawnPos,
        Vector2 direction,
        float speed,
        float lifeTime,
        HitboxShape shape,
        Vector2 size,               // Circle: dùng size.x làm bán kính
        string targetTag,
        bool canPierce,
        bool showDebug,
        Action<GameObject, Vector2> onHit,
        Action<Vector2> onDisappear = null,
        bool rotateWithDirection = true)
    {
        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject obj = new GameObject($"ProjectileHitbox_{(owner != null ? owner.name : "unknown")}");
        obj.transform.position = spawnPos;
        obj.transform.rotation = Quaternion.Euler(0f, 0f, rotateWithDirection ? angle : 0f);
        obj.tag = "Skill";

        Collider2D col = CreateCollider(obj, shape, size);
        col.isTrigger = true;

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        if (showDebug)
        {
            HitboxDebugDrawer.Draw(obj.transform, shape, size);
        }

        ProjectileHitboxReporter reporter = obj.AddComponent<ProjectileHitboxReporter>();
        reporter.Init(owner, targetTag, canPierce, dir, speed, lifeTime, onHit, onDisappear);

        return obj;
    }

    private static Collider2D CreateCollider(GameObject obj, HitboxShape shape, Vector2 size)
    {
        if (shape == HitboxShape.Circle)
        {
            CircleCollider2D c = obj.AddComponent<CircleCollider2D>();
            c.radius = size.x > 0f ? size.x : 0.5f;
            return c;
        }

        BoxCollider2D b = obj.AddComponent<BoxCollider2D>();
        b.size = size.sqrMagnitude > 0.001f ? size : new Vector2(1f, 1f);
        return b;
    }
}
