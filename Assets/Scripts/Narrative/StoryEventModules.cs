using System;
using UnityEngine;

public interface IStoryAction
{
    void Execute(StoryEventContext context);
}

public interface IStoryActionSnapshot
{
    void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem);
}

public interface IStoryCondition
{
    bool IsMet(StoryEventContext context);
}

public sealed class StoryEventContext
{
    private readonly StoryEventDirector director;
    private readonly bool deferReadableItem;

    public PlayerProgressionService Progress { get; }

    internal StoryEventContext(
        StoryEventDirector director,
        PlayerProgressionService progress,
        bool deferReadableItem)
    {
        this.director = director;
        Progress = progress;
        this.deferReadableItem = deferReadableItem;
    }

    public void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (director != null) director.SetObjectsActiveFromModule(objects, active);
    }

    public void SetFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag)) Progress?.SetFlag(flag);
    }

    public void RemoveFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag)) Progress?.RemoveFlag(flag);
    }

    public void CompleteSession(string sessionId, string nextSessionId)
    {
        if (director != null) director.CompleteSessionFromModule(sessionId, nextSessionId);
    }

    public void AddItem(string itemName, int amount)
    {
        if (director != null) director.AddItemFromModule(itemName, amount);
    }

    public void RemoveItem(string itemName, int amount)
    {
        if (director != null) director.RemoveItemFromModule(itemName, amount);
    }

    public void OpenReadableItem(string itemName)
    {
        if (director == null) return;
        if (deferReadableItem) director.DeferReadableItemFromModule(itemName);
        else director.OpenReadableItemFromModule(itemName);
    }
}

internal static class StoryEventActionSnapshotUtility
{
    internal static T[] Append<T>(T[] current, T[] additional)
    {
        if (additional == null || additional.Length == 0)
            return current ?? Array.Empty<T>();
        if (current == null || current.Length == 0)
            return (T[])additional.Clone();

        T[] combined = new T[current.Length + additional.Length];
        Array.Copy(current, 0, combined, 0, current.Length);
        Array.Copy(additional, 0, combined, current.Length, additional.Length);
        return combined;
    }
}

[Serializable]
public sealed class ActivateObjectsStoryAction : IStoryAction, IStoryActionSnapshot
{
    public GameObject[] objects = Array.Empty<GameObject>();

    public void Execute(StoryEventContext context)
    {
        context?.SetObjectsActive(objects, true);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot != null)
            snapshot.activateObjects = StoryEventActionSnapshotUtility.Append(snapshot.activateObjects, objects);
    }
}

[Serializable]
public sealed class DeactivateObjectsStoryAction : IStoryAction, IStoryActionSnapshot
{
    public GameObject[] objects = Array.Empty<GameObject>();

    public void Execute(StoryEventContext context)
    {
        context?.SetObjectsActive(objects, false);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot != null)
            snapshot.deactivateObjects = StoryEventActionSnapshotUtility.Append(snapshot.deactivateObjects, objects);
    }
}

[Serializable]
public sealed class SetFlagsStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Flag)] public string[] flags = Array.Empty<string>();

    public void Execute(StoryEventContext context)
    {
        if (context == null) return;
        foreach (string flag in flags ?? Array.Empty<string>())
            context.SetFlag(flag);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot != null)
            snapshot.flagsToSet = StoryEventActionSnapshotUtility.Append(snapshot.flagsToSet, flags);
    }
}

[Serializable]
public sealed class RemoveFlagsStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Flag)] public string[] flags = Array.Empty<string>();

    public void Execute(StoryEventContext context)
    {
        if (context == null) return;
        foreach (string flag in flags ?? Array.Empty<string>())
            context.RemoveFlag(flag);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot != null)
            snapshot.flagsToRemove = StoryEventActionSnapshotUtility.Append(snapshot.flagsToRemove, flags);
    }
}

[Serializable]
public sealed class AddItemStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Item)] public string itemName;
    [Min(1)] public int amount = 1;

    public void Execute(StoryEventContext context)
    {
        context?.AddItem(itemName, Mathf.Max(1, amount));
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(itemName)) return;
        if (string.IsNullOrWhiteSpace(snapshot.itemToAdd))
        {
            snapshot.itemToAdd = itemName;
            snapshot.itemToAddAmount = Mathf.Max(1, amount);
        }
    }
}

[Serializable]
public sealed class RemoveItemStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Item)] public string itemName;
    [Min(1)] public int amount = 1;

    public void Execute(StoryEventContext context)
    {
        context?.RemoveItem(itemName, Mathf.Max(1, amount));
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(itemName)) return;
        if (string.IsNullOrWhiteSpace(snapshot.itemToRemove))
        {
            snapshot.itemToRemove = itemName;
            snapshot.itemToRemoveAmount = Mathf.Max(1, amount);
        }
    }
}

[Serializable]
public sealed class OpenReadableItemStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Item)] public string itemName;

    public void Execute(StoryEventContext context)
    {
        context?.OpenReadableItem(itemName);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot == null || deferReadableItem || string.IsNullOrWhiteSpace(itemName)) return;
        if (string.IsNullOrWhiteSpace(snapshot.readableItemToOpen))
            snapshot.readableItemToOpen = itemName;
    }
}

[Serializable]
public sealed class CompleteSessionStoryAction : IStoryAction, IStoryActionSnapshot
{
    [RegistryId(RegistryIdType.Session)] public string sessionId;
    [RegistryId(RegistryIdType.Session)] public string nextSessionId;

    public void Execute(StoryEventContext context)
    {
        context?.CompleteSession(sessionId, nextSessionId);
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot == null || !string.IsNullOrWhiteSpace(snapshot.completeSessionId)) return;
        snapshot.completeSessionId = sessionId;
        snapshot.nextSessionId = nextSessionId;
    }
}

[Serializable]
public sealed class SpawnSpawnersStoryAction : IStoryAction, IStoryActionSnapshot
{
    public ZoneEnemySpawner[] spawners = Array.Empty<ZoneEnemySpawner>();

    public void Execute(StoryEventContext context)
    {
        foreach (ZoneEnemySpawner spawner in spawners ?? Array.Empty<ZoneEnemySpawner>())
            if (spawner != null) spawner.Spawn();
    }

    public void AppendToLegacySnapshot(StoryEventAction snapshot, bool deferReadableItem)
    {
        if (snapshot != null)
            snapshot.spawnSpawners = StoryEventActionSnapshotUtility.Append(snapshot.spawnSpawners, spawners);
    }
}

[Serializable]
public sealed class HasFlagStoryCondition : IStoryCondition
{
    [RegistryId(RegistryIdType.Flag)] public string requiredFlag;

    public bool IsMet(StoryEventContext context)
    {
        return context != null
            && context.Progress != null
            && context.Progress.Data != null
            && !string.IsNullOrWhiteSpace(requiredFlag)
            && context.Progress.Data.HasFlag(requiredFlag);
    }
}

[Serializable]
public sealed class HasItemStoryCondition : IStoryCondition
{
    [RegistryId(RegistryIdType.Item)] public string itemName;
    [Min(1)] public int minimumAmount = 1;

    public bool IsMet(StoryEventContext context)
    {
        return context != null
            && context.Progress != null
            && context.Progress.Data != null
            && !string.IsNullOrWhiteSpace(itemName)
            && context.Progress.Data.GetItemAmount(itemName) >= Mathf.Max(1, minimumAmount);
    }
}