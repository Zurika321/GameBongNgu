using System;
using UnityEngine;

/// <summary>
/// Tạo 1 vùng hitbox ĐỨNG YÊN tại vị trí chỉ định (hình vuông hoặc tròn).
/// Dùng cho skill kiểu "nổ tại chỗ", "zone phép thuật", hitbox cận chiến trước mặt (ArcAttack)...
///
/// CHỈ tạo collider + Rigidbody2D (Kinematic) + debug visual + báo callback khi trúng mục tiêu.
/// KHÔNG tự gây sát thương / áp DOT — class Skill gọi hàm Spawn() tự xử lý toàn bộ trong callback onHit.
/// Đây chính là điểm khác với ArcAttackSkill bản cũ (hitbox cũ tự làm luôn việc gây dame/DOT,
/// gây dính logic và dễ sai khi skill khác muốn xử lý khác đi).
/// </summary>
public static class ZoneHitboxSpawner
{
    public static GameObject Spawn(
        GameObject owner,
        Vector2 position,
        HitboxShape shape,
        Vector2 size,              // Circle: dùng size.x làm bán kính
        float lifeTime,
        string targetTag,
        bool showDebug,
        Action<GameObject, Vector2> onHit,
        float rotationDeg = 0f,
        bool rotateWithDirection = true)
    {
        GameObject obj = new GameObject($"ZoneHitbox_{(owner != null ? owner.name : "unknown")}");
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(0f, 0f, rotateWithDirection ? rotationDeg : 0f);
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

        HitboxReporter reporter = obj.AddComponent<HitboxReporter>();
        reporter.Init(owner, targetTag, onHit);

        if (lifeTime > 0f)
        {
            UnityEngine.Object.Destroy(obj, lifeTime);
        }

        return obj;
    }

    private static Collider2D CreateCollider(GameObject obj, HitboxShape shape, Vector2 size)
    {
        if (shape == HitboxShape.Circle)
        {
            CircleCollider2D c = obj.AddComponent<CircleCollider2D>();
            c.radius = size.x > 0f ? size.x : 1f;
            return c;
        }

        BoxCollider2D b = obj.AddComponent<BoxCollider2D>();
        b.size = size.sqrMagnitude > 0.001f ? size : new Vector2(1f, 1f);
        return b;
    }
}
