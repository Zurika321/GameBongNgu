using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn tự động lên GameObject gốc của mục tiêu (nơi có IDamage) khi bị dính DOT.
/// QUAN TRỌNG: nếu mục tiêu đã dính DOT từ trước (component này đã tồn tại trên nó), KHÔNG tạo thêm
/// component/coroutine mới - chỉ gọi Refresh() để cập nhật lại thời gian còn lại + thông số mới nhất.
/// Tránh trường hợp đứng lâu trong zone hoặc bị trúng nhiều lần làm chồng hàng loạt coroutine gây lag.
/// Luôn dùng DameDotEffect.ApplyOrRefresh(...) để áp DOT, không AddComponent trực tiếp.
/// </summary>
public class DameDotEffect : MonoBehaviour
{
    private GameObject source;
    private IDamage damageable;
    private float damagePerTick;
    private float tickInterval;
    private float remainingDuration;

    /// <summary>
    /// Điểm vào duy nhất để áp DOT lên 1 mục tiêu (Immediate hoặc từ Zone đều gọi qua đây).
    /// targetHitObject là GameObject của collider vừa bị trúng (có thể là hurtbox con) - hàm tự tìm
    /// lên IDamage ở object cha để xác định "chủ thể" thực sự cần theo dõi DOT.
    /// </summary>
    public static void ApplyOrRefresh(GameObject targetHitObject, GameObject source, DameDotConfig config)
    {
        if (targetHitObject == null || config == null || config.activationType == DotActivationType.None)
        {
            return;
        }

        IDamage damageable = targetHitObject.GetComponentInParent<IDamage>();
        if (damageable == null)
        {
            Debug.Log($"[DameDotEffect] '{targetHitObject.name}' không có IDamage, bỏ qua áp DOT.");
            return;
        }

        GameObject owner = ((Component)damageable).gameObject;

        DameDotEffect existing = owner.GetComponent<DameDotEffect>();
        if (existing != null)
        {
            existing.Refresh(source, config, damageable);
        }
        else
        {
            DameDotEffect effect = owner.AddComponent<DameDotEffect>();
            effect.StartEffect(source, config, damageable);
        }
    }

    private void StartEffect(GameObject source, DameDotConfig config, IDamage damageable)
    {
        this.source = source;
        this.damageable = damageable;
        damagePerTick = config.damagePerTick;
        tickInterval = Mathf.Max(0.05f, config.tickInterval);
        remainingDuration = config.dotDuration;

        StartCoroutine(TickRoutine());
    }

    /// <summary>
    /// Dính DOT thêm lần nữa trong lúc đang dính dở - chỉ cập nhật lại thông số + reset thời gian còn lại
    /// về full dotDuration mới nhất. Coroutine TickRoutine() đang chạy vẫn tiếp tục dùng field này,
    /// KHÔNG start thêm coroutine mới.
    /// </summary>
    public void Refresh(GameObject source, DameDotConfig config, IDamage damageable)
    {
        this.source = source;
        this.damageable = damageable;
        damagePerTick = config.damagePerTick;
        tickInterval = Mathf.Max(0.05f, config.tickInterval);
        remainingDuration = config.dotDuration;
    }

    private IEnumerator TickRoutine()
    {
        while (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(tickInterval);

            // đọc lại tickInterval sau khi wait, phòng trường hợp Refresh() đổi interval giữa chừng
            remainingDuration -= tickInterval;

            if (damageable != null)
            {
                damageable.TakeDamage(damagePerTick, source);
            }
        }

        Destroy(this);
    }
}
