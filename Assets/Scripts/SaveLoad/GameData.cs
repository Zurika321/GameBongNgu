using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  TÊN BẢNG (dùng cho LoadData / SetData / GetData)
// =====================================================================
public static class Tables
{
    public const string Session = "Session";   // Chi Tiet Session
    public const string Level   = "Level";     // He Thong Level
    public const string Monster = "Monster";   // Database Quai
    public const string Boss    = "Boss";      // Database Boss
    public const string Item    = "Item";      // Database Item
    public const string NPC     = "NPC";       // Database NPC
    public const string Skill   = "Skill";     // Cay Ky Nang
    public const string Zone    = "Zone";      // So Do Zone
    public const string Player  = "Player";    // Tiến trình người chơi (file save)
}

// =====================================================================
//  DATA THIẾT KẾ (đọc từ Resources/Data/*.json, sinh từ file Excel)
// =====================================================================
[Serializable]
public class ChoiceData
{
    public string id;      // "A", "B", "A1"...
    public string text;
    public int lt;         // delta Lý Trí
    public int qt;         // delta Quỷ Tính
}

[Serializable]
public class SessionData
{
    public string id;              // "1", "2A", "2B", "13C"...
    public string name;
    public string scenery;
    public string mission;
    public string monsters;
    public string boss;
    public string npc;
    public string drops;
    public string suggestedLevel;  // "5-6"
    public string effectText;
    public List<ChoiceData> choices = new List<ChoiceData>();
}

[Serializable]
public class LevelData
{
    public int lv;
    public int expRequired;        // EXP tích lũy
    public int maxHp;
    public int atk;
    public int def;
    public int maxRareAmmo;
    public string skill;
    public string session;
}

[Serializable]
public class MonsterData
{
    public string name;
    public string zoneSession;
    public string difficulty;
    public int hp;
    public int atk;
    public string skill;
    public string weakness;
    public string drops;
}

[Serializable]
public class BossData
{
    public string name;
    public string session;
    public string difficulty;
    public int hp;
    public int phaseCount;
    public string skills;
    public string counter;
    public string rewards;
}

[Serializable]
public class ItemData
{
    public string name;
    public string type;
    public string rarity;
    public string dropRate;
    public string source;
    public string description;
}

[Serializable]
public class NPCData
{
    public string name;
    public string role;
    public string location;
    public string sessions;
    public string dialogue;
}

[Serializable]
public class SkillData
{
    public string branch;          // Cận Chiến / Sinh Tồn / Quỷ Tính
    public string tier;            // T1..T6
    public string name;
    public string unlockCondition;
    public string requires;
    public string effect;
}

[Serializable]
public class ZoneData
{
    public string zone;
    public string place;
    public string description;
    public string sessions;
    public string connections;
    public string residents;
}

// ---- Bảng bọc list (JsonUtility không lưu list ở root được) ----
[Serializable] public class SessionTable { public List<SessionData> sessions = new List<SessionData>(); }
[Serializable] public class LevelTable   { public List<LevelData>   levels   = new List<LevelData>(); }
[Serializable] public class MonsterTable { public List<MonsterData> monsters = new List<MonsterData>(); }
[Serializable] public class BossTable    { public List<BossData>    bosses   = new List<BossData>(); }
[Serializable] public class ItemTable    { public List<ItemData>    items    = new List<ItemData>(); }
[Serializable] public class NPCTable     { public List<NPCData>     npcs     = new List<NPCData>(); }
[Serializable] public class SkillTable   { public List<SkillData>   skills   = new List<SkillData>(); }
[Serializable] public class ZoneTable    { public List<ZoneData>    zones    = new List<ZoneData>(); }

// =====================================================================
//  DATA NGƯỜI CHƠI (file save) — thêm biến vào đây là tự được lưu
// =====================================================================
[Serializable]
public class InventoryEntry
{
    public string itemName;
    public int amount;
    public int slotIndex = -1;
}

[Serializable]
public class StorageContainerData
{
    public string containerId;
    public List<InventoryEntry> items = new List<InventoryEntry>();
}

[Serializable]
public class CollectedWorldItemData
{
    public string itemId;
    public string itemName;
    public int amount;
    public float x;
    public float y;
}

[Serializable]
public class ChoiceRecord
{
    public string sessionId;
    public string choiceId;
}

[Serializable]
public class StoryObjectState
{
    public string objectId;
    public bool active;
}

[Serializable]
public class EnemyBattleSnapshot
{
    public string enemyId;
    public float health;
    public float x;
    public float y;
    public bool dead;
    public bool rewardsGranted;
}

[Serializable]
public class SpawnerBattleSnapshot
{
    public string spawnerId;
    public string sceneName;
    public bool started;
    public bool cleared;
    public List<EnemyBattleSnapshot> enemies = new List<EnemyBattleSnapshot>();
}

