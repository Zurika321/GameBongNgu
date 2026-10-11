using UnityEngine;

public class HealthController : MonoBehaviour, IDamage, IHealth
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Health Bar")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.2f, 0.12f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color healthBarFillColor = Color.green;
    [SerializeField] private int healthBarSortingOrder = 1001;

    [Header("Stun Bar")]
    [SerializeField] private Vector2 stunBarSize = new Vector2(1.2f, 0.08f);
    [SerializeField] private float stunBarSpacing = 0.06f;
    [SerializeField] private Color stunBarBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color stunBarFillColor = new Color(0.25f, 0.75f, 1f, 1f);

    private static Sprite sharedWhiteSprite;
    private Transform healthBarRoot;
    private SpriteRenderer healthBarFill;
    private Transform stunBarRoot;
    private SpriteRenderer stunBarFill;
    private Collider2D[] collidersBeforeDeath;
    private bool[] colliderStatesBeforeDeath;
    private bool runtimeInitialized;
    private bool lastKnownDeadState;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;
    public bool IsInvincible { get; private set; }

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        lastKnownDeadState = IsDead;
        runtimeInitialized = true;

        if (lastKnownDeadState)
        {
            SetCharacterCollidersEnabled(false);
        }

        if (showHealthBar)
        {
            CreateHealthBar();
            UpdateHealthBar();
        }
    }

    public void TakeDamage(float damage, GameObject source)
    {
        TakeDamage(damage, source, 0f);
    }

    public void TakeDamage(float damage, GameObject source, float stunTime)
    {
        if (damage <= 0f || IsDead || IsInvincible) return;

        if (GetComponent<PlayerController>() != null && PlayerProgressionService.Instance != null)
            damage = Mathf.Max(1f, damage - PlayerProgressionService.Instance.GetDefensePower());

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        UpdateHealthBar();

        if (IsDead)
        {
            SetStunProgress(0f);
            SetCharacterCollidersEnabled(false);
            if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(false);
        }
        else
        {
            PlayerController playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.ApplyHitReaction(stunTime);
            }
            else
            {
                EnemyController enemyController = GetComponent<EnemyController>();
                if (enemyController != null) enemyController.ApplyHitReaction(stunTime);
            }
        }

        lastKnownDeadState = IsDead;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || currentHealth >= maxHealth) return;

        bool wasDead = IsDead;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthBar();

        if (wasDead && !IsDead)
        {
            SetCharacterCollidersEnabled(true);
            if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(showHealthBar);
        }

        lastKnownDeadState = IsDead;
    }

    public void ConfigureHealth(float newMaxHealth, float newCurrentHealth)
    {
        bool wasDead = IsDead;
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);

        if (wasDead != IsDead)
        {
            SetCharacterCollidersEnabled(!IsDead);
            if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(!IsDead && showHealthBar);
        }
        lastKnownDeadState = IsDead;
        UpdateHealthBar();
    }

    public void SetInvincible(bool invincible)
    {
        IsInvincible = invincible;

        if (healthBarRoot != null && !IsDead)
        {
            healthBarRoot.gameObject.SetActive(!invincible);
        }
    }

    public void SetStunProgress(float normalizedRemainingTime)
    {
        if (stunBarRoot == null) return;

        bool visible = normalizedRemainingTime > 0f && !IsDead && !IsInvincible;
        stunBarRoot.gameObject.SetActive(visible);
        if (!visible || stunBarFill == null) return;

        float progress = Mathf.Clamp01(normalizedRemainingTime);
        stunBarFill.transform.localScale = new Vector3(stunBarSize.x * progress, stunBarSize.y, 1f);
        stunBarFill.transform.localPosition = new Vector3(
            -stunBarSize.x * (1f - progress) * 0.5f,
            0f,
            -0.01f);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || !runtimeInitialized) return;

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        bool isDead = IsDead;
        if (isDead != lastKnownDeadState)
        {
            SetCharacterCollidersEnabled(!isDead);
            lastKnownDeadState = isDead;
        }

        UpdateHealthBar();
    }

    private void SetCharacterCollidersEnabled(bool enabled)
    {
        if (!enabled)
        {
            collidersBeforeDeath = GetComponentsInChildren<Collider2D>(true);
            colliderStatesBeforeDeath = new bool[collidersBeforeDeath.Length];

            for (int i = 0; i < collidersBeforeDeath.Length; i++)
            {
                Collider2D characterCollider = collidersBeforeDeath[i];
                if (characterCollider == null) continue;

                colliderStatesBeforeDeath[i] = characterCollider.enabled;
                characterCollider.enabled = false;
            }

            return;
        }

        if (collidersBeforeDeath == null || colliderStatesBeforeDeath == null) return;

        for (int i = 0; i < collidersBeforeDeath.Length; i++)
        {
            if (collidersBeforeDeath[i] != null)
            {
                collidersBeforeDeath[i].enabled = colliderStatesBeforeDeath[i];
            }
        }

        collidersBeforeDeath = null;
        colliderStatesBeforeDeath = null;
    }

    private void CreateHealthBar()
    {
        healthBarRoot = new GameObject("HealthBar").transform;
        healthBarRoot.SetParent(transform, false);
        healthBarRoot.localPosition = healthBarOffset;

        CreateBarPart("Background", healthBarBackgroundColor, healthBarSize, 0f);
        healthBarFill = CreateBarPart("Fill", healthBarFillColor, healthBarSize, -0.01f);

        stunBarRoot = new GameObject("StunBar").transform;
        stunBarRoot.SetParent(healthBarRoot, false);
        stunBarRoot.localPosition = new Vector3(0f, -healthBarSize.y - stunBarSpacing, 0f);
        CreateBarPart(stunBarRoot, "Background", stunBarBackgroundColor, stunBarSize, 0f);
        stunBarFill = CreateBarPart(stunBarRoot, "Fill", stunBarFillColor, stunBarSize, -0.01f);
        stunBarRoot.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateBarPart(string objectName, Color color, Vector2 size, float localZ)
    {
        return CreateBarPart(healthBarRoot, objectName, color, size, localZ);
    }

    private SpriteRenderer CreateBarPart(Transform parent, string objectName, Color color, Vector2 size, float localZ)
    {
        GameObject barObject = new GameObject(objectName);
        barObject.transform.SetParent(parent, false);
        barObject.transform.localPosition = new Vector3(0f, 0f, localZ);
        barObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = barObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSharedWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = healthBarSortingOrder + (parent == stunBarRoot ? -2 : 0)
            + (objectName == "Fill" ? 1 : 0);
        return renderer;
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill == null) return;

        float normalizedHealth = Mathf.Clamp01(currentHealth / maxHealth);
        healthBarFill.transform.localScale = new Vector3(
            healthBarSize.x * normalizedHealth,
            healthBarSize.y,
            1f);
        healthBarFill.transform.localPosition = new Vector3(
            -healthBarSize.x * (1f - normalizedHealth) * 0.5f,
            0f,
            -0.01f);
    }

    private static Sprite GetSharedWhiteSprite()
    {
        if (sharedWhiteSprite != null) return sharedWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        sharedWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        sharedWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedWhiteSprite;
    }
}