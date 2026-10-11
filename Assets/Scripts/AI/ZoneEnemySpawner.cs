using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ZoneDropDefinition
{
    public string itemName;
    [Min(1)] public int amount = 1;
    [FormerlySerializedAs("chance")]
    [Range(0f, 1f), Tooltip("Per-item drop probability from 0 to 1; this is separate from enemy EXP.")]
    public float dropChance = 0.4f;
}

public class ZoneEnemySpawner : MonoBehaviour
{
    [SerializeField] private string sessionId = "1";
    [SerializeField] private string enemyIdPrefix = "RAT";
    [SerializeField] private string enemyDisplayName = "Chuá»™t Cá»‘ng Biáº¿n Dá»‹";
    [SerializeField, Min(1)] private int spawnCount = 3;
    [SerializeField, Min(0f)] private float spawnRadius = 1.25f;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject itemPickupPrefab;
    [SerializeField] private ZoneDropDefinition[] drops = Array.Empty<ZoneDropDefinition>();
    [SerializeField, Min(0), Tooltip("EXP awarded per defeated enemy. Never store drop percentages here.")] private int experiencePerEnemy = 15;
    [SerializeField] private bool spawnWhenPlayerNearby;
    [SerializeField, Min(0.1f)] private float activationRadius = 5f;
    [SerializeField] private string requiredFlag;
    [SerializeField] private StoryEventDirector storyEventDirector;
    [SerializeField] private string eventIdOnCleared;

    private readonly List<SpawnedEnemy> spawnedEnemies = new List<SpawnedEnemy>();
    private bool hasSpawned;
    private bool clearEventRaised;
    private Transform player;

    public bool IsCleared => hasSpawned && spawnedEnemies.Count == 0;
    public bool HasSpawned => hasSpawned;
    public string EventIdOnCleared => eventIdOnCleared;