[Serializable]
public class SkillCooldownSnapshot
{
    public string ownerId;
    public string skillId;
    public float remainingSeconds;
}

[Serializable]
public class LastEventData
{
    public string eventId;
    public string choiceId;
    public List<string> actionTypes = new List<string>();
    public List<string> spawnedSpawnerIds = new List<string>();
    public List<string> activatedObjectIds = new List<string>();
    public List<string> deactivatedObjectIds = new List<string>();
    public string dialogueProfileId;
    public bool pendingChoice;
    public bool pendingSpawn;
    public string sessionId;
    public string currentSession;
    public List<string> flags = new List<string>();
    public List<string> completedOneShotEvents = new List<string>();
    public int hp;
    public string sceneName;
    public float playerX;
    public float playerY;
}

[Serializable]
public class PlayerSaveData
{
    public const int InventoryCapacity = 24;
    // Tiến trình
    public string currentSession = "1";
    public string currentZone = "Nhà Hoang";
    public string currentScene = "Zone1";
    public float playerX;
    public float playerY;
    public bool manualSaveOverridesLastEventPosition;
    public List<string> completedSessions = new List<string>();
    public List<string> defeatedBosses = new List<string>();
    public List<string> defeatedEnemies = new List<string>();
    public List<ChoiceRecord> choices = new List<ChoiceRecord>();
    public List<CollectedWorldItemData> worldDrops = new List<CollectedWorldItemData>();

    // Chỉ số
    public int level = 1;
    public int exp = 0;
    public int hp = 60;
    public int lt = 0;             // Lý Trí
    public int qt = 0;             // Quỷ Tính
    public int rareAmmo = 0;
    public string equippedItemName = string.Empty;

    // Đồ & kỹ năng
    public List<InventoryEntry> inventory = new List<InventoryEntry>();
    public List<StorageContainerData> storageContainers = new List<StorageContainerData>();
    public List<string> unlockedSkills = new List<string>();
    public List<string> collectedWorldItems = new List<string>();

    // Cờ cốt truyện, ví dụ "NhiTrust", "DiaryRead", "HaoAlive"
    public List<string> flags = new List<string>();
    public LastEventData lastEvent;
    public List<string> completedStoryEvents = new List<string>();
    public List<StoryObjectState> storyObjectStates = new List<StoryObjectState>();
    public List<string> pendingStorySpawnerIds = new List<string>();
    public List<SpawnerBattleSnapshot> battleSnapshots = new List<SpawnerBattleSnapshot>();
    public List<SkillCooldownSnapshot> skillCooldowns = new List<SkillCooldownSnapshot>();

