using UnityEngine;

/// <summary>
/// Vẽ debug hitbox (vuông hoặc tròn) bằng LineRenderer trực tiếp trong Game view.
/// Dùng chung cho ZoneHitboxSpawner + ProjectileHitboxSpawner, tránh mỗi skill tự copy
/// 1 bản CreateDebugBox riêng như ArcAttackSkill/FlyAttackSkill bản cũ.
/// </summary>
public static class HitboxDebugDrawer
{
    public static void Draw(Transform parent, HitboxShape shape, Vector2 size)
    {
        if (shape == HitboxShape.Circle)
            DrawCircle(parent, size.x);
        else
            DrawBox(parent, size);
    }

    private static void DrawBox(Transform parent, Vector2 size)
    {
        LineRenderer line = CreateLine(parent, 5);

        float halfX = size.x / 2f;
        float halfY = size.y / 2f;

        line.SetPosition(0, new Vector3(-halfX, -halfY, 0));
        line.SetPosition(1, new Vector3(-halfX, halfY, 0));
        line.SetPosition(2, new Vector3(halfX, halfY, 0));
        line.SetPosition(3, new Vector3(halfX, -halfY, 0));
        line.SetPosition(4, new Vector3(-halfX, -halfY, 0));
    }

    private static void DrawCircle(Transform parent, float radius)
    {
        const int segments = 24;
        LineRenderer line = CreateLine(parent, segments + 1);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private static LineRenderer CreateLine(Transform parent, int positionCount)
    {
        GameObject debugObj = new GameObject("DEBUG_HITBOX");
        debugObj.transform.SetParent(parent);
        debugObj.transform.localPosition = Vector3.zero;
        debugObj.transform.localRotation = Quaternion.identity;
        debugObj.transform.localScale = Vector3.one;

        int decorLayer = LayerMask.NameToLayer("Decor");
        if (decorLayer != -1) debugObj.layer = decorLayer;

        LineRenderer line = debugObj.AddComponent<LineRenderer>();
        line.positionCount = positionCount;
        line.loop = false;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.red;
        line.endColor = Color.red;
        line.startWidth = 0.08f;
        line.endWidth = 0.08f;
        line.useWorldSpace = false;
        line.sortingLayerName = "Decor";
        line.sortingOrder = 9999;

        Object.Destroy(debugObj, 1f);
        return line;
    }
}
