using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerProgressionService : MonoBehaviour
{
    private const float AutoSaveInterval = 10f;

    public static PlayerProgressionService Instance { get; private set; }
    public PlayerSaveData Data { get; private set; }
    public bool IsContinueRestorePending => continueRestorePending;
    public bool IsStoryEventBatchActive => storyEventBatchDepth > 0;

    private GameObject player;
    private HealthController playerHealth;
    private float autoSaveTimer;
    private int storyEventBatchDepth;
    private bool saveDeferred;
    private bool continueRestorePending;

    private static bool continueRestoreRequested;

    public static void BeginContinueRestore()
    {
        continueRestoreRequested = true;
    }

    public static void BeginNewGame()
    {
        continueRestoreRequested = false;
        DataManager.DeleteData(Tables.Player);
        PlayerSaveData data = new PlayerSaveData();
        // data.AddItem("Dao GÄƒm Gá»‰");
        DataManager.SetData(Tables.Player, data);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        continueRestorePending = continueRestoreRequested;
        continueRestoreRequested = false;
        bool hasSave = DataManager.LoadData(Tables.Player);
        Data = hasSave ? DataManager.GetData<PlayerSaveData>(Tables.Player) : new PlayerSaveData();
        NormalizeData();
        Data.unlockedSkills.Remove("ChÃ©m cÆ¡ báº£n");
        for (int level = 1; level <= Data.level; level++) UnlockSkillsForLevel(level);
        ApplyPlayerData();

        if (!hasSave) Save();
    }

    private void Start()
    {
        ApplyPlayerData();
    }

    private void Update()
    {
        if (player == null) FindPlayer();
        if (player != null)
        {
            Data.playerX = player.transform.position.x;
            Data.playerY = player.transform.position.y;
            if (playerHealth != null) Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
        }

        autoSaveTimer += Time.unscaledDeltaTime;
        if (autoSaveTimer >= AutoSaveInterval)
        {
            autoSaveTimer = 0f;
            Save();
        }
    }

    public void Save()
    {
        if (Data == null) return;
        CapturePlayerLocationAndHealth();
        if (storyEventBatchDepth > 0)
        {
            saveDeferred = true;
            return;
        }

        DataManager.SetData(Tables.Player, Data);
    }

    public void SaveManual()
    {
        if (Data == null) return;
        if (Data.lastEvent != null) Data.manualSaveOverridesLastEventPosition = true;
        Save();
    }

    public void BeginStoryEventBatch()
    {
        storyEventBatchDepth++;
    }

    public void EndStoryEventBatch()
    {
        if (storyEventBatchDepth <= 0)
        {
            Debug.LogError("[PlayerProgressionService] Story event batch ended without a matching begin.", this);
            return;
        }

        storyEventBatchDepth--;
        if (storyEventBatchDepth == 0 && saveDeferred)
        {
            saveDeferred = false;
            Save();
        }
    }

    public void SetLastEvent(LastEventData lastEvent)
    {
        if (Data == null || lastEvent == null) return;
        CapturePlayerLocationAndHealth();
        lastEvent.currentSession = Data.currentSession;
        if (string.IsNullOrWhiteSpace(lastEvent.sessionId))
            lastEvent.sessionId = Data.currentSession;
        lastEvent.flags = Data.flags != null ? new List<string>(Data.flags) : new List<string>();
        lastEvent.completedOneShotEvents = Data.completedStoryEvents != null
            ? new List<string>(Data.completedStoryEvents)
            : new List<string>();
        lastEvent.hp = Data.hp;
        lastEvent.playerX = Data.playerX;
        lastEvent.playerY = Data.playerY;
        lastEvent.sceneName = Data.currentScene;
        Data.lastEvent = lastEvent;
        Data.manualSaveOverridesLastEventPosition = false;
        Save();
    }

    public void RecordStoryObjectState(string objectId, bool active)
    {
        if (Data == null || string.IsNullOrWhiteSpace(objectId)) return;
        if (Data.storyObjectStates == null) Data.storyObjectStates = new List<StoryObjectState>();
        StoryObjectState state = Data.storyObjectStates.Find(entry => entry != null && entry.objectId == objectId);
        if (state == null)
        {
            state = new StoryObjectState { objectId = objectId };
            Data.storyObjectStates.Add(state);
        }
        state.active = active;
    }

    public void RestoreLastEventSnapshot(LastEventData lastEvent)
    {
        if (Data == null || lastEvent == null) return;

        if (!string.IsNullOrWhiteSpace(lastEvent.currentSession))
            Data.currentSession = lastEvent.currentSession;
        if (lastEvent.flags != null) Data.flags = new List<string>(lastEvent.flags);
        if (lastEvent.completedOneShotEvents != null)
            Data.completedStoryEvents = new List<string>(lastEvent.completedOneShotEvents);
        Data.pendingStorySpawnerIds = lastEvent.spawnedSpawnerIds != null
            ? new List<string>(lastEvent.spawnedSpawnerIds)
            : new List<string>();

        if (!Data.manualSaveOverridesLastEventPosition)
        {
            Data.hp = Mathf.Max(0, lastEvent.hp);
            Data.playerX = lastEvent.playerX;
            Data.playerY = lastEvent.playerY;
            if (string.IsNullOrWhiteSpace(Data.currentScene)
                && !string.IsNullOrWhiteSpace(lastEvent.sceneName))
                Data.currentScene = lastEvent.sceneName;
        }

        ApplyPlayerData();
    }

    public void CompleteContinueRestore()
    {
        continueRestorePending = false;
    }

    private void CapturePlayerLocationAndHealth()
    {
        if (player == null) FindPlayer();
        if (player != null)
        {
            Data.playerX = player.transform.position.x;
            Data.playerY = player.transform.position.y;
            if (playerHealth != null) Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
            Data.SetFlag("HasSavedPlayerPosition");
        }
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.name))
            Data.currentScene = activeScene.name;
    }

    public int GetAttackPower()
    {
        LevelTable table = GetLevelTable();
        LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
        int attackPower = level != null ? level.atk : 8;
        ItemDefinition equipment = ItemDefinition.Find(Data.equippedItemName);
        return attackPower + (equipment != null ? equipment.attackBonus : 0);
    }

    public int GetDefensePower()
    {
        LevelTable table = GetLevelTable();
        LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
        ItemDefinition equipment = ItemDefinition.Find(Data.equippedItemName);
        return (level != null ? level.def : 3) + (equipment != null ? equipment.defenseBonus : 0);
    }

    public int GetMaxHealth()
    {
        LevelTable table = GetLevelTable();
        LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
        ItemDefinition equipment = ItemDefinition.Find(Data.equippedItemName);
        return (level != null ? level.maxHp : 60) + (equipment != null ? equipment.maxHealthBonus : 0);
    }

    public bool ToggleEquipAtSlot(int slotIndex)
    {
        InventoryEntry entry = Data.inventory.Find(item => item.slotIndex == slotIndex);
        if (entry == null)
        {
            Debug.LogWarning($"[PlayerProgressionService] Inventory slot {slotIndex + 1} is empty.", this);
            return false;
        }

        ItemDefinition definition = ItemDefinition.Find(entry.itemName);
        if (definition == null || !definition.IsEquippable)
        {
            Debug.LogWarning($"[PlayerProgressionService] '{entry.itemName}' cannot be equipped.", this);
            return false;
        }

        int previousMaxHealth = GetMaxHealth();
        Data.equippedItemName = Data.equippedItemName == entry.itemName ? string.Empty : entry.itemName;
        int healthDelta = GetMaxHealth() - previousMaxHealth;
        if (playerHealth != null)
        {
            playerHealth.ConfigureHealth(GetMaxHealth(),
                Mathf.Clamp(playerHealth.CurrentHealth + healthDelta, 0f, GetMaxHealth()));
            Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
        }

        Save();
        return true;
    }

    public bool UseItemAtSlot(int slotIndex)
    {
        InventoryEntry entry = Data.inventory.Find(item => item.slotIndex == slotIndex);
        if (entry == null) return false;

        ItemDefinition definition = ItemDefinition.Find(entry.itemName);
        if (definition == null || definition.healAmount <= 0 || playerHealth == null
            || playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            return false;

        if (!Data.RemoveItem(entry.itemName)) return false;
        playerHealth.Heal(definition.healAmount);
        Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
        Save();
        return true;
    }

    public bool HasUnlockedSkill(string skillName)
    {
        return !string.IsNullOrWhiteSpace(skillName)
            && Data.unlockedSkills != null
            && Data.unlockedSkills.Contains(skillName);
    }

    public bool HasCollectedWorldItem(string itemId)
    {
        return Data.HasCollectedWorldItem(itemId);
    }

    public bool RecordChoice(string sessionId, string choiceId, int ltDelta, int qtDelta)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(choiceId))
        {
            Debug.LogError("[PlayerProgressionService] Session and choice IDs are required.");
            return false;
        }

        if (Data.choices.Exists(choice => choice.sessionId == sessionId)) return false;
        int effectiveQtDelta = qtDelta;
        if (effectiveQtDelta > 0 && Data.GetItemAmount("BÃ¹a Há»™ Má»‡nh RÃ¡ch") > 0)
            effectiveQtDelta = Mathf.Max(0, effectiveQtDelta - 1);
        Data.AddChoice(sessionId, choiceId, ltDelta, effectiveQtDelta);
        Save();
        return true;
    }

    public void CompleteSession(string sessionId, string nextSessionId)//, string nextZone)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(nextSessionId))
        {
            Debug.LogError("[PlayerProgressionService] Session IDs are required to advance progress.");
            return;
        }

        if (!Data.completedSessions.Contains(sessionId))
            Data.completedSessions.Add(sessionId);
        Data.currentSession = nextSessionId;
        // if (!string.IsNullOrWhiteSpace(nextZone)) Data.currentZone = nextZone;
        UpdateLastEventProgressSnapshot();
        Save();
    }

    public bool AddItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
        {
            Debug.LogError("[PlayerProgressionService] Cannot add an invalid inventory item.");
            return false;
        }

        if (!Data.AddItem(itemName, amount))
        {
            Save();
            return false;
        }
        if (itemName.StartsWith("BÄƒng Äáº¡n", System.StringComparison.Ordinal))
        {
            LevelTable table = GetLevelTable();
            LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
            if (level != null)
                Data.rareAmmo = Mathf.Min(level.maxRareAmmo, Data.rareAmmo + amount);
        }
        Save();
        return true;
    }

    public bool RemoveItem(string itemName, int amount = 1)
    {
        bool removed = Data.RemoveItem(itemName, amount);
        if (removed)
        {
            if (Data.GetItemAmount(itemName) <= 0 && Data.equippedItemName == itemName)
            {
                Data.equippedItemName = string.Empty;
                if (playerHealth != null)
                    playerHealth.ConfigureHealth(GetMaxHealth(), Mathf.Min(playerHealth.CurrentHealth, GetMaxHealth()));
            }
            Save();
        }
        return removed;
    }

    public void SetFlag(string flag)
    {
        Data.SetFlag(flag);
        UpdateLastEventProgressSnapshot();
        Save();
    }

    public bool RemoveFlag(string flag)
    {
        bool removed = Data.RemoveFlag(flag);
        if (removed)
        {
            UpdateLastEventProgressSnapshot();
            Save();
        }
        return removed;
    }

    private void UpdateLastEventProgressSnapshot()
    {
        LastEventData lastEvent = Data != null ? Data.lastEvent : null;
        if (lastEvent == null) return;
        lastEvent.currentSession = Data.currentSession;
        lastEvent.flags = Data.flags != null
            ? new List<string>(Data.flags)
            : new List<string>();
        lastEvent.completedOneShotEvents = Data.completedStoryEvents != null
            ? new List<string>(Data.completedStoryEvents)
            : new List<string>();
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        Data.exp += amount;

        LevelTable table = GetLevelTable();
        if (table.levels == null || table.levels.Count == 0)
        {
            Debug.LogError("[PlayerProgressionService] Level table is empty; experience was not applied.");
            return;
        }

        LevelData currentLevel = FindLevel(table, Data.level);
        while (currentLevel != null && Data.level < 20)
        {
            LevelData nextLevel = FindLevel(table, Data.level + 1);
            if (nextLevel == null || Data.exp < nextLevel.expRequired) break;

            int previousMaxHp = currentLevel.maxHp;
            Data.level = nextLevel.lv;
            Data.hp = Mathf.Min(nextLevel.maxHp, Data.hp + nextLevel.maxHp - previousMaxHp);
            currentLevel = nextLevel;

            UnlockSkillsForLevel(Data.level);

            Debug.Log($"[PlayerProgressionService] Kháº£i reached level {Data.level}.");
        }

        ApplyPlayerData();
        Save();
    }

    private void UnlockSkillsForLevel(int level)
    {
        string skill = null;
        switch (level)
        {
            case 1: skill = "Chém Cơ Bản"; break;
            case 2: skill = "Né Tránh Nhanh"; break;
            case 3: skill = "Đỡ Đòn"; break;
            case 4: skill = "Đòn Xuyên"; break;
            case 5: skill = "Combo 2 Đòn Liên Tiếp"; break;
            case 6: skill = "Kháng Choáng Nhẹ"; break;
            case 7: skill = "Nộ Khí"; break;
            case 8: skill = "Ném Vũ Khí Phụ"; break;
            case 9: skill = "Cường Hóa Vũ Khí Cận Chiến"; break;
            case 10: skill = "Kháng Hiệu Ứng Độc/Chảy Máu"; break;
            case 11: skill = "Đòn Chí Mạng Cận Chiến"; break;
            case 12: skill = "Phá Giáp Hoàn Toàn"; break;
            case 13: skill = "Tập Trung Tinh Thần"; break;
            case 14: skill = "Phản Đòn Hoàn Hảo"; break;
            case 15: skill = "Bộc Phát Cận Chiến"; break;
            case 16: skill = "Tinh Thông Đạn Bạc"; break;
            case 17: skill = "Ý Chí Sắt Đá"; break;
            case 18: skill = "Song Kiếm Hợp Bích"; break;
            case 19: skill = "Trực Giác Sinh Tồn"; break;
            case 20: skill = "Hoàn Thiện Bản Thân"; break;
        }
        if (!string.IsNullOrWhiteSpace(skill) && !Data.unlockedSkills.Contains(skill))
            Data.unlockedSkills.Add(skill);
    }
    public void MarkEnemyDefeated(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId)) return;
        if (!Data.defeatedEnemies.Contains(enemyId))
        {
            Data.defeatedEnemies.Add(enemyId);
            Save();
        }
    }

    public void MarkWorldItemCollected(string itemId)
    {
        Data.MarkWorldItemCollected(itemId);
        Save();
    }

    public void RegisterWorldDrop(string itemId, string itemName, int amount, Vector2 position)
    {
        if (Data.HasCollectedWorldItem(itemId) || amount <= 0 || string.IsNullOrWhiteSpace(itemId)) return;
        if (Data.worldDrops == null) Data.worldDrops = new List<CollectedWorldItemData>();
        if (Data.worldDrops.Exists(drop => drop.itemId == itemId)) return;

        Data.worldDrops.Add(new CollectedWorldItemData
        {
            itemId = itemId,
            itemName = itemName,
            amount = amount,
            x = position.x,
            y = position.y
        });
        Save();
    }

    public void StoreAll(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId)) return;
        NormalizeData();
        for (int i = Data.inventory.Count - 1; i >= 0; i--)
        {
            InventoryEntry entry = Data.inventory[i];
            Data.AddStoredItem(containerId, entry.itemName, entry.amount);
            if (entry.itemName.StartsWith("BÄƒng Äáº¡n", System.StringComparison.Ordinal))
                Data.rareAmmo = Mathf.Max(0, Data.rareAmmo - entry.amount);
        }
        Data.inventory.Clear();
        Data.equippedItemName = string.Empty;
        if (playerHealth != null)
            playerHealth.ConfigureHealth(GetMaxHealth(), Mathf.Min(playerHealth.CurrentHealth, GetMaxHealth()));
        Save();
    }

    public void WithdrawAll(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId) || Data.storageContainers == null) return;
        StorageContainerData container = Data.storageContainers.Find(c => c.containerId == containerId);
        if (container == null || container.items == null) return;

        for (int i = container.items.Count - 1; i >= 0; i--)
        {
            InventoryEntry entry = container.items[i];
            if (!Data.AddItem(entry.itemName, entry.amount)) continue;
            if (entry.itemName.StartsWith("BÄƒng Äáº¡n", System.StringComparison.Ordinal))
            {
                LevelTable table = GetLevelTable();
                LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
                if (level != null) Data.rareAmmo = Mathf.Min(level.maxRareAmmo, Data.rareAmmo + entry.amount);
            }
            container.items.RemoveAt(i);
        }
        Save();
    }

    public bool UseBandage()
    {
        if (Data.GetItemAmount("Váº£i BÄƒng BÃ³") <= 0 || playerHealth == null) return false;
        float missingHealth = playerHealth.MaxHealth - playerHealth.CurrentHealth;
        if (missingHealth <= 0f || !Data.RemoveItem("Váº£i BÄƒng BÃ³")) return false;

        playerHealth.Heal(Mathf.Min(20f, missingHealth));
        Save();
        return true;
    }

    public List<InventoryEntry> GetStoredItems(string containerId)
    {
        if (Data.storageContainers == null) return new List<InventoryEntry>();
        StorageContainerData container = Data.storageContainers.Find(c => c.containerId == containerId);
        return container != null && container.items != null
            ? container.items
            : new List<InventoryEntry>();
    }

    public void LoadScene(string sceneName, string zoneName, Vector2 arrivalPosition)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || Data == null) return;
        Save();
        Data.currentScene = sceneName;
        if (!string.IsNullOrWhiteSpace(zoneName)) Data.currentZone = zoneName;
        Data.playerX = arrivalPosition.x;
        Data.playerY = arrivalPosition.y;
        Data.SetFlag("HasSavedPlayerPosition");
        if (Data.lastEvent != null)
        {
            Data.lastEvent.sceneName = sceneName;
            Data.lastEvent.playerX = arrivalPosition.x;
            Data.lastEvent.playerY = arrivalPosition.y;
        }
        DataManager.SetData(Tables.Player, Data);
        SceneManager.LoadScene(sceneName);
    }
    public void SetCurrentZone(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return;
        Data.currentZone = zoneName;
    }

    private LevelTable GetLevelTable()
    {
        LevelTable table = DataManager.GetData<LevelTable>(Tables.Level);
        if (table.levels != null && table.levels.Count > 0) return table;
        int[] xp = { 0, 120, 280, 480, 740, 1050, 1420, 1860, 2380, 2980, 3660, 4440, 5320, 6320, 7440, 8700, 10100, 11660, 13400, 15340 };
        int[] hp = { 60, 72, 85, 98, 112, 126, 142, 158, 175, 193, 212, 232, 253, 276, 300, 326, 354, 384, 416, 450 };
        int[] atk = { 8, 10, 12, 14, 17, 19, 22, 25, 28, 31, 35, 39, 43, 48, 54, 60, 67, 74, 82, 91 };
        int[] def = { 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 15, 16, 18, 20, 22, 24, 27, 30, 33, 36 };
        int[] ammo = { 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 12 };
        table.levels = new List<LevelData>();
        for (int i = 0; i < xp.Length; i++)
            table.levels.Add(new LevelData { lv = i + 1, expRequired = xp[i], maxHp = hp[i], atk = atk[i], def = def[i], maxRareAmmo = ammo[i] });
        return table;
    }
    private static LevelData FindLevel(LevelTable table, int level)
    {
        return table.levels.Find(entry => entry.lv == level);
    }

    private void ApplyPlayerData()
    {
        FindPlayer();
        if (player == null) return;

        LevelTable table = GetLevelTable();
        LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
        if (level != null)
        {
            if (playerHealth != null)
                playerHealth.ConfigureHealth(GetMaxHealth(), Data.hp);
            Data.rareAmmo = Mathf.Clamp(Data.rareAmmo, 0, level.maxRareAmmo);
        }
        else
        {
            Debug.LogWarning($"[PlayerProgressionService] No level data found for level {Data.level}.");
        }

        if (Data.HasFlag("HasSavedPlayerPosition"))
            player.transform.position = new Vector3(Data.playerX, Data.playerY, player.transform.position.z);
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            try
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException exception)
            {
                Debug.LogError("[PlayerProgressionService] The Player tag is missing: " + exception.Message, this);
                return;
            }
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<HealthController>();
    }

    private void NormalizeData()
    {
        if (Data == null) Data = new PlayerSaveData();
        if (Data.completedSessions == null) Data.completedSessions = new List<string>();
        if (Data.defeatedBosses == null) Data.defeatedBosses = new List<string>();
        if (Data.defeatedEnemies == null) Data.defeatedEnemies = new List<string>();
        if (Data.choices == null) Data.choices = new List<ChoiceRecord>();
        if (Data.inventory == null) Data.inventory = new List<InventoryEntry>();
        if (Data.storageContainers == null) Data.storageContainers = new List<StorageContainerData>();
        if (Data.unlockedSkills == null) Data.unlockedSkills = new List<string>();
        if (Data.flags == null) Data.flags = new List<string>();
        if (Data.collectedWorldItems == null) Data.collectedWorldItems = new List<string>();
        if (Data.worldDrops == null) Data.worldDrops = new List<CollectedWorldItemData>();
        if (Data.completedStoryEvents == null) Data.completedStoryEvents = new List<string>();
        if (Data.storyObjectStates == null) Data.storyObjectStates = new List<StoryObjectState>();
        if (Data.pendingStorySpawnerIds == null) Data.pendingStorySpawnerIds = new List<string>();
        if (string.IsNullOrWhiteSpace(Data.currentScene)) Data.currentScene = "CoreCombatDemo";
        if (Data.lastEvent != null)
        {
            if (Data.lastEvent.actionTypes == null) Data.lastEvent.actionTypes = new List<string>();
            if (Data.lastEvent.spawnedSpawnerIds == null) Data.lastEvent.spawnedSpawnerIds = new List<string>();
            if (Data.lastEvent.activatedObjectIds == null) Data.lastEvent.activatedObjectIds = new List<string>();
            if (Data.lastEvent.deactivatedObjectIds == null) Data.lastEvent.deactivatedObjectIds = new List<string>();
            if (Data.lastEvent.flags == null) Data.lastEvent.flags = new List<string>();
            if (Data.lastEvent.completedOneShotEvents == null)
                Data.lastEvent.completedOneShotEvents = new List<string>();
        }
        if (Data.equippedItemName == null) Data.equippedItemName = string.Empty;
        HashSet<int> usedSlots = new HashSet<int>();
        for (int i = Data.inventory.Count - 1; i >= 0; i--)
        {
            InventoryEntry entry = Data.inventory[i];
            if (entry == null || entry.amount <= 0 || string.IsNullOrWhiteSpace(entry.itemName))
            {
                Data.inventory.RemoveAt(i);
                continue;
            }

            if (entry.slotIndex < 0 || entry.slotIndex >= PlayerSaveData.InventoryCapacity || !usedSlots.Add(entry.slotIndex))
            {
                int slot = 0;
                while (usedSlots.Contains(slot)) slot++;
                if (slot >= PlayerSaveData.InventoryCapacity)
                {
                    Data.inventory.RemoveAt(i);
                    continue;
                }

                entry.slotIndex = slot;
                usedSlots.Add(slot);
            }
        }
        if (!string.IsNullOrEmpty(Data.equippedItemName)
            && !Data.inventory.Exists(item => item.itemName == Data.equippedItemName))
            Data.equippedItemName = string.Empty;
        Data.level = Mathf.Clamp(Data.level, 1, 20);
        Data.exp = Mathf.Max(0, Data.exp);
        Data.lt = Mathf.Max(0, Data.lt);
        Data.qt = Mathf.Max(0, Data.qt);
        Data.hp = Mathf.Max(0, Data.hp);
        Data.rareAmmo = Mathf.Max(0, Data.rareAmmo);
    }

    private void OnApplicationQuit()
    {
        Data.SetFlag("HasSavedPlayerPosition");
        Save();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}