    // ---- Hàm tiện ích ----
    public bool AddItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0) return false;

        if (inventory == null) inventory = new List<InventoryEntry>();
        ItemDefinition definition = ItemDefinition.Find(itemName);
        int maxStack = definition != null ? Mathf.Max(1, definition.maxStack) : 99;
        if (!CanAddItem(itemName, amount, maxStack)) return false;
        int remaining = amount;

        foreach (InventoryEntry entry in inventory)
        {
            if (entry.itemName != itemName || entry.amount >= maxStack) continue;
            int add = Math.Min(remaining, maxStack - entry.amount);
            entry.amount += add;
            remaining -= add;
            if (remaining == 0) return true;
        }

        while (remaining > 0)
        {
            int slotIndex = FindEmptyInventorySlot();
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[PlayerSaveData] Inventory is full. Could not add all of '{itemName}'.");
                return false;
            }

            int stackAmount = Math.Min(remaining, maxStack);
            inventory.Add(new InventoryEntry { itemName = itemName, amount = stackAmount, slotIndex = slotIndex });
            remaining -= stackAmount;
        }

        return true;
    }

    private bool CanAddItem(string itemName, int amount, int maxStack)
    {
        int availableStackSpace = 0;
        foreach (InventoryEntry entry in inventory)
        {
            if (entry.itemName == itemName)
                availableStackSpace += Mathf.Max(0, maxStack - entry.amount);
        }

        int newStacks = Mathf.CeilToInt(Mathf.Max(0, amount - availableStackSpace) / (float)maxStack);
        return newStacks <= PlayerSaveData.InventoryCapacity - inventory.Count;
    }

    public int GetItemAmount(string itemName)
    {
        if (inventory == null) return 0;
        int total = 0;
        foreach (InventoryEntry entry in inventory)
        {
            if (entry.itemName == itemName) total += entry.amount;
        }
        return total;
    }

    public bool RemoveItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0 || inventory == null) return false;

        if (GetItemAmount(itemName) < amount) return false;

        int remaining = amount;
        for (int i = inventory.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventoryEntry entry = inventory[i];
            if (entry.itemName != itemName) continue;

            int remove = Math.Min(remaining, entry.amount);
            entry.amount -= remove;
            remaining -= remove;
            if (entry.amount == 0) inventory.RemoveAt(i);
        }
        CompactInventorySlots();
        return true;
    }

    private void CompactInventorySlots()
    {
        inventory.Sort((left, right) => left.slotIndex.CompareTo(right.slotIndex));
        for (int i = 0; i < inventory.Count; i++)
            inventory[i].slotIndex = i;
    }

    public bool MoveInventoryItem(int fromSlot, int toSlot)
    {
        if (fromSlot < 0 || fromSlot >= InventoryCapacity
            || toSlot < 0 || toSlot >= InventoryCapacity
            || inventory == null) return false;

        InventoryEntry source = inventory.Find(entry => entry.slotIndex == fromSlot);
        if (source == null) return false;
        if (fromSlot == toSlot) return true;

        InventoryEntry destination = inventory.Find(entry => entry.slotIndex == toSlot);
        if (destination == null)
        {
            source.slotIndex = toSlot;
            return true;
        }

        if (destination.itemName == source.itemName)
        {
            ItemDefinition definition = ItemDefinition.Find(source.itemName);
            int maxStack = definition != null ? definition.maxStack : 99;
            int movedAmount = Math.Min(source.amount, Math.Max(0, maxStack - destination.amount));
            if (movedAmount > 0)
            {
                destination.amount += movedAmount;
                source.amount -= movedAmount;
                if (source.amount == 0) inventory.Remove(source);
                return true;
            }
        }

        int oldSourceSlot = source.slotIndex;
        source.slotIndex = destination.slotIndex;
        destination.slotIndex = oldSourceSlot;
        return true;
    }

    public int FindEmptyInventorySlot()
    {
        if (inventory == null) inventory = new List<InventoryEntry>();
        for (int slotIndex = 0; slotIndex < InventoryCapacity; slotIndex++)
        {
            if (!inventory.Exists(entry => entry.slotIndex == slotIndex))
                return slotIndex;
        }

        return -1;
    }

    public bool HasInventorySlot(int slotIndex)
    {
        return inventory != null && inventory.Exists(entry => entry.slotIndex == slotIndex);
    }

    public void AddStoredItem(string containerId, string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(containerId) || string.IsNullOrWhiteSpace(itemName) || amount <= 0) return;
        if (storageContainers == null) storageContainers = new List<StorageContainerData>();

        StorageContainerData container = storageContainers.Find(c => c.containerId == containerId);
        if (container == null)
        {
            container = new StorageContainerData { containerId = containerId };
            storageContainers.Add(container);
        }

        if (container.items == null) container.items = new List<InventoryEntry>();
        InventoryEntry entry = container.items.Find(i => i.itemName == itemName);
        if (entry == null)
        {
            int slotIndex = 0;
            while (container.items.Exists(item => item.slotIndex == slotIndex)) slotIndex++;
            container.items.Add(new InventoryEntry { itemName = itemName, amount = amount, slotIndex = slotIndex });
        }
        else entry.amount += amount;
    }

    public int GetStoredItemAmount(string containerId, string itemName)
    {
        if (storageContainers == null) return 0;
        StorageContainerData container = storageContainers.Find(c => c.containerId == containerId);
        if (container == null || container.items == null) return 0;

        InventoryEntry entry = container.items.Find(i => i.itemName == itemName);
        return entry == null ? 0 : entry.amount;
    }

    public bool RemoveStoredItem(string containerId, string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(containerId) || string.IsNullOrWhiteSpace(itemName)
            || amount <= 0 || storageContainers == null) return false;

        StorageContainerData container = storageContainers.Find(c => c.containerId == containerId);
        if (container == null || container.items == null) return false;

        InventoryEntry entry = container.items.Find(i => i.itemName == itemName);
        if (entry == null || entry.amount < amount) return false;

        entry.amount -= amount;
        if (entry.amount == 0) container.items.Remove(entry);
        return true;
    }

    public void AddChoice(string sessionId, string choiceId, int ltDelta, int qtDelta)
    {
        if (choices == null) choices = new List<ChoiceRecord>();
        choices.Add(new ChoiceRecord { sessionId = sessionId, choiceId = choiceId });
        lt += ltDelta;
        qt += qtDelta;
    }

    public bool HasFlag(string flag) { return flags != null && flags.Contains(flag); }
    public void SetFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag)) return;
        if (flags == null) flags = new List<string>();
        if (!flags.Contains(flag)) flags.Add(flag);
    }
    public bool RemoveFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && flags != null && flags.Remove(flag);
    }

    public bool HasCollectedWorldItem(string itemId)
    {
        return collectedWorldItems != null && collectedWorldItems.Contains(itemId);
    }

    public void MarkWorldItemCollected(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (collectedWorldItems == null) collectedWorldItems = new List<string>();
        if (!collectedWorldItems.Contains(itemId)) collectedWorldItems.Add(itemId);
        if (worldDrops != null) worldDrops.RemoveAll(drop => drop.itemId == itemId);
    }
}
