using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class ZoneTrigger : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private string zoneName = "Nhà Hoang";

    [Header("Detect")]
    [SerializeField] private string playerHitBoxTag = "HitBoxPlayer";
    [SerializeField] private bool saveOnEnter = true;
    [SerializeField] private bool debugLog = false;

    private PolygonCollider2D zoneCollider;
    private bool applied; // đã áp zone này cho lần vào hiện tại chưa

    private void Reset()
    {
        GetComponent<PolygonCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<PolygonCollider2D>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning("[ZoneTrigger] PolygonCollider2D chưa bật Is Trigger, đang tự bật.", this);
            zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (debugLog) Debug.Log($"[ZoneTrigger:{zoneName}] Enter: {other.name} (tag {other.tag})", this);
        TryApply(other);
    }

    // Bắt trường hợp player đã đứng sẵn bên trong (teleport, load save, spawn)
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!applied) TryApply(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerHitBoxTag)) applied = false;
    }

    private void OnDisable()
    {
        applied = false;
    }

    private void TryApply(Collider2D other)
    {
        if (!other.CompareTag(playerHitBoxTag)) return;
        ApplyZone();
    }

    private void ApplyZone()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null)
        {
            Debug.LogError("[ZoneTrigger] PlayerProgressionService is required.", this);
            return;
        }

        // Đang khôi phục save thì chưa ghi đè zone
        if (progress.IsContinueRestorePending) return;
        if (string.IsNullOrEmpty(zoneName)) return;

        applied = true;
        if (progress.Data.currentZone == zoneName) return;

        progress.SetCurrentZone(zoneName);
        if (saveOnEnter) progress.Save();
        if (debugLog) Debug.Log($"[ZoneTrigger] currentZone = {zoneName}", this);
    }
}