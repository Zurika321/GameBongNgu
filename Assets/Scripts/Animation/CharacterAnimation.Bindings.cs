using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterAnimationNameBinding
{
    [HideInInspector] public string sourceName;
    public string animationName;
}

public sealed class CharacterAnimationNameInfo
{
    public string sourceName;
    public string animationName;
    public bool locked;
}

[Serializable]
public sealed class CharacterWeaponVariantBinding
{
    [RegistryId(RegistryIdType.Item)] public string weaponId;
    public string variantName;
}

public sealed partial class CharacterAnimation
{
    [SerializeField, HideInInspector] private List<CharacterAnimationNameBinding> animationNameBindings = new List<CharacterAnimationNameBinding>();
    [SerializeField, Tooltip("Group vũ khí trong JSON. Trống = tự nhận nhóm duy nhất có biến thể khớp tên vũ khí.")]
    private string weaponGroupName;
    [SerializeField, Tooltip("Chỉ cần khi tên biến thể trong JSON khác ID/tên vật phẩm.")]
    private List<CharacterWeaponVariantBinding> weaponVariantBindings = new List<CharacterWeaponVariantBinding>();

    public static bool IsReservedAnimationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        foreach (string reserved in Enum.GetNames(typeof(AnimState)))
            if (string.Equals(reserved, name.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(name.Trim(), "Default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.Trim(), "Death", StringComparison.OrdinalIgnoreCase);
    }

    public CharacterAnimationNameInfo[] GetAnimationNameDefinitions()
    {
        var result = new List<CharacterAnimationNameInfo>();
        if (string.IsNullOrWhiteSpace(unityJson)) return result.ToArray();
        try
        {
            var header = JsonUtility.FromJson<UnityAnimationHeaderRoot>(unityJson);
            if (header?.animations != null)
                foreach (var animation in header.animations)
                    if (animation != null && !string.IsNullOrWhiteSpace(animation.name))
                        result.Add(new CharacterAnimationNameInfo { sourceName = animation.name,
                            animationName = GetBoundAnimationName(animation.name), locked = IsReservedAnimationName(animation.name) });
        }
        catch (Exception) { }
        return result.ToArray();
    }

    private string GetBoundAnimationName(string sourceName)
    {
        if (IsReservedAnimationName(sourceName)) return sourceName;
        var binding = animationNameBindings?.Find(x => x != null && x.sourceName == sourceName);
        return !string.IsNullOrWhiteSpace(binding?.animationName) ? binding.animationName : sourceName;
    }

    private string ResolveAnimationSourceName(string name)
    {
        if (animationNameBindings != null && !IsReservedAnimationName(name))
            foreach (var binding in animationNameBindings)
                if (binding != null && !IsReservedAnimationName(binding.sourceName)
                    && string.Equals(binding.animationName, name, StringComparison.OrdinalIgnoreCase)
                    && (data?.animations != null
                        ? Array.Exists(data.animations, x => x != null && x.name == binding.sourceName)
                        : Array.Exists(GetAnimationNameDefinitions(), x => x.sourceName == binding.sourceName))) return binding.sourceName;
        return name;
    }

    // Inspector overrides are saved on the component, without rewriting the source JSON or SFX keys.
    public bool SetSkillAnimationName(string sourceName, string animationName, out string error)
    {
        error = string.Empty;
        var definitions = GetAnimationNameDefinitions();
        var source = Array.Find(definitions, x => x.sourceName == sourceName);
        animationName = animationName?.Trim();
        if (source == null) error = "Không có animation nguồn: " + sourceName;
        else if (source.locked) error = "Tên AnimState mặc định được khóa: " + sourceName;
        else if (string.IsNullOrWhiteSpace(animationName)) error = "Tên animation skill không được để trống.";
        else if (IsReservedAnimationName(animationName)) error = "Không dùng tên dành riêng cho AnimState: " + animationName;
        else if (Array.Exists(definitions, x => x.sourceName != sourceName &&
            (string.Equals(x.animationName, animationName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.sourceName, animationName, StringComparison.OrdinalIgnoreCase))))
            error = "Tên animation bị trùng: " + animationName;
        if (error.Length > 0) return false;
        if (animationNameBindings == null) animationNameBindings = new List<CharacterAnimationNameBinding>();
        animationNameBindings.RemoveAll(x => x != null && x.sourceName == sourceName);
        if (animationName != sourceName) animationNameBindings.Add(new CharacterAnimationNameBinding { sourceName = sourceName, animationName = animationName });
        return true;
    }

    public bool HasAnimation(string name)
    {
        string source = ResolveAnimationSourceName(name);
        if (data?.animations != null) return FindAnimation(name) != null;
        return Array.Exists(GetAnimationNameDefinitions(), x => string.Equals(x.sourceName, source, StringComparison.Ordinal));
    }

    public string ResolveBasicAttackAnimationName(string skillId, string configuredName)
    {
        if (!string.IsNullOrWhiteSpace(configuredName)) return configuredName.Trim();
        return !HasAnimation(skillId) && HasAnimation(nameof(AnimState.Attack)) ? nameof(AnimState.Attack) : skillId;
    }

    /// <summary>Apply equipped item to its JSON group; empty item clears only the weapon group.</summary>
    public bool SyncEquippedWeapon(string weaponId, IEnumerable<string> knownWeaponIds)
    {
        var groups = GetGroupVariantDefinitions();
        if (groups.Length == 0) return false; // Old JSON has no appearance groups.
        string item = weaponId?.Trim() ?? string.Empty;
        var mapping = weaponVariantBindings?.Find(x => x != null && string.Equals(x.weaponId?.Trim(), item, StringComparison.OrdinalIgnoreCase));
        string desired = item.Length == 0 ? string.Empty : mapping != null ? mapping.variantName?.Trim() ?? string.Empty : item;
        CharacterAnimationGroupInfo group = null;
        if (!string.IsNullOrWhiteSpace(weaponGroupName))
            group = Array.Find(groups, x => string.Equals(x.name, weaponGroupName.Trim(), StringComparison.OrdinalIgnoreCase));
        else
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (desired.Length > 0) names.Add(desired);
            if (knownWeaponIds != null) foreach (string id in knownWeaponIds) if (!string.IsNullOrWhiteSpace(id)) names.Add(id.Trim());
            if (weaponVariantBindings != null) foreach (var binding in weaponVariantBindings)
                if (!string.IsNullOrWhiteSpace(binding?.variantName)) names.Add(binding.variantName.Trim());
            foreach (var candidate in groups)
            {
                if (!Array.Exists(candidate.variants, name => names.Contains(name))) continue;
                if (group != null) { Warn("weapon-group-ambiguous", "Có nhiều GroupBlock khớp vũ khí. Chọn Weapon Group trong Inspector."); return false; }
                group = candidate;
            }
        }
        if (group == null)
        {
            if (!string.IsNullOrWhiteSpace(weaponGroupName) || item.Length > 0)
                Warn("weapon-group-missing:" + item, "Chưa xác định được GroupBlock vũ khí. Gán Weapon Group / Weapon Variant Bindings trong Inspector.");
            return false;
        }
        string variant = desired.Length == 0 ? string.Empty : Array.Find(group.variants, name => string.Equals(name, desired, StringComparison.OrdinalIgnoreCase));
        if (variant == null)
        {
            SetGroupVariant(group.name, string.Empty); // Never leave the previous weapon visible.
            Warn("weapon-variant-missing:" + item, "Không có biến thể cho vũ khí '" + item + "' trong '" + group.name + "'. Gán Weapon Variant Bindings.");
            return false;
        }
        return SetGroupVariant(group.name, variant);
    }
}
