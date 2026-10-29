using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerProgressionService : MonoBehaviour
{
    private const float AutoSaveInterval = 30f;
    private const float TransitionFadeDuration = 0.4f;
    private const string MainGameSceneName = "CoreCombatDemo";

    private static bool zoneTransitionPending;
    private static bool pendingZoneDataApplied;
    private static string pendingDestinationScene;
    private static string pendingDestinationZone;

    public static PlayerProgressionService Instance { get; private set; }
    public static bool IsZoneTransitionPending => zoneTransitionPending;
    public static bool ShouldDeferConnectedStoryDirector => zoneTransitionPending && !pendingZoneDataApplied;
    public PlayerSaveData Data { get; private set; }
    public bool IsContinueRestorePending => continueRestorePending;
    public bool IsStoryEventBatchActive => storyEventBatchDepth > 0;

    [Header("Zone Scene Transition")]
    [SerializeField] private GameObject transitionFade;

    private GameObject player;
    private HealthController playerHealth;
    private float autoSaveTimer;
    private int storyEventBatchDepth;
    private bool saveDeferred;
    private bool continueRestorePending;
    private string loadedMapSceneName;
    private bool saveDataInvalid;
    private SkillTable cachedSkillTable;
    private bool skillTableLoaded;

    private static readonly string[] SkillInputKeys =
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "space"
    };

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
        data.currentScene = "Zone1";
        data.languageCode = transition.IsReady ? transition.CurrentLanguage : "vn";
        data.AddStoredItem("HOUSE-CHEST", "Dao Găm Gỉ");
        DataManager.SetData(Tables.Player, data);
    }

    public static void PersistLanguagePreference(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return;

        if (Instance != null)
        {
            if (Instance.Data == null || Instance.saveDataInvalid) return;
            Instance.Data.languageCode = languageCode.Trim().ToLowerInvariant();
            Instance.Save();
            return;
        }

        if (!DataManager.HasSavedGame()
            || !DataManager.TryGetData(Tables.Player, out PlayerSaveData savedData)
            || savedData == null) return;

        savedData.languageCode = languageCode.Trim().ToLowerInvariant();
        DataManager.SetData(Tables.Player, savedData);
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
        bool saveFileExists = DataManager.HasSavedGame();
        bool loaded = DataManager.TryGetData(Tables.Player, out PlayerSaveData loadedData);
        loaded = loaded
            && !string.IsNullOrWhiteSpace(loadedData.currentSession)
            && !string.IsNullOrWhiteSpace(loadedData.currentZone)
            && !string.IsNullOrWhiteSpace(loadedData.currentScene);
        saveDataInvalid = saveFileExists && !loaded;
        Data = loaded ? loadedData : new PlayerSaveData();
        if (saveDataInvalid)
        {
            continueRestorePending = false;
            Debug.LogError("[PlayerProgressionService] Save file exists but could not be parsed. It will not be overwritten; start a new game explicitly to replace it.", this);
        }
        NormalizeData();
        Data.unlockedSkills.Remove("Chém cơ bản");
        for (int level = 1; level <= Data.level; level++) UnlockSkillsForLevel(level);
        NormalizeEquippedSkills();
        ApplyPlayerData();

        if (!saveFileExists && !saveDataInvalid) Save();
    }

    private void Start()
    {
        ApplyPlayerData();
        if (SceneManager.GetActiveScene().name == MainGameSceneName)
        {
            string savedMapScene = GetSavedMapSceneName();
            if (!string.IsNullOrWhiteSpace(savedMapScene))
            {
                Debug.Log($"[PlayerProgressionService] Loading saved map '{savedMapScene}' additively for currentZone '{Data.currentZone}'.", this);
                StartCoroutine(LoadSavedZoneMap(savedMapScene));
            }
            else
            {
                Debug.LogWarning(
                    $"[PlayerProgressionService] No additive map found for currentScene '{Data.currentScene}' " +
                    $"or currentZone '{Data.currentZone}'.",
                    this);
            }
        }
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
        if (Data == null || saveDataInvalid) return;
        CapturePlayerLocationAndHealth();
        CaptureCombatSnapshots();
        if (storyEventBatchDepth > 0)
        {
            saveDeferred = true;
            return;
        }

        DataManager.SetData(Tables.Player, Data);
    }

    private void CaptureCombatSnapshots()
    {
        if (Data.skillCooldowns == null) Data.skillCooldowns = new List<SkillCooldownSnapshot>();
        PlayerSkillController[] playerSkills = FindObjectsByType<PlayerSkillController>(
            FindObjectsInactive.Include);
        foreach (PlayerSkillController controller in playerSkills)
            if (controller != null) controller.CaptureCooldowns(Data);

        ZoneEnemySpawner[] spawners = FindObjectsByType<ZoneEnemySpawner>(
            FindObjectsInactive.Include);
        foreach (ZoneEnemySpawner spawner in spawners)
            if (spawner != null) spawner.CaptureBattleSnapshot(Data);
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

        bool hasNewerPlayerSnapshot = Data.manualSaveOverridesLastEventPosition
            || (Data.HasFlag("HasSavedPlayerPosition")
                && (Data.hp != lastEvent.hp
                    || !Mathf.Approximately(Data.playerX, lastEvent.playerX)
                    || !Mathf.Approximately(Data.playerY, lastEvent.playerY)));
        if (!hasNewerPlayerSnapshot)
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
        string logicalScene = !string.IsNullOrWhiteSpace(loadedMapSceneName)
            ? loadedMapSceneName
            : activeScene.name;
        if (!string.IsNullOrWhiteSpace(logicalScene)) Data.currentScene = logicalScene;
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
        if (string.IsNullOrWhiteSpace(skillName) || Data == null || Data.unlockedSkills == null)
            return false;
        return Data.unlockedSkills.Exists(unlocked =>
            string.Equals(unlocked, skillName, StringComparison.OrdinalIgnoreCase));
    }

    public SkillData GetSkillDefinition(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName)) return null;
        SkillTable table = GetSkillTable();
        return table.skills != null
            ? table.skills.Find(skill => skill != null
                && string.Equals(skill.name, skillName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    public EquippedSkillData GetEquippedSkillAtSlot(int slotIndex)
    {
        if (Data == null || Data.equippedSkills == null) return null;
        return Data.equippedSkills.Find(skill => skill != null && skill.slotIndex == slotIndex);
    }

    public int GetEquippedSkillIndex(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || Data == null || Data.equippedSkills == null)
            return -1;
        EquippedSkillData equipped = Data.equippedSkills.Find(skill => skill != null
            && string.Equals(skill.skillName, skillName, StringComparison.OrdinalIgnoreCase));
        return equipped != null ? equipped.slotIndex : -1;
    }

    public int GetEquippedSkillCount()
    {
        return Data != null && Data.equippedSkills != null ? Data.equippedSkills.Count : 0;
    }

    public bool CanEquipSkill(string skillName)
    {
        if (!HasUnlockedSkill(skillName)) return false;
        SkillData definition = GetSkillDefinition(skillName);
        return definition != null
            && !string.IsNullOrWhiteSpace(definition.activeSkillId)
            && SkillActions.Instance != null
            && SkillActions.Instance.HasRegisteredSkill(definition.activeSkillId);
    }

    public bool EquipSkill(string skillName)
    {
        if (!CanEquipSkill(skillName)) return false;
        if (GetEquippedSkillIndex(skillName) >= 0) return true;
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();
        if (Data.equippedSkills.Count >= PlayerSaveData.EquippedSkillCapacity) return false;

        bool[] occupiedSlots = new bool[PlayerSaveData.EquippedSkillCapacity];
        HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            if (entry == null) continue;
            if (entry.slotIndex >= 0 && entry.slotIndex < occupiedSlots.Length)
                occupiedSlots[entry.slotIndex] = true;
            if (!string.IsNullOrWhiteSpace(entry.inputKey)) usedKeys.Add(entry.inputKey);
        }

        int freeSlot = Array.FindIndex(occupiedSlots, occupied => !occupied);
        string inputKey = FindAvailableSkillInputKey(usedKeys);
        if (freeSlot < 0 || string.IsNullOrWhiteSpace(inputKey)) return false;

        Data.equippedSkills.Add(new EquippedSkillData
        {
            slotIndex = freeSlot,
            skillName = skillName.Trim(),
            inputKey = inputKey
        });
        NormalizeEquippedSkills();
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public bool UnequipSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || Data == null || Data.equippedSkills == null)
            return false;
        int removed = Data.equippedSkills.RemoveAll(skill => skill != null
            && string.Equals(skill.skillName, skillName, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        SyncPlayerSkillControllers();
        Save();
        return true;
    }

    public bool CycleEquippedSkillKey(string skillName)
    {
        int slotIndex = GetEquippedSkillIndex(skillName);
        EquippedSkillData equipped = GetEquippedSkillAtSlot(slotIndex);
        if (equipped == null) return false;

        string currentKey = NormalizeSkillInputKey(equipped.inputKey);
        int currentIndex = Array.IndexOf(SkillInputKeys, currentKey);
        for (int offset = 1; offset <= SkillInputKeys.Length; offset++)
        {
            int candidateIndex = (currentIndex + offset + SkillInputKeys.Length) % SkillInputKeys.Length;
            string candidate = SkillInputKeys[candidateIndex];
            bool usedByAnotherSkill = Data.equippedSkills.Exists(skill => skill != null
                && skill.slotIndex != slotIndex
                && string.Equals(NormalizeSkillInputKey(skill.inputKey), candidate, StringComparison.OrdinalIgnoreCase));
            if (usedByAnotherSkill) continue;

            equipped.inputKey = candidate;
            SyncPlayerSkillControllers();
            Save();
            return true;
        }
        return false;
    }

    private void SyncPlayerSkillControllers()
    {
        if (player == null) FindPlayer();
        if (player == null || Data == null) return;
        SkillTable table = GetSkillTable();
        PlayerSkillController[] controllers = player.GetComponentsInChildren<PlayerSkillController>(true);
        foreach (PlayerSkillController controller in controllers)
            if (controller != null) controller.SyncEquippedSkills(Data, table);
    }

    private static string NormalizeSkillInputKey(string inputKey)
    {
        if (string.IsNullOrWhiteSpace(inputKey)) return string.Empty;
        string candidate = inputKey.Trim().ToLowerInvariant();
        return Array.Exists(SkillInputKeys, key => key == candidate) ? candidate : string.Empty;
    }

    private static string FindAvailableSkillInputKey(HashSet<string> usedKeys)
    {
        foreach (string key in SkillInputKeys)
            if (usedKeys == null || !usedKeys.Contains(key)) return key;
        return string.Empty;
    }

    private void NormalizeEquippedSkills()
    {
        if (Data == null) return;
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();

        EquippedSkillData[] slots = new EquippedSkillData[PlayerSaveData.EquippedSkillCapacity];
        HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            if (entry == null || entry.slotIndex < 0 || entry.slotIndex >= slots.Length
                || slots[entry.slotIndex] != null || string.IsNullOrWhiteSpace(entry.skillName)
                || !HasUnlockedSkill(entry.skillName) || !usedNames.Add(entry.skillName.Trim()))
                continue;
            slots[entry.slotIndex] = new EquippedSkillData
            {
                slotIndex = entry.slotIndex, skillName = entry.skillName.Trim(), inputKey = entry.inputKey
            };
        }

        foreach (EquippedSkillData entry in Data.equippedSkills)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.skillName)
                || !HasUnlockedSkill(entry.skillName) || usedNames.Contains(entry.skillName.Trim()))
                continue;
            int freeSlot = Array.FindIndex(slots, slot => slot == null);
            if (freeSlot < 0) break;
            usedNames.Add(entry.skillName.Trim());
            slots[freeSlot] = new EquippedSkillData
            {
                slotIndex = freeSlot, skillName = entry.skillName.Trim(), inputKey = entry.inputKey
            };
        }

        HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Data.equippedSkills = new List<EquippedSkillData>();
        for (int i = 0; i < slots.Length; i++)
        {
            EquippedSkillData entry = slots[i];
            if (entry == null) continue;
            string key = NormalizeSkillInputKey(entry.inputKey);
            if (string.IsNullOrEmpty(key) || usedKeys.Contains(key))
                key = FindAvailableSkillInputKey(usedKeys);
            entry.inputKey = key;
            usedKeys.Add(key);
            Data.equippedSkills.Add(entry);
        }
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
        if (effectiveQtDelta > 0 && Data.GetItemAmount("Bùa Hộ Mệnh Rách") > 0)
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
        if (itemName.StartsWith("Băng Đạn", System.StringComparison.Ordinal))
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

            Debug.Log($"[PlayerProgressionService] Khải reached level {Data.level}.");
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
        if (!string.IsNullOrWhiteSpace(skill) && !HasUnlockedSkill(skill))
            Data.unlockedSkills.Add(skill);
    }

    public bool MarkEnemyDefeated(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId) || Data.defeatedEnemies.Contains(enemyId)) return false;
        Data.defeatedEnemies.Add(enemyId);
        Save();
        return true;
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
            if (entry.itemName.StartsWith("Băng Đạn", System.StringComparison.Ordinal))
                Data.rareAmmo = Mathf.Max(0, Data.rareAmmo - entry.amount);
        }
        Data.inventory.Clear();
        Data.equippedItemName = string.Empty;   // Unequip the item after it is moved into storage.
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
            if (entry.itemName.StartsWith("Băng Đạn", System.StringComparison.Ordinal))
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
        if (Data.GetItemAmount("Vải Băng Bó") <= 0 || playerHealth == null) return false;
        float missingHealth = playerHealth.MaxHealth - playerHealth.CurrentHealth;
        if (missingHealth <= 0f || !Data.RemoveItem("Vải Băng Bó")) return false;

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

    public void LoadSceneByZone(string sceneName, string zoneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || Data == null || saveDataInvalid) return;

        if (player == null) FindPlayer();
        if (player != null && playerHealth != null)
            Data.hp = Mathf.CeilToInt(playerHealth.CurrentHealth);

        Data.currentScene = sceneName;
        pendingDestinationScene = sceneName;
        pendingDestinationZone = zoneName;
        pendingZoneDataApplied = false;
        zoneTransitionPending = true;
        Data.RemoveFlag("HasSavedPlayerPosition");
        Data.manualSaveOverridesLastEventPosition = true;
        if (Data.lastEvent != null) Data.lastEvent.sceneName = sceneName;
        CaptureCombatSnapshots();
        DataManager.SetData(Tables.Player, Data);
        StartCoroutine(SwitchZoneMap(sceneName, zoneName));
    }

    private System.Collections.IEnumerator LoadSavedZoneMap(string sceneName)
    {
        yield return null;
        Scene mapScene = SceneManager.GetSceneByName(sceneName);
        if (!mapScene.IsValid() || !mapScene.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[PlayerProgressionService] Could not load map scene '{sceneName}'.", this);
                yield break;
            }
            while (!load.isDone) yield return null;
            mapScene = SceneManager.GetSceneByName(sceneName);
        }

        loadedMapSceneName = sceneName;
        SetLegacyZoneContentActive(false);
        ConnectedStoryDirector director = FindAnyObjectByType<ConnectedStoryDirector>(FindObjectsInactive.Include);
        if (director != null) director.SetMapScene(mapScene);
        Debug.Log($"[PlayerProgressionService] Additive map '{sceneName}' loaded for zone '{Data.currentZone}'.", this);
    }

    private System.Collections.IEnumerator SwitchZoneMap(string destinationScene, string destinationZone)
    {
        transitionFade = ResolveTransitionFade();
        Image fadeImage = transitionFade != null
            ? transitionFade.GetComponent<Image>() ?? transitionFade.GetComponentInChildren<Image>(true)
            : null;
        if (transitionFade != null) transitionFade.SetActive(true);
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 1f;
            fadeImage.color = color;
            fadeImage.raycastTarget = true;
        }
        yield return null;

        Scene destination = SceneManager.GetSceneByName(destinationScene);
        bool destinationIsMainScene = destinationScene == MainGameSceneName;
        if (!destinationIsMainScene && (!destination.IsValid() || !destination.isLoaded))
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[PlayerProgressionService] Could not load map scene '{destinationScene}'.", this);
                if (transitionFade != null) transitionFade.SetActive(false);
                zoneTransitionPending = false;
                yield break;
            }
            while (!load.isDone) yield return null;
            destination = SceneManager.GetSceneByName(destinationScene);
        }
        else if (destinationIsMainScene)
        {
            destination = SceneManager.GetSceneByName(MainGameSceneName);
        }

        Scene mainScene = SceneManager.GetSceneByName(MainGameSceneName);
        if (mainScene.IsValid() && mainScene.isLoaded) SceneManager.SetActiveScene(mainScene);

        string previousMap = loadedMapSceneName;
        if (!string.IsNullOrWhiteSpace(previousMap) && previousMap != destinationScene)
        {
            Scene oldMap = SceneManager.GetSceneByName(previousMap);
            if (oldMap.IsValid() && oldMap.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(oldMap);
                if (unload != null) while (!unload.isDone) yield return null;
            }
        }
        loadedMapSceneName = destinationIsMainScene ? null : destinationScene;
        SetLegacyZoneContentActive(destinationIsMainScene);

        Data.currentScene = destinationIsMainScene ? MainGameSceneName : destinationScene;
        if (!string.IsNullOrWhiteSpace(destinationZone)) Data.currentZone = destinationZone;
        Data.RemoveFlag("HasSavedPlayerPosition");
        DataManager.SetData(Tables.Player, Data);
        pendingZoneDataApplied = true;

        ConnectedStoryDirector director = FindAnyObjectByType<ConnectedStoryDirector>(FindObjectsInactive.Include);
        if (director != null)
        {
            director.enabled = true;
            director.SetMapScene(destination);
        }
        else Debug.LogError("[PlayerProgressionService] ConnectedStoryDirector was not found in the main scene.", this);

        if (fadeImage != null)
        {
            float elapsed = 0f;
            Color color = fadeImage.color;
            while (elapsed < TransitionFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = 1f - Mathf.Clamp01(elapsed / TransitionFadeDuration);
                fadeImage.color = color;
                yield return null;
            }
            color.a = 0f;
            fadeImage.color = color;
        }
        if (transitionFade != null) transitionFade.SetActive(false);
        zoneTransitionPending = false;
        pendingZoneDataApplied = false;
        pendingDestinationScene = null;
        pendingDestinationZone = null;
    }

    private string GetSavedMapSceneName()
    {
        // currentScene is the sole source for selecting the additive map.
        if (!string.IsNullOrWhiteSpace(Data.currentScene)
            && Data.currentScene.StartsWith("Zone", System.StringComparison.OrdinalIgnoreCase)
            && Application.CanStreamedLevelBeLoaded(Data.currentScene))
        {
            return Data.currentScene;
        }

        return null;
    }

    private static void SetLegacyZoneContentActive(bool active)
    {
        Scene mainScene = SceneManager.GetSceneByName(MainGameSceneName);
        if (!mainScene.IsValid() || !mainScene.isLoaded) return;

        foreach (GameObject root in mainScene.GetRootGameObjects())
        {
            if (root.name == "Zone1Main")
            {
                if (root.activeSelf != active) root.SetActive(active);
                return;
            }
        }
    }

    private System.Collections.IEnumerator FinishZoneTransitionAfterSceneLoad()
    {
        transitionFade = ResolveTransitionFade();
        Image fadeImage = transitionFade != null
            ? transitionFade.GetComponent<Image>() ?? transitionFade.GetComponentInChildren<Image>(true)
            : null;

        if (transitionFade != null) transitionFade.SetActive(true);
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 1f;
            fadeImage.color = color;
            fadeImage.raycastTarget = true;
        }

        // Render the transition overlay before changing zone state or enabling its director.
        yield return null;

        if (!string.IsNullOrWhiteSpace(pendingDestinationZone))
            Data.currentZone = pendingDestinationZone;
        Data.RemoveFlag("HasSavedPlayerPosition");
        DataManager.SetData(Tables.Player, Data);
        pendingZoneDataApplied = true;

        ConnectedStoryDirector director = FindAnyObjectByType<ConnectedStoryDirector>(FindObjectsInactive.Include);
        if (director != null)
        {
            director.enabled = true;
            float waitTime = 0f;
            while (director != null && !director.IsInitialized && waitTime < 5f)
            {
                waitTime += Time.unscaledDeltaTime;
                yield return null;
            }
            if (director != null && !director.IsInitialized)
                Debug.LogError("[PlayerProgressionService] ConnectedStoryDirector did not initialize before the zone transition timeout.", director);
        }
        else
        {
            Debug.LogError("[PlayerProgressionService] ConnectedStoryDirector was not found in the destination scene.", this);
        }

        if (fadeImage != null)
        {
            float elapsed = 0f;
            Color color = fadeImage.color;
            while (elapsed < TransitionFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = 1f - Mathf.Clamp01(elapsed / TransitionFadeDuration);
                fadeImage.color = color;
                yield return null;
            }
            color.a = 0f;
            fadeImage.color = color;
        }

        if (transitionFade != null) transitionFade.SetActive(false);
        zoneTransitionPending = false;
        pendingDestinationScene = null;
        pendingDestinationZone = null;
        pendingZoneDataApplied = false;
    }

    private GameObject ResolveTransitionFade()
    {
        if (transitionFade != null) return transitionFade;

        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "TransitionFade") return child.gameObject;
            }
        }

        GameObject canvasObject = new GameObject("ZoneTransitionCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        transitionFade = new GameObject("TransitionFade", typeof(RectTransform), typeof(Image));
        transitionFade.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = transitionFade.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = transitionFade.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        return transitionFade;
    }
    public void SetCurrentZone(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return;
        Data.currentZone = zoneName;
    }

    private SkillTable GetSkillTable()
    {
        if (!skillTableLoaded)
        {
            cachedSkillTable = DataManager.GetData<SkillTable>(Tables.Skill);
            if (cachedSkillTable.skills == null) cachedSkillTable.skills = new List<SkillData>();
            skillTableLoaded = true;
        }
        return cachedSkillTable;
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

        SyncPlayerSkillControllers();
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
        if (Data.equippedSkills == null) Data.equippedSkills = new List<EquippedSkillData>();
        if (Data.flags == null) Data.flags = new List<string>();
        if (Data.collectedWorldItems == null) Data.collectedWorldItems = new List<string>();
        if (Data.worldDrops == null) Data.worldDrops = new List<CollectedWorldItemData>();
        if (Data.completedStoryEvents == null) Data.completedStoryEvents = new List<string>();
        if (Data.storyObjectStates == null) Data.storyObjectStates = new List<StoryObjectState>();
        if (Data.pendingStorySpawnerIds == null) Data.pendingStorySpawnerIds = new List<string>();
        if (Data.battleSnapshots == null) Data.battleSnapshots = new List<SpawnerBattleSnapshot>();
        if (Data.skillCooldowns == null) Data.skillCooldowns = new List<SkillCooldownSnapshot>();
        if (string.IsNullOrWhiteSpace(Data.languageCode)) Data.languageCode = "vn";
        if (string.IsNullOrWhiteSpace(Data.currentScene)) Data.currentScene = "Zone1";
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
        Data.equippedItemName = string.Empty;   // Unequip the item after it is moved into storage.
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
        Data.equippedItemName = string.Empty;   // Unequip the item after it is moved into storage.
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
    // Called by StorageWindowUI.
    public void RevalidateEquipment()
    {
        if (Data == null || string.IsNullOrEmpty(Data.equippedItemName)) return;
        if (Data.GetItemAmount(Data.equippedItemName) > 0) return;

        Data.equippedItemName = string.Empty;   // Unequip the item after it is moved into storage.
        if (playerHealth != null)
            playerHealth.ConfigureHealth(GetMaxHealth(), Mathf.Min(playerHealth.CurrentHealth, GetMaxHealth()));
    }

    public void AdjustRareAmmoForTransfer(string itemName, int delta)
    {
        if (Data == null || string.IsNullOrEmpty(itemName) || delta == 0) return;
        if (!itemName.StartsWith("Băng Đạn", System.StringComparison.Ordinal)) return;

        if (delta > 0)
        {
            LevelTable table = GetLevelTable();
            LevelData level = table.levels != null ? FindLevel(table, Data.level) : null;
            if (level != null) Data.rareAmmo = Mathf.Min(level.maxRareAmmo, Data.rareAmmo + delta);
        }
        else Data.rareAmmo = Mathf.Max(0, Data.rareAmmo + delta);
    }
}
