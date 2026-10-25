using UnityEngine;

/// <summary>
/// Vùng (zone) gây DOT khi ActivationType = Zone. Tồn tại config.zoneLifetime giây rồi tự huỷ.
/// Ai đứng trong đó (đúng targetTag) sẽ được áp DOT qua DameDotEffect.ApplyOrRefresh() -
/// đứng lâu hoặc ra vào nhiều lần chỉ refresh lại thời gian dính DOT, không tạo thêm gì mới nên không lag.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DameDotZone : MonoBehaviour
{
    private GameObject source;
    private string targetTag;
    private DameDotConfig config;

    public void Init(GameObject source, string targetTag, DameDotConfig config)
    {
        this.source = source;
        this.targetTag = targetTag;
        this.config = config;

        float lifeTime = config.zoneLifetime > 0f ? config.zoneLifetime : 5f;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryApply(other);

    private void OnTriggerStay2D(Collider2D other) => TryApply(other);

    private void TryApply(Collider2D other)
    {
        if (string.IsNullOrEmpty(targetTag) || !other.CompareTag(targetTag)) return;
        DameDotEffect.ApplyOrRefresh(other.gameObject, source, config);
    }

    /// <summary>
    /// Tạo 1 zone DOT dựa theo SkillContext (vị trí = Position + Direction * Range, giống ArcAttackSkill).
    /// Chỉ tạo khi context.DotConfig != null và ActivationType = Zone, ngược lại bỏ qua (trả về null).
    /// </summary>
    public static GameObject CreateZone(SkillContext context)
    {
        DameDotConfig config = context.DotConfig;
        if (config == null || config.activationType != DotActivationType.Zone)
        {
            return null;
        }

        Vector2 dir = context.Direction.sqrMagnitude > 0.001f
            ? context.Direction.normalized
            : Vector2.right;

        Vector2 spawnPos = context.Position + dir * context.Range;

        GameObject zoneObj = new GameObject($"Zone_DameDOT_{context.Caster.name}");
        zoneObj.transform.position = spawnPos;
        zoneObj.tag = "Skill";

        Vector2 size = config.zoneSize.sqrMagnitude > 0.001f ? config.zoneSize : new Vector2(2f, 2f);

        Collider2D col;
        if (config.zoneShape == DotZoneShape.Circle)
        {
            CircleCollider2D circle = zoneObj.AddComponent<CircleCollider2D>();
            circle.radius = size.x;
            col = circle;
        }
        else
        {
            BoxCollider2D box = zoneObj.AddComponent<BoxCollider2D>();
            box.size = size;
            col = box;
        }
        col.isTrigger = true;

        Rigidbody2D rb = zoneObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        if (context.Debug)
        {
            CreateDebugVisual(zoneObj.transform, config.zoneShape, size);
        }

        DameDotZone zone = zoneObj.AddComponent<DameDotZone>();
        zone.Init(context.Caster, context.ResolveTargetTag(), config);

        return zoneObj;
    }

    /// <summary>
    /// Vẽ viền phạm vi zone DOT bằng LineRenderer màu xanh dương, phân biệt với hitbox đòn đánh thường (màu đỏ).
    /// Gắn làm con của zone nên tự bị Destroy theo zone khi hết zoneLifetime, không cần tự huỷ riêng.
    /// </summary>
    private static void CreateDebugVisual(Transform parent, DotZoneShape shape, Vector2 size)
    {
        GameObject debugObj = new GameObject("DEBUG_DOT_ZONE");

        debugObj.transform.SetParent(parent);
        debugObj.transform.localPosition = Vector3.zero;
        debugObj.transform.localRotation = Quaternion.identity;
        debugObj.transform.localScale = Vector3.one;

        int decorLayer = LayerMask.NameToLayer("Decor");
        if (decorLayer != -1) debugObj.layer = decorLayer;

        LineRenderer line = debugObj.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.blue;
        line.endColor = Color.blue;
        line.startWidth = 0.08f;
        line.endWidth = 0.08f;
        line.useWorldSpace = false;
        line.sortingLayerName = "skill";
        line.sortingOrder = 9999;

        if (shape == DotZoneShape.Circle)
        {
            const int segments = 32;
            float radius = size.x;

            line.loop = true;
            line.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }
        else
        {
            float halfX = size.x / 2f;
            float halfY = size.y / 2f;

            line.loop = false;
            line.positionCount = 5;

            line.SetPosition(0, new Vector3(-halfX, -halfY, 0));
            line.SetPosition(1, new Vector3(-halfX, halfY, 0));
            line.SetPosition(2, new Vector3(halfX, halfY, 0));
            line.SetPosition(3, new Vector3(halfX, -halfY, 0));
            line.SetPosition(4, new Vector3(-halfX, -halfY, 0));
        }
    }
}
