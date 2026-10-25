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
    [SerializeField] private string enemyDisplayName = "Chuột Cống Biến Dị";
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
    [SerializeField, Tooltip("Only commit defeated-enemy progress after this whole encounter is clear. Snapshots are still saved during combat.")]
    private bool saveOnBattleClear;
    [SerializeField, Tooltip("Stable ID for this encounter. Leave empty to derive it from the scene and hierarchy path.")]
    private string persistentSpawnerId;

    private readonly List<SpawnedEnemy> spawnedEnemies = new List<SpawnedEnemy>();
    private bool hasSpawned;
    private bool clearEventRaised;
    private Transform player;
    private SpawnerBattleSnapshot battleSnapshot;

    public bool IsCleared => hasSpawned && spawnedEnemies.Count == 0;
    public bool HasSpawned => hasSpawned;
    public string EventIdOnCleared => eventIdOnCleared;

    private void Update()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null) return;
        if (progress.IsContinueRestorePending) return;

        if (!hasSpawned && HasUnclearedSnapshot(progress.Data))
            Spawn();

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

            progress.BeginStoryEventBatch();
            try
            {
                EnemyBattleSnapshot savedEnemy = GetOrCreateEnemySnapshot(battleSnapshot, spawned.Id);
                bool alreadyDefeated = progress.Data.defeatedEnemies.Contains(spawned.Id)
                    || progress.Data.defeatedEnemies.Contains(MakeLegacyEnemyId(spawned.Index));
                if (!savedEnemy.dead)
                {
                    savedEnemy.dead = true;
                    savedEnemy.health = 0f;
                    savedEnemy.x = spawned.Object.transform.position.x;
                    savedEnemy.y = spawned.Object.transform.position.y;
                }

                if (!savedEnemy.rewardsGranted)
                {
                    bool recordProgress = saveOnBattleClear || (!alreadyDefeated && progress.MarkEnemyDefeated(spawned.Id));
                    if (recordProgress)
                    {
                        progress.AddExperience(GetExperienceReward());
                        SpawnDrops(spawned, progress);
                    }
                    savedEnemy.rewardsGranted = true;
                }
                if (progress.Data.skillCooldowns != null)
                    progress.Data.skillCooldowns.RemoveAll(entry => entry != null && entry.ownerId == spawned.Id);
            }
            finally
            {
                spawnedEnemies.RemoveAt(i);
                progress.Save();
                progress.EndStoryEventBatch();
            }
        }

        if (IsCleared && !clearEventRaised && !string.IsNullOrWhiteSpace(eventIdOnCleared))
        {
            MarkBattleCleared(progress);
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
            MarkBattleCleared(progress);
            if (storyEventDirector == null)
                storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
            if (storyEventDirector != null) storyEventDirector.NotifySpawnerCleared(this);
            clearEventRaised = true;
        }
    }

    public void Spawn()
    {
        SpawnInternal();
    }

    public void ReplaySpawn()
    {
        if (HasLivingEnemies()) return;
        hasSpawned = false;
        clearEventRaised = false;
        SpawnInternal();
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

    private void SpawnInternal()
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

        battleSnapshot = GetOrCreateSpawnerSnapshot(progress.Data);
        if (battleSnapshot.cleared)
        {
            hasSpawned = true;
            if (storyEventDirector == null) storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
            clearEventRaised = string.IsNullOrWhiteSpace(eventIdOnCleared)
                || (storyEventDirector != null && storyEventDirector.IsEventCompleted(eventIdOnCleared));
            return;
        }

        bool restoringStartedBattle = battleSnapshot.started;
        hasSpawned = true;
        battleSnapshot.started = true;
        battleSnapshot.sceneName = gameObject.scene.name;
        RestoreSavedDrops(progress);

        for (int i = 0; i < spawnCount; i++)
        {
            string enemyId = MakeEnemyId(i);
            string legacyEnemyId = MakeLegacyEnemyId(i);
            bool hadSavedEnemy = battleSnapshot.enemies.Exists(entry => entry != null && entry.enemyId == enemyId);
            EnemyBattleSnapshot savedEnemy = GetOrCreateEnemySnapshot(progress.Data, enemyId);
            if (restoringStartedBattle && !hadSavedEnemy)
                Debug.LogWarning($"[ZoneEnemySpawner] No saved combat snapshot exists for enemy '{enemyId}'; using prefab defaults.", this);
            if (progress.Data.defeatedEnemies.Contains(enemyId) || progress.Data.defeatedEnemies.Contains(legacyEnemyId))
            {
                savedEnemy.dead = true;
                savedEnemy.rewardsGranted = true;
                continue;
            }
            if (savedEnemy.dead && savedEnemy.rewardsGranted) continue;

            Vector3 position;
            if (savedEnemy.dead || savedEnemy.health > 0f)
                position = new Vector3(savedEnemy.x, savedEnemy.y, transform.position.z);
            else
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
                position = transform.position + new Vector3(offset.x, offset.y, 0f);
            }
            GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity, transform);
            enemy.name = enemyDisplayName + " " + (i + 1);
            HealthController healthController = enemy.GetComponent<HealthController>();
            IHealth health = enemy.GetComponent<IHealth>();
            if (savedEnemy.health > 0f && healthController == null)
                Debug.LogWarning($"[ZoneEnemySpawner] Enemy '{enemyId}' has no HealthController; saved HP cannot be restored.", enemy);
            if (savedEnemy.dead && healthController != null)
                healthController.ConfigureHealth(healthController.MaxHealth, 0f);
            else if (savedEnemy.health > 0f && healthController != null)
                healthController.ConfigureHealth(healthController.MaxHealth, savedEnemy.health);
            else if (savedEnemy.health <= 0f && health != null)
                savedEnemy.health = health.CurrentHealth;

            EnemySkillController enemySkills = enemy.GetComponentInChildren<EnemySkillController>(true);
            if (enemySkills != null) enemySkills.RestoreCooldowns(progress.Data, enemyId);
            spawnedEnemies.Add(new SpawnedEnemy(enemyId, enemy, i));
        }

        if (storyEventDirector == null) storyEventDirector = FindAnyObjectByType<StoryEventDirector>();
        if (storyEventDirector != null) storyEventDirector.NotifySpawnerSpawned(this);
        progress.Save();
    }

    public void CaptureBattleSnapshot(PlayerSaveData data)
    {
        if (data == null || battleSnapshot == null && !hasSpawned) return;
        SpawnerBattleSnapshot snapshot = battleSnapshot ?? GetOrCreateSpawnerSnapshot(data);
        if (!snapshot.started) return;
        snapshot.sceneName = gameObject.scene.name;

        foreach (SpawnedEnemy spawned in spawnedEnemies)
        {
            if (spawned.Object == null) continue;
            IHealth health = spawned.Object.GetComponent<IHealth>();
            if (health == null) continue;
            EnemyBattleSnapshot enemy = GetOrCreateEnemySnapshot(snapshot, spawned.Id);
            enemy.health = health.CurrentHealth;
            enemy.dead = health.IsDead;
            enemy.x = spawned.Object.transform.position.x;
            enemy.y = spawned.Object.transform.position.y;
            EnemySkillController skills = spawned.Object.GetComponentInChildren<EnemySkillController>(true);
            if (skills != null) skills.CaptureCooldowns(data, spawned.Id);
        }
    }

    private void MarkBattleCleared(PlayerProgressionService progress)
    {
        if (battleSnapshot == null) battleSnapshot = GetOrCreateSpawnerSnapshot(progress.Data);
        bool changed = !battleSnapshot.cleared;
        battleSnapshot.started = true;
        battleSnapshot.cleared = true;
        if (saveOnBattleClear)
        {
            if (progress.Data.defeatedEnemies == null) progress.Data.defeatedEnemies = new List<string>();
            for (int i = 0; i < spawnCount; i++)
            {
                string enemyId = MakeEnemyId(i);
                if (!progress.Data.defeatedEnemies.Contains(enemyId))
                {
                    progress.Data.defeatedEnemies.Add(enemyId);
                    changed = true;
                }
                if (progress.Data.skillCooldowns != null)
                    progress.Data.skillCooldowns.RemoveAll(entry => entry != null && entry.ownerId == enemyId);
            }
        }
        if (changed) progress.Save();
    }

    private SpawnerBattleSnapshot GetOrCreateSpawnerSnapshot(PlayerSaveData data)
    {
        if (data.battleSnapshots == null) data.battleSnapshots = new List<SpawnerBattleSnapshot>();
        string id = GetPersistentSpawnerId();
        SpawnerBattleSnapshot snapshot = data.battleSnapshots.Find(entry => entry != null && entry.spawnerId == id);
        if (snapshot == null)
        {
            snapshot = new SpawnerBattleSnapshot { spawnerId = id, sceneName = gameObject.scene.name };
            data.battleSnapshots.Add(snapshot);
        }
        if (snapshot.enemies == null) snapshot.enemies = new List<EnemyBattleSnapshot>();
        battleSnapshot = snapshot;
        return snapshot;
    }

    private bool HasUnclearedSnapshot(PlayerSaveData data)
    {
        if (data == null || data.battleSnapshots == null) return false;
        string id = GetPersistentSpawnerId();
        SpawnerBattleSnapshot snapshot = data.battleSnapshots.Find(entry => entry != null && entry.spawnerId == id);
        return snapshot != null && snapshot.started && !snapshot.cleared;
    }

    private static EnemyBattleSnapshot GetOrCreateEnemySnapshot(SpawnerBattleSnapshot snapshot, string enemyId)
    {
        if (snapshot.enemies == null) snapshot.enemies = new List<EnemyBattleSnapshot>();
        EnemyBattleSnapshot enemy = snapshot.enemies.Find(entry => entry != null && entry.enemyId == enemyId);
        if (enemy == null)
        {
            enemy = new EnemyBattleSnapshot { enemyId = enemyId };
            snapshot.enemies.Add(enemy);
        }
        return enemy;
    }

    private EnemyBattleSnapshot GetOrCreateEnemySnapshot(PlayerSaveData data, string enemyId)
        => GetOrCreateEnemySnapshot(GetOrCreateSpawnerSnapshot(data), enemyId);

    private string GetPersistentSpawnerId()
    {
        if (!string.IsNullOrWhiteSpace(persistentSpawnerId)) return persistentSpawnerId.Trim();
        List<string> path = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            path.Add(current.name + "[" + current.GetSiblingIndex() + "]");
            current = current.parent;
        }
        path.Reverse();
        return gameObject.scene.name + "/" + string.Join("/", path);
    }

    private string MakeEnemyId(int index) => GetPersistentSpawnerId() + ":" + (index + 1);
    private string MakeLegacyEnemyId(int index) => $"{sessionId}:{enemyIdPrefix}:{index + 1}";

    private void RestoreSavedDrops(PlayerProgressionService progress)
    {
        if (itemPickupPrefab == null || progress.Data.worldDrops == null) return;

        string prefix = sessionId + ":" + enemyIdPrefix + ":";
        string stablePrefix = GetPersistentSpawnerId() + ":";
        foreach (CollectedWorldItemData drop in progress.Data.worldDrops)
        {
            if (drop == null || string.IsNullOrWhiteSpace(drop.itemId)
                || (!drop.itemId.StartsWith(prefix) && !drop.itemId.StartsWith(stablePrefix))
                || progress.HasCollectedWorldItem(drop.itemId))
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
        public SpawnedEnemy(string id, GameObject gameObject, int index)
        {
            Id = id;
            Object = gameObject;
            Index = index;
        }
    }
}