    private void Update()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null) return;
        if (progress.IsContinueRestorePending) return;

        if (spawnWhenPlayerNearby && !hasSpawned)
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            bool requirementMet = string.IsNullOrWhiteSpace(requiredFlag)
                || progress.Data.HasFlag(requiredFlag);
            if (requirementMet && player != null
                && Vector2.Distance(player.position, transform.position) <= activationRadius)
                Spawn();
        }

        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            SpawnedEnemy spawned = spawnedEnemies[i];
            if (spawned.Object == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            IHealth health = spawned.Object.GetComponent<IHealth>();
            if (health == null || !health.IsDead) continue;

            if (!spawned.ReplayOnly)
            {
                progress.MarkEnemyDefeated(spawned.Id);
                progress.AddExperience(GetExperienceReward());
                SpawnDrops(spawned, progress);
            }
            spawnedEnemies.RemoveAt(i);
        }

        if (IsCleared && !clearEventRaised && !string.IsNullOrWhiteSpace(eventIdOnCleared))
        {
            if (storyEventDirector == null)
                storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
            if (storyEventDirector == null)
                Debug.LogError($"[ZoneEnemySpawner] Cannot raise clear event '{eventIdOnCleared}': StoryEventDirector is missing.", this);
            else if (storyEventDirector.TriggerEvent(eventIdOnCleared))
            {
                clearEventRaised = true;
                storyEventDirector.NotifySpawnerCleared(this);
            }
        }
        else if (IsCleared && !clearEventRaised)
        {
            if (storyEventDirector == null)
                storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
            if (storyEventDirector != null) storyEventDirector.NotifySpawnerCleared(this);
            clearEventRaised = true;
        }
    }

    public void Spawn()
    {
        SpawnInternal(false);
    }

    public void ReplaySpawn()
    {
        if (HasLivingEnemies()) return;
        hasSpawned = false;
        clearEventRaised = false;
        SpawnInternal(true);
    }

    public bool HasLivingEnemies()
    {
        foreach (SpawnedEnemy spawned in spawnedEnemies)
        {
            if (spawned.Object == null) continue;
            IHealth health = spawned.Object.GetComponent<IHealth>();
            if (health != null && !health.IsDead) return true;
        }
        return false;
    }

    private void SpawnInternal(bool replayOnly)
    {
        if (hasSpawned) return;

        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (!string.IsNullOrWhiteSpace(requiredFlag)
            && (progress == null || !progress.Data.HasFlag(requiredFlag)))
        {
            Debug.LogWarning($"[ZoneEnemySpawner] Spawn request blocked; required story flag '{requiredFlag}' is missing.", this);
            hasSpawned = false;
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError($"[ZoneEnemySpawner] No enemy prefab is assigned for {enemyDisplayName}.", this);
            return;
        }

        if (progress == null)
        {
            Debug.LogError("[ZoneEnemySpawner] PlayerProgressionService is missing from the scene.", this);
            return;
        }

        hasSpawned = true;
        RestoreSavedDrops(progress);

        for (int i = 0; i < spawnCount; i++)
        {
            string enemyId = $"{sessionId}:{enemyIdPrefix}:{i + 1}";
            if (!replayOnly && progress.Data.defeatedEnemies.Contains(enemyId)) continue;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 position = transform.position + new Vector3(offset.x, offset.y, 0f);
            GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity, transform);
            enemy.name = enemyDisplayName + " " + (i + 1);
            spawnedEnemies.Add(new SpawnedEnemy(enemyId, enemy, i, replayOnly));
        }

        if (!replayOnly)
        {
            if (storyEventDirector == null) storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
            if (storyEventDirector != null) storyEventDirector.NotifySpawnerSpawned(this);
        }
    }

    private void RestoreSavedDrops(PlayerProgressionService progress)
    {
        if (itemPickupPrefab == null || progress.Data.worldDrops == null) return;

        string prefix = sessionId + ":" + enemyIdPrefix + ":";
        foreach (CollectedWorldItemData drop in progress.Data.worldDrops)
        {
            if (drop == null || string.IsNullOrWhiteSpace(drop.itemId)
                || !drop.itemId.StartsWith(prefix) || progress.HasCollectedWorldItem(drop.itemId))
                continue;

            GameObject pickup = Instantiate(
                itemPickupPrefab,
                new Vector3(drop.x, drop.y, 0f),
                Quaternion.identity,
                transform);
            WorldItemPickup worldItem = pickup.GetComponent<WorldItemPickup>();
            if (worldItem == null)
            {
                Debug.LogError($"[ZoneEnemySpawner] Item pickup prefab '{itemPickupPrefab.name}' has no WorldItemPickup component.", pickup);
                Destroy(pickup);
                continue;
            }
            worldItem.Configure(drop.itemId, drop.itemName, drop.amount);
        }
    }

    public void ConfigureClearEvent(StoryEventDirector director, string eventId)
    {
        storyEventDirector = director;
        eventIdOnCleared = eventId;
    }

    private int GetExperienceReward()
    {
        string id = enemyIdPrefix != null ? enemyIdPrefix.Trim().ToUpperInvariant() : string.Empty;
        string display = enemyDisplayName ?? string.Empty;
        if (id == "RAT" || display.Contains("Chuột Cống")) return 15;
        if (id == "HOUND" || display.Contains("Chó Săn Ma")) return 20;
        if (id == "SPIDER" || display.Contains("Nhện Bóng Tối")) return 20;
        if (id == "CROW" || display.Contains("Quạ Đen") || display.Contains("Quạ Xác Thối")) return 20;
        if (id == "UNDEAD" || display.Contains("Xác Sống")) return 20;
        if (id == "BAT" || display.Contains("Dơi Máu")) return 20;
        if (id == "SHADE" || display.Contains("Bóng Gương")) return 25;
        if (display.Contains("Hồn Ma")) return 0;
        return experiencePerEnemy;
    }

    private float GetDropChance(ZoneDropDefinition drop)
    {
        switch (drop.itemName)
        {
            case "Vải Rách": return 0.40f;
            case "Vải Băng Bó": return 0.35f;
            case "Nanh Chó Ma": return 0.20f;
            case "Tơ Nhện Độc": return 0.30f;
            case "Lông Quạ Dị": return 0.25f;
            case "Lông Thối": return 0.20f;
            case "Mảnh Gương Vỡ": return 0.35f;
            case "Cánh Dơi": return 0.20f;
            case "Máu Đóng Băng": return 0.15f;
            case "Vải Vụn": return 0.50f;
            case "Đồng Xu Cũ": return 0.30f;
            case "Tinh Chất Bóng Tối": return 0.25f;
            case "Da Sói Sương": return 0.30f;
            case "Rễ Cây Độc": return 0.30f;
            case "Nhựa Cổ Thụ": return 0.15f;
            case "Da Cháy Xém": return 0.25f;
            case "Mảnh Tinh Thể": return 0.40f;
            case "Quặng Quỷ": return 0.20f;
        }
        return Mathf.Clamp01(drop.dropChance);
    }

    private void SpawnDrops(SpawnedEnemy enemy, PlayerProgressionService progress)
    {
        if (drops == null || drops.Length == 0) return;
        if (itemPickupPrefab == null)
        {
            Debug.LogError($"[ZoneEnemySpawner] Cannot drop loot for {enemyDisplayName}: item pickup prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < drops.Length; i++)
        {
            ZoneDropDefinition drop = drops[i];
            if (drop == null || string.IsNullOrWhiteSpace(drop.itemName) || UnityEngine.Random.value >= GetDropChance(drop))
                continue;

            string itemId = $"{enemy.Id}:DROP:{i}";
            if (progress.HasCollectedWorldItem(itemId)) continue;
            Vector2 position = enemy.Object.transform.position;
            progress.RegisterWorldDrop(itemId, drop.itemName, drop.amount, position);
            GameObject pickup = Instantiate(itemPickupPrefab, position, Quaternion.identity, transform);
            WorldItemPickup worldItem = pickup.GetComponent<WorldItemPickup>();
            if (worldItem == null)
            {
                Debug.LogError($"[ZoneEnemySpawner] Item pickup prefab '{itemPickupPrefab.name}' has no WorldItemPickup component.", pickup);
                Destroy(pickup);
                continue;
            }

            worldItem.Configure(itemId, drop.itemName, drop.amount);
        }
    }

    private sealed class SpawnedEnemy
    {
        public string Id { get; }
        public GameObject Object { get; }
        public int Index { get; }
        public bool ReplayOnly { get; }

        public SpawnedEnemy(string id, GameObject gameObject, int index, bool replayOnly)
        {
            Id = id;
            Object = gameObject;
            Index = index;
            ReplayOnly = replayOnly;
        }
    }
}





