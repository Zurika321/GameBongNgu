using UnityEngine;

public enum ItemCategory
{
    Weapon,
    Equipment,
    Consumable,
    Ammo,
    Quest,
    Material
}

[CreateAssetMenu(menuName = "Bóng Ngủ/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public ItemCategory category;
    public Sprite icon;
    [Min(1)] public int maxStack = 99;
    [Min(0)] public int attackBonus;
    [Min(0)] public int defenseBonus;
    [Min(0)] public int maxHealthBonus;
    [Min(0)] public int healAmount;
    public bool isReadable;
    public bool canBeBurned;
    [TextArea(4, 12)] public string readingText;
    public string rarity = "Thường";
    [TextArea] public string dropSource;

    private static ItemDefinition[] cachedDefinitions;

    public bool IsEquippable => category == ItemCategory.Weapon || category == ItemCategory.Equipment;

    public static ItemDefinition Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (cachedDefinitions == null)
            cachedDefinitions = Resources.LoadAll<ItemDefinition>("ItemDefinitions");

        foreach (ItemDefinition definition in cachedDefinitions)
        {
            if (definition != null && definition.itemName == name) return definition;
        }

        return null;
    }

    public static void ClearCache()
    {
        cachedDefinitions = null;
    }
}
