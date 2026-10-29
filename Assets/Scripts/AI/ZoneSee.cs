using UnityEngine;
// Gắn script này vào GameObject con "DetectionZone" của Enemy.
[RequireComponent(typeof(CircleCollider2D))]
public class ZoneSee : MonoBehaviour
{
    [Header("Cấu hình vùng phát hiện")]
    public float radius = 2f;

    [Header("Tham chiếu (kéo GameObject Enemy cha vào)")]
    public Transform enemyBody;                    // Transform của Enemy cha
    public EnemyController enemyController;         // Script EnemyController trên Enemy cha

    [Header("Debug hiển thị vùng (chỉ thấy trong Scene view)")]
    public bool showGizmo = true;                   // bật/tắt hiển thị vùng
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.25f); // màu vùng thấy (xanh lá, trong suốt)

    private CircleCollider2D col;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true; // bắt buộc phải là trigger để không cản đường đi
        col.radius = radius;
        ResolveEnemyReferences();
    }

    void LateUpdate()
    {
        // A DetectionZone parented to the enemy follows it automatically.
        // Keep manual following only for setups where it is not a child.
        if (enemyBody != null && transform.parent != enemyBody)
        {
            Vector3 bodyPosition = enemyBody.position;
            if ((transform.position - bodyPosition).sqrMagnitude > 0.000001f)
                transform.position = bodyPosition;
        }

        // Reassigning CircleCollider2D.radius every frame dirties its physics shape.
        if (col != null && !Mathf.Approximately(col.radius, radius))
            col.radius = radius;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HitBoxPlayer"))
        {
            ResolveEnemyReferences();
            if (enemyController != null)
            {
                enemyController.SetTarget(other.transform);
            }
            else
            {
                Debug.LogWarning("ZoneSee không tìm thấy EnemyController. Hãy đặt ZoneSee làm object con của Enemy.", this);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("HitBoxPlayer"))
        {
            if (enemyController != null)
            {
                enemyController.ClearTarget();
            }
        }
    }

    private void ResolveEnemyReferences()
    {
        if (enemyBody == null)
        {
            enemyBody = transform.parent;
        }

        if (enemyController == null && enemyBody != null)
        {
            enemyController = enemyBody.GetComponent<EnemyController>();
        }

        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }

        if (enemyBody == null && enemyController != null)
        {
            enemyBody = enemyController.transform;
        }
    }

    // Vẽ vùng ZoneSee trong Scene view (chỉ hiện trong Editor, không hiện lúc build game)
    void OnDrawGizmos()
    {
        if (!showGizmo) return;


        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, radius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}