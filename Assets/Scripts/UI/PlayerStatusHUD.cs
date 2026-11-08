using UnityEngine;
using UnityEngine.UI;

/// <summary>Popup chỉ số dùng UI đặt sẵn trong scene, mở từ nút Thông số của inventory.</summary>
public sealed class PlayerStatusHUD : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Text statusText;
    [SerializeField] private Button closeButton;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;
    [SerializeField] private GameObject player;
    [SerializeField] private LvConfig levelConfig;
    private float refreshTimer;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (statusText == null) Debug.LogWarning("[PlayerStatusHUD] Gán Status Text trong Inspector.", this);
    }
    public void SetVisible(bool visible) { gameObject.SetActive(visible); if (visible) Refresh(); }
    public void Close() => SetVisible(false);
    private void OnEnable() { refreshTimer = 0f; Refresh(); }
    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval) return;
        refreshTimer = 0f;
        Refresh();
    }
    private void Refresh()
    {
        if (statusText == null) return;
        var progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) { statusText.text = "Chưa có dữ liệu người chơi."; return; }
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null ? player.GetComponent<HealthController>() : null;
        var controller = player != null ? player.GetComponent<PlayerController>() : null;
        var data = progress.Data;
        float hp = health != null ? health.CurrentHealth : data.hp;
        float maxHp = health != null ? health.MaxHealth : progress.GetMaxHealth();
        string movement = controller != null ? (progress.ModifyStat(PlayerStatType.MoveSpeed, controller.BaseMoveSpeed) * ApplyStatus.MovementMultiplier(player)).ToString("0.##") : "—";
        string equipment = string.IsNullOrWhiteSpace(data.equippedItemName) ? "Tay không" : data.equippedItemName;
        var nextLevel = levelConfig != null ? levelConfig.GetLevelEntry(data.level + 1) : null;
        string experience = nextLevel != null ? $"{data.exp} / {nextLevel.expRequired}" : data.exp.ToString();
        statusText.text = $"CẤP {data.level}   •   EXP {experience}\n"
            + $"HP  {hp:0.#} / {maxHp:0.#}\n"
            + $"Tấn công  {progress.GetAttackPowerExact():0.##}    Phòng thủ  {progress.GetDefensePower()}\n"
            + $"Di chuyển  {movement}    Tốc đánh  ×{progress.ModifyStat(PlayerStatType.AttackSpeed, 1f):0.##}\n"
            + $"Chí mạng  {progress.GetCriticalChance():P0}    Sát thương CM  ×{progress.GetCriticalMultiplier():0.##}\n"
            + $"Kháng choáng  {Mathf.Clamp01(progress.ModifyStat(PlayerStatType.StunResistance, 0f)):P0}\n"
            + $"Kháng độc / chảy máu  {Mathf.Clamp01(progress.ModifyStat(PlayerStatType.DotResistance, 0f)):P0}\n"
            + $"Lý trí  {data.lt}    Quỷ tính  {data.qt}    Đạn hiếm  {data.rareAmmo}\n"
            + $"Trang bị  {equipment}\n"
            + $"Skill đã mở  {data.unlockedSkills?.Count ?? 0}    Đang trang bị  {data.equippedSkills?.Count ?? 0}";
        if (portrait != null && portrait.sprite == null && player != null)
        {
            var renderer = player.GetComponent<SpriteRenderer>();
            if (renderer != null) portrait.sprite = renderer.sprite;
        }
        if (portrait != null) portrait.enabled = portrait.sprite != null;
    }
    private void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(Close); }
}
