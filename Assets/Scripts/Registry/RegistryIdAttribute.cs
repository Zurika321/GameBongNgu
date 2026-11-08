using UnityEngine;

public enum RegistryIdKind
{
    Flag,
    Item,
    Session,
    Skill,
    Weapon,
    Music,
    Sfx,
    EventZone1,
    EventZone2,
    EventZone3,
    EventZone4,
    EventZone5,
    EventScene
}

// Constants preserve existing attribute syntax and allow RegistryIdType.Event.Zone1.
public static class RegistryIdType
{
    public const RegistryIdKind Flag = RegistryIdKind.Flag;
    public const RegistryIdKind Item = RegistryIdKind.Item;
    public const RegistryIdKind Session = RegistryIdKind.Session;
    public const RegistryIdKind Skill = RegistryIdKind.Skill;
    public const RegistryIdKind Weapon = RegistryIdKind.Weapon;
    public const RegistryIdKind Music = RegistryIdKind.Music;
    public const RegistryIdKind Sfx = RegistryIdKind.Sfx;
    public static class Event
    {
        public const RegistryIdKind Zone1 = RegistryIdKind.EventZone1;
        public const RegistryIdKind Zone2 = RegistryIdKind.EventZone2;
        public const RegistryIdKind Zone3 = RegistryIdKind.EventZone3;
        public const RegistryIdKind Zone4 = RegistryIdKind.EventZone4;
        public const RegistryIdKind Zone5 = RegistryIdKind.EventZone5;
        public const RegistryIdKind Scene = RegistryIdKind.EventScene;
    }
}

[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class RegistryIdAttribute : PropertyAttribute
{
    public RegistryIdKind IdType { get; }

    public RegistryIdAttribute(RegistryIdKind idType)
    {
        IdType = idType;
    }
}
