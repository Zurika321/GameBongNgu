using System;
using System.Collections.Generic;
using System.Text.Json;
namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class HideInInspector : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text) => JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { IncludeFields = true });
    }
}
public enum RegistryIdType { Item }
public class RegistryIdAttribute : Attribute { public RegistryIdAttribute(RegistryIdType type) { } }
public enum AnimState { Idle, Run, Attack, Skill, Hurt, Die, Stun }
public class CharacterAnimationGroupInfo { public string name; public string[] variants; }
// Only the Unity-owned half is doubled. Name validation/resolution and equipment routing use production source.
public sealed partial class CharacterAnimation
{
    public string unityJson;
    private Data data;
    public sealed class Data { public Header[] animations; }
    public sealed class Header { public string name; }
    public sealed class UnityAnimationHeaderRoot { public Header[] animations; }
    public CharacterAnimationGroupInfo[] Groups = Array.Empty<CharacterAnimationGroupInfo>();
    public Dictionary<string, string> Selected = new();
    public List<string> Warnings = new();
    public CharacterAnimationGroupInfo[] GetGroupVariantDefinitions() => Groups;
    public bool SetGroupVariant(string group, string variant) { Selected[group] = variant; return true; }
    private Header FindAnimation(string id) => Array.Find(data.animations, x => x.name == ResolveAnimationSourceName(id));
    private void Warn(string key, string text) => Warnings.Add(key);
    public void LoadRuntime() => data = new Data { animations = UnityEngine.JsonUtility.FromJson<UnityAnimationHeaderRoot>(unityJson).animations };
    public void WeaponGroup(string name) => weaponGroupName = name;
    public void WeaponMapping(string id, string name) => weaponVariantBindings.Add(new CharacterWeaponVariantBinding { weaponId = id, variantName = name });
}
