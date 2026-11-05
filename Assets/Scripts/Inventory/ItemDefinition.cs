using System.Collections.Generic;
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

/// <summary>
/// Loại vũ khí dùng cho điều kiện của skill (SkillConfig > Weapon Requirement).
/// Giá trị số được serialize trong asset nên chỉ thêm giá trị mới ở cuối, không đổi số cũ.
/// </summary>
public enum WeaponType
{
    None = 0,       // chưa phân loại (vật phẩm không phải vũ khí hoặc chưa cấu hình)
    Unarmed = 1,    // tay không / đang không cầm vũ khí
    Dagger = 2,     // dao găm
    Sword = 3,      // kiếm
    Blunt = 4,      // vũ khí cùn: gậy, búa, dùi cui
    Spear = 5,      // giáo
    Axe = 6,        // rìu
    Bow = 7,        // cung
    Gun = 8,        // súng
    Staff = 9,      // trượng
    Other = 99      // vũ khí khác
}

public static class WeaponTypeInfo
{
    public static string GetLabel(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Unarmed: return "Tay không";
            case WeaponType.Dagger: return "Dao găm";
            case WeaponType.Sword: return "Kiếm";
            case WeaponType.Blunt: return "Vũ khí cùn";
            case WeaponType.Spear: return "Giáo";
            case WeaponType.Axe: return "Rìu";
            case WeaponType.Bow: return "Cung";
            case WeaponType.Gun: return "Súng";
            case WeaponType.Staff: return "Trượng";
            case WeaponType.Other: return "Vũ khí khác";
            default: return "Chưa phân loại";
        }
    }
}

[CreateAssetMenu(menuName = "Bóng Ngủ/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [RegistryId(RegistryIdType.Item)] public string itemName;
    [TextArea] public string description;
    public ItemCategory category;

    [Tooltip("Chỉ dùng khi Category = Weapon. Skill có thể yêu cầu loại vũ khí này (SkillConfig > Weapon Requirement). Vũ khí chưa chọn loại được xem là 'Other'.")]
    public WeaponType weaponType;

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
        EnsureCache();

        foreach (ItemDefinition definition in cachedDefinitions)
        {
            if (definition != null && definition.itemName == name) return definition;
        }

        return null;
    }

    /// <summary>Mọi ItemDefinition trong Resources/ItemDefinitions (dùng cho công cụ kiểm tra cấu hình).</summary>
    public static IReadOnlyList<ItemDefinition> GetAll()
    {
        EnsureCache();
        return cachedDefinitions;
    }

    /// <summary>
    /// Loại vũ khí đang cầm theo tên vật phẩm đang trang bị:
    /// trống / không tìm thấy / không phải vũ khí (giáp, bùa...) = Unarmed; vũ khí chưa gán loại = Other.
    /// </summary>
    public static WeaponType ResolveWeaponType(string equippedItemName)
    {
        ItemDefinition definition = Find(equippedItemName);
        if (definition == null || definition.category != ItemCategory.Weapon) return WeaponType.Unarmed;
        return definition.weaponType == WeaponType.None ? WeaponType.Other : definition.weaponType;
    }

    public static void ClearCache()
    {
        cachedDefinitions = null;
    }

    private static void EnsureCache()
    {
        if (cachedDefinitions == null)
            cachedDefinitions = Resources.LoadAll<ItemDefinition>("ItemDefinitions");
    }
}
