// using System;
// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// Chứa dữ liệu context mặc định theo skill (không có animation), và dữ liệu animation theo skill + weapon.
// /// Khi skill chạy, PlayerSkillController sẽ lấy context từ đây rồi ghi đè các giá trị runtime như caster,
// /// vị trí, hướng, loại caster, ...
// /// </summary>
// [System.Serializable]
// public class SkillContextConfig
// {
//     [Header("Tên skill")]
//     [RegistryId(RegistryIdType.Skill)]public string skillName;

//     [Header("Thông số skill (runtime base)")]
//     public float damage = 10f;
//     public Vector2 size = new Vector2(1.5f, 1f);
//     public float range = 1f;
//     public float speed = 10f;
//     public float duration = 0.2f;
//     [Min(0f)] public float stunTime;
//     [Min(0f)] public float chargeTime;
//     [Min(0f)] public float endTime;
//     public bool canPierce;
//     public bool debug = true;
//     public DameDotConfig dotConfig = new DameDotConfig();
// }

// /// <summary>
// /// Animation biến thể của 1 skill theo từng vũ khí. Mỗi skill có thể có nhiều weaponName khác nhau,
// /// ví dụ: Fireball + Sword, Fireball + Hammer, Fireball + Punch.
// /// </summary>
// [System.Serializable]
// public class SkillAnimationVariant
// {
//     [Header("Skill & weapon")]
//     [RegistryId(RegistryIdType.Skill)] public string skillName;
//     [RegistryId(RegistryIdType.Item)] public string weaponName;

//     [Header("Animation set cho biến thể này")]
//     public SkillAnimationSet animationSet = new SkillAnimationSet();
// }

// public class SkillContextLibrary : MonoBehaviour
// {
//     public static SkillContextLibrary Instance { get; private set; }

//     [Header("Context mặc định theo skill")]
//     [Tooltip("Mỗi phần tử là 1 skill - không chứa animation. Dùng để sinh context cơ bản cho 1 skill.")]
//     [SerializeField] private SkillContextConfig[] skillContexts = Array.Empty<SkillContextConfig>();

//     [Header("Animation biến thể theo skill + weapon")]
//     [Tooltip("Mỗi phần tử là 1 biến thể của skill theo vũ khí: skillName + weaponName + SkillAnimationSet.")]
//     [SerializeField] private SkillAnimationVariant[] skillAnimationVariants = Array.Empty<SkillAnimationVariant>();

//     private readonly Dictionary<string, SkillContextConfig> contextBySkill = new(StringComparer.OrdinalIgnoreCase);
//     private readonly Dictionary<string, Dictionary<string, SkillAnimationSet>> animationsBySkill = new(StringComparer.OrdinalIgnoreCase);

//     private const string DefaultWeaponName = "Punch";

//     private void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }

//         Instance = this;
//         BuildDictionaries();
//     }

//     private void OnDestroy()
//     {
//         if (Instance == this)
//         {
//             Instance = null;
//         }
//     }

//     private void BuildDictionaries()
//     {
//         contextBySkill.Clear();
//         animationsBySkill.Clear();

//         if (skillContexts != null)
//         {
//             foreach (SkillContextConfig config in skillContexts)
//             {
//                 if (config == null) continue;
//                 string skillName = NormalizeName(config.skillName);
//                 if (string.IsNullOrWhiteSpace(skillName))
//                 {
//                     Debug.LogWarning("[SkillContextLibrary] Bỏ qua SkillContextConfig không có skillName.", this);
//                     continue;
//                 }

//                 if (contextBySkill.ContainsKey(skillName))
//                 {
//                     Debug.LogWarning($"[SkillContextLibrary] Trùng skillName trong mảng context: '{skillName}'. Chỉ giữ bản đầu tiên.", this);
//                     continue;
//                 }

//                 contextBySkill.Add(skillName, config);
//             }
//         }

//         if (skillAnimationVariants != null)
//         {
//             foreach (SkillAnimationVariant variant in skillAnimationVariants)
//             {
//                 if (variant == null) continue;

//                 string skillName = NormalizeName(variant.skillName);
//                 string weaponName = NormalizeName(variant.weaponName);
//                 if (string.IsNullOrWhiteSpace(skillName))
//                 {
//                     Debug.LogWarning("[SkillContextLibrary] Bỏ qua SkillAnimationVariant không có skillName.", this);
//                     continue;
//                 }

//                 if (string.IsNullOrWhiteSpace(weaponName))
//                 {
//                     Debug.LogWarning($"[SkillContextLibrary] Skill '{skillName}' có animation variant thiếu weaponName; tự mặc định về 'Punch'.", this);
//                     weaponName = DefaultWeaponName;
//                 }

//                 if (!animationsBySkill.TryGetValue(skillName, out Dictionary<string, SkillAnimationSet> byWeapon))
//                 {
//                     byWeapon = new Dictionary<string, SkillAnimationSet>(StringComparer.OrdinalIgnoreCase);
//                     animationsBySkill.Add(skillName, byWeapon);
//                 }

//                 if (byWeapon.ContainsKey(weaponName))
//                 {
//                     Debug.LogWarning($"[SkillContextLibrary] Trùng cặp skill + weapon trong mảng animation: '{skillName}' + '{weaponName}'. Chỉ giữ bản đầu tiên.", this);
//                     continue;
//                 }

//                 byWeapon.Add(weaponName, variant.animationSet != null ? variant.animationSet : new SkillAnimationSet());
//             }
//         }
//     }

//     /// <summary>
//     /// Tạo context mới cho 1 skill theo vũ khí đang cầm.
//     /// Nếu weaponName rỗng/null => mặc định 'Punch'.
//     /// Luôn trả về 1 instance mới mỗi lần gọi, vì context là runtime snapshot thay đổi theo vũ khí/điều kiện.
//     /// Animation sẽ được tìm theo skill + weapon; nếu không có thì fallback về Punch; nếu vẫn không có thì để AnimationSet rỗng.
//     /// </summary>
//     public SkillContext GetContext(string skillName, string weaponName)
//     {
//         string normalizedSkill = NormalizeName(skillName);
//         if (string.IsNullOrWhiteSpace(normalizedSkill))
//         {
//             Debug.LogWarning("[SkillContextLibrary] Không thể tạo context khi skillName trống hoặc null.");
//             return null;
//         }

//         if (!contextBySkill.TryGetValue(normalizedSkill, out SkillContextConfig config))
//         {
//             Debug.LogWarning($"[SkillContextLibrary] Không có context cho skill '{normalizedSkill}'. Hãy thêm SkillContextConfig tương ứng trong Inspector.");
//             return null;
//         }

//         SkillContext context = new SkillContext
//         {
//             Damage = config.damage,
//             Size = config.size,
//             Range = config.range,
//             Speed = config.speed,
//             Duration = config.duration,
//             StunTime = config.stunTime,
//             ChargeTime = config.chargeTime,
//             EndTime = config.endTime,
//             CanPierce = config.canPierce,
//             Debug = config.debug,
//             DotConfig = config.dotConfig != null ? config.dotConfig : new DameDotConfig(),
//             AnimationSet = null
//         };

//         string normalizedWeapon = NormalizeWeaponName(weaponName);
//         if (string.IsNullOrWhiteSpace(normalizedWeapon))
//         {
//             normalizedWeapon = DefaultWeaponName;
//         }

//         if (TryGetAnimationSet(normalizedSkill, normalizedWeapon, out SkillAnimationSet variantSet))
//         {
//             context.AnimationSet = variantSet;
//         }
//         else if (TryGetAnimationSet(normalizedSkill, DefaultWeaponName, out SkillAnimationSet punchSet))
//         {
//             context.AnimationSet = punchSet;
//             Debug.LogWarning($"[SkillContextLibrary] Không có animation cho skill '{normalizedSkill}' với vũ khí '{normalizedWeapon}'. Fallback sang 'Punch'.");
//         }
//         else
//         {
//             context.AnimationSet = new SkillAnimationSet();
//             Debug.LogWarning($"[SkillContextLibrary] Không có animation cho skill '{normalizedSkill}' với vũ khí '{normalizedWeapon}' và không có fallback 'Punch'. AnimationSet được để rỗng.");
//         }

//         return context;
//     }

//     private bool TryGetAnimationSet(string skillName, string weaponName, out SkillAnimationSet result)
//     {
//         result = null;
//         if (!animationsBySkill.TryGetValue(skillName, out Dictionary<string, SkillAnimationSet> byWeapon))
//             return false;

//         return byWeapon.TryGetValue(weaponName, out result);
//     }

//     private static string NormalizeName(string value)
//     {
//         if (string.IsNullOrWhiteSpace(value)) return string.Empty;
//         return value.Trim();
//     }

//     private static string NormalizeWeaponName(string value)
//     {
//         string normalized = NormalizeName(value);
//         if (string.IsNullOrEmpty(normalized)) return string.Empty;

//         if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase)
//             || normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
//             || normalized.Equals("empty", StringComparison.OrdinalIgnoreCase))
//         {
//             return string.Empty;
//         }

//         return normalized;
//     }
// }
