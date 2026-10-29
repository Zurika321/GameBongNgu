using UnityEngine;

public enum RegistryIdType
{
    Flag,
    Item,
    Session
}

[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class RegistryIdAttribute : PropertyAttribute
{
    public RegistryIdType IdType { get; }

    public RegistryIdAttribute(RegistryIdType idType)
    {
        IdType = idType;
    }
}