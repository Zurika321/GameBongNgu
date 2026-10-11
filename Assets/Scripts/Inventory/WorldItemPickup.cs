using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup : MonoBehaviour
{
    private const float BobHeight = 0.12f;
    private const float BobSpeed = 2f;
    private const float ShadowOffset = 0.28f;
    private static Sprite shadowSprite;

    [SerializeField] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField] private ItemDefinition itemDefinition;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;

    private bool collected;
    private Vector3 initialLocalPosition;
    private Transform shadowTransform;
    private float bobPhase;

    public string ItemName => itemName;
    public int Amount => amount;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        bobPhase = Random.value * Mathf.PI * 2f;

        if (itemDefinition != null)
        {
            itemName = itemDefinition.itemName;
            SpriteRenderer itemRenderer = GetComponent<SpriteRenderer>();
            if (itemRenderer != null && itemDefinition.icon != null) itemRenderer.sprite = itemDefinition.icon;
        }

        CreateGroundShadow();

        Collider2D itemCollider = GetComponent<Collider2D>();
        itemCollider.isTrigger = true;

        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null && progress.HasCollectedWorldItem(itemId))
        {
            collected = true;
            gameObject.SetActive(false);
            return;
        }

        if (spawnChance < 1f && Random.value > spawnChance)
        {
            collected = true;
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (collected || GameplayPause.IsPaused) return;

        float phase = Time.time * BobSpeed + bobPhase;
        float bobOffset = (Mathf.Sin(phase) + 1f) * 0.5f * BobHeight;
        transform.localPosition = initialLocalPosition + Vector3.up * bobOffset;

        if (shadowTransform != null)
        {
            shadowTransform.localPosition = new Vector3(0f, -ShadowOffset - bobOffset, 0.02f);
            float scale = 1f - bobOffset / BobHeight * 0.16f;
            shadowTransform.localScale = new Vector3(0.38f * scale, 0.38f * scale, 1f);
        }
    }

    private void CreateGroundShadow()
    {
        SpriteRenderer itemRenderer = GetComponent<SpriteRenderer>();
        if (itemRenderer == null) return;

        GameObject shadow = new GameObject("Ground Shadow");
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = new Vector3(0f, -ShadowOffset, 0.02f);
        shadow.transform.localScale = new Vector3(0.38f, 0.38f, 1f);

        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = GetShadowSprite();
        renderer.color = new Color(0f, 0f, 0f, 0.32f);
        renderer.sortingLayerID = itemRenderer.sortingLayerID;
        renderer.sortingOrder = itemRenderer.sortingOrder - 1;
        renderer.maskInteraction = itemRenderer.maskInteraction;
        shadowTransform = shadow.transform;
    }

    private static Sprite GetShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;

        const int width = 32;
        const int height = 16;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Item Shadow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                float normalizedY = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                float distance = normalizedX * normalizedX + normalizedY * normalizedY;
                float alpha = Mathf.Clamp01((1f - distance) * 4f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 16f);
        shadowSprite.name = "Runtime Item Shadow";
        shadowSprite.hideFlags = HideFlags.HideAndDontSave;
        return shadowSprite;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (player == null || progress == null) return;
        if (progress.HasCollectedWorldItem(itemId))
        {
            collected = true;
            gameObject.SetActive(false);
            return;
        }

        if (!progress.AddItem(itemName, amount)) return;

        collected = true;
        progress.MarkWorldItemCollected(itemId);
        gameObject.SetActive(false);
    }

    public void Configure(string newItemId, string newItemName, int newAmount, ItemDefinition definition = null)
    {
        itemId = newItemId;
        itemDefinition = definition != null ? definition : ItemDefinition.Find(newItemName);
        itemName = itemDefinition != null ? itemDefinition.itemName : newItemName;
        amount = Mathf.Max(1, newAmount);
        spawnChance = 1f;

        if (itemDefinition != null)
        {
            SpriteRenderer itemRenderer = GetComponent<SpriteRenderer>();
            if (itemRenderer != null && itemDefinition.icon != null) itemRenderer.sprite = itemDefinition.icon;
        }
    }

}
