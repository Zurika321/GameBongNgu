using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerStatusHUD : MonoBehaviour
{
    private Image portrait;
    private Text statusText;
    private float refreshTimer;
    private SpriteRenderer playerRenderer;

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    public static PlayerStatusHUD Create(Canvas canvas)
    {
        if (canvas == null) return null;
        PlayerStatusHUD existing = canvas.GetComponentInChildren<PlayerStatusHUD>(true);
        if (existing != null) return existing;

        GameObject root = new GameObject("Player Status HUD",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PlayerStatusHUD));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = Vector2.zero;
        rootRect.anchoredPosition = new Vector2(18f, 18f);
        rootRect.sizeDelta = new Vector2(410f, 132f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.07f, 0.9f);
        background.raycastTarget = false;

        PlayerStatusHUD hud = root.GetComponent<PlayerStatusHUD>();
        hud.BuildContents(root.transform);
        return hud;
    }

    private void BuildContents(Transform root)
    {
        GameObject portraitObject = new GameObject("Character Portrait",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObject.transform.SetParent(root, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(12f, 0f);
        portraitRect.sizeDelta = new Vector2(108f, 108f);
        portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        GameObject textObject = new GameObject("Player Stats",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(root, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(132f, 8f);
        textRect.offsetMax = new Vector2(-10f, -8f);
        statusText = textObject.GetComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (statusText.font == null)
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 17;
        statusText.color = new Color(0.94f, 0.92f, 0.84f);
        statusText.alignment = TextAnchor.MiddleLeft;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Truncate;
        statusText.raycastTarget = false;
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < 0.12f) return;
        refreshTimer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            statusText.text = "KHÔNG TÌM THẤY PLAYER";
            portrait.enabled = false;
            return;
        }

        if (playerRenderer == null || playerRenderer.gameObject.scene != player.scene)
            playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        portrait.enabled = playerRenderer != null && playerRenderer.sprite != null;
        if (portrait.enabled)
        {
            portrait.sprite = playerRenderer.sprite;
            portrait.transform.localScale = new Vector3(playerRenderer.flipX ? -1f : 1f, 1f, 1f);
        }

        HealthController health = player.GetComponent<HealthController>();
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        int attack = progress != null && progress.Data != null ? progress.GetAttackPower() : 0;
        int defense = progress != null && progress.Data != null ? progress.GetDefensePower() : 0;
        int currentHp = health != null ? Mathf.CeilToInt(health.CurrentHealth) : 0;
        int maxHp = health != null ? Mathf.CeilToInt(health.MaxHealth)
            : progress != null && progress.Data != null ? progress.GetMaxHealth() : 0;
        string equipment = progress != null && progress.Data != null
            && !string.IsNullOrWhiteSpace(progress.Data.equippedItemName)
            ? progress.Data.equippedItemName : "Không có";

        statusText.text = $"HP: {currentHp}/{maxHp}\nTấn công: {attack}   Phòng thủ: {defense}\nTrang bị: {equipment}";
    }
}


