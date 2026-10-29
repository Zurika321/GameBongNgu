using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class StoryEventAction
{
    public GameObject[] activateObjects = Array.Empty<GameObject>();
    public GameObject[] deactivateObjects = Array.Empty<GameObject>();
    public ZoneEnemySpawner[] spawnSpawners = Array.Empty<ZoneEnemySpawner>();
    [RegistryId(RegistryIdType.Flag)] public string[] flagsToSet = Array.Empty<string>();
    [RegistryId(RegistryIdType.Flag)] public string[] flagsToRemove = Array.Empty<string>();
    [Tooltip("Item added when this event or choice action runs.")]
    [RegistryId(RegistryIdType.Item)] public string itemToAdd;
    [Min(1), Tooltip("Quantity to add for this action.")]
    public int itemToAddAmount = 1;
    [Tooltip("Item removed when this event or choice action runs.")]
    [RegistryId(RegistryIdType.Item)] public string itemToRemove;
    [Min(1), Tooltip("Quantity to remove for this action.")]
    public int itemToRemoveAmount = 1;
    [RegistryId(RegistryIdType.Item)] public string readableItemToOpen;

    [Header("Complete Session (bỏ trống nếu không dùng)")]
    [RegistryId(RegistryIdType.Session)] public string completeSessionId;
    [RegistryId(RegistryIdType.Session)] public string nextSessionId;
}

[Serializable]
public class StoryEventChoiceAction
{
    public string choiceId;
    public StoryEventAction action = new StoryEventAction();
    [Tooltip("Khi có action hợp lệ ở đây, danh sách module thay thế action cũ phía trên.")]
    [SerializeReference] public List<IStoryAction> actions = new List<IStoryAction>();
}

[Serializable]
public class StoryEventDefinition
{
    public string eventId;
    public bool oneShot = true;
    public DemoNpcDialogueProfile dialogueProfile;
    public StoryEventAction onTriggered = new StoryEventAction();
    [Tooltip("Điều kiện phải đạt trước khi sự kiện chạy; tất cả điều kiện trong danh sách đều phải đạt.")]
    [SerializeReference] public List<IStoryCondition> conditions = new List<IStoryCondition>();
    [Tooltip("Khi có action hợp lệ ở đây, danh sách module thay thế action cũ phía trên.")]
    [SerializeReference] public List<IStoryAction> onTriggeredActions = new List<IStoryAction>();
    public StoryEventChoiceAction[] choices = Array.Empty<StoryEventChoiceAction>();
}

[DefaultExecutionOrder(-200)]
public class StoryEventDirector : MonoBehaviour
{
    private const string EventFlagPrefix = "StoryEventCompleted:";
    private const string OpenDialogueAction = "OpenDialogue";
    private const string SpawnSpawnerAction = "SpawnSpawner";
    private const string ActivateObjectsAction = "ActivateObjects";
    private const string DeactivateObjectsAction = "DeactivateObjects";

    [SerializeField] private StoryEventDefinition[] events = Array.Empty<StoryEventDefinition>();
    [SerializeField] private TMP_Text itemChangeText;

    private readonly HashSet<string> completedEvents = new HashSet<string>();
    private readonly HashSet<string> invalidConditionWarnings = new HashSet<string>();
    private DemoNpcDialogueController dialogueController;
    private string activeDialogueEventId;
    private string pendingReadableItem;
    private PlayerProgressionService progress;
    private bool startingConfiguredDialogue;
    private bool replayingLastEvent;

    public void ConfigureEvents(StoryEventDefinition[] configuredEvents)
    {
        events = configuredEvents ?? Array.Empty<StoryEventDefinition>();
    }

    private void Start()
    {
        progress = PlayerProgressionService.Instance;
        if (progress == null)
        {
            Debug.LogError("[StoryEventDirector] PlayerProgressionService is required to restore story events.", this);
            return;
        }

        if (progress.IsContinueRestorePending)
        {
            StartCoroutine(ResumeLastEventAfterContinue());
            return;
        }

        RestoreCompletedEventsFromSave();
    }

    private void OnEnable()
    {
        DemoNpcDialogueController.ChoiceSelected += HandleChoiceSelected;
        DemoNpcDialogueController.DialogueClosed += HandleDialogueClosed;
    }

    private void OnDisable()
    {
        DemoNpcDialogueController.ChoiceSelected -= HandleChoiceSelected;
        DemoNpcDialogueController.DialogueClosed -= HandleDialogueClosed;
    }

    public bool TriggerEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            Debug.LogError("[StoryEventDirector] Cannot trigger an event without an ID.", this);
            return false;
        }

        StoryEventDefinition definition = FindEvent(eventId);
        if (definition == null)
        {
            Debug.LogError($"[StoryEventDirector] Event '{eventId}' is not configured.", this);
            return false;
        }

        progress = PlayerProgressionService.Instance;
        if (progress == null)
        {
            Debug.LogError($"[StoryEventDirector] Cannot run '{eventId}': PlayerProgressionService is missing.", this);
            return false;
        }

        bool hasPendingChoice = false;
        if (definition.dialogueProfile != null
            && DemoNpcDialogueController.HasSessionChoices(definition.dialogueProfile))
        {
            if (dialogueController == null)
                dialogueController = FindAnyObjectByType<DemoNpcDialogueController>();
            if (dialogueController == null)
            {
                Debug.LogError($"[StoryEventDirector] Cannot validate choices for '{eventId}': dialogue controller is missing.", this);
                return false;
            }
            hasPendingChoice = dialogueController.HasPendingChoiceForCurrentSession(definition.dialogueProfile, progress);
        }

        if (definition.oneShot && IsEventCompleted(eventId))
        {
            if (!hasPendingChoice) return true;
            ClearCompletedEventForPendingChoice(eventId);
        }
        if (!string.IsNullOrEmpty(activeDialogueEventId))
        {
            if (activeDialogueEventId == eventId) return true;
            Debug.LogWarning($"[StoryEventDirector] Cannot start '{eventId}' while '{activeDialogueEventId}' is awaiting a choice.", this);
            return false;
        }
        if (!AreConditionsMet(definition)) return false;

        if (definition.dialogueProfile != null
            && DemoNpcDialogueController.HasSessionChoices(definition.dialogueProfile)
            && !hasPendingChoice)
        {
            if (HasRecordedChoice(definition.dialogueProfile))
            {
                CompleteEvent(definition);
                return true;
            }
            return false;
        }

        progress.BeginStoryEventBatch();
        try
        {
            StoryEventAction actionSnapshot = definition.onTriggered;
            if (HasConfiguredActions(definition.onTriggeredActions))
                actionSnapshot = ExecuteActionModules(definition.onTriggeredActions, false);
            else
                ApplyAction(definition.onTriggered);

            bool pendingChoice = false;
            if (definition.dialogueProfile != null)
            {
                if (dialogueController == null)
                    dialogueController = FindAnyObjectByType<DemoNpcDialogueController>();

                startingConfiguredDialogue = true;
                bool dialogueStarted = dialogueController != null
                    && dialogueController.StartDialogue(definition.dialogueProfile);
                startingConfiguredDialogue = false;
                if (!dialogueStarted)
                {
                    Debug.LogError($"[StoryEventDirector] Could not open the dialogue for event '{eventId}'.", this);
                    return false;
                }

                pendingChoice = dialogueController.HasPendingChoiceForCurrentSession(
                    definition.dialogueProfile, progress);
                if (pendingChoice) activeDialogueEventId = eventId;
            }

            if (!pendingChoice) CompleteEvent(definition);
            RecordLastEvent(eventId, string.Empty, actionSnapshot,
                definition.dialogueProfile, pendingChoice);
            return true;
        }
        finally
        {
            startingConfiguredDialogue = false;
            progress.EndStoryEventBatch();
        }
    }

    public bool IsEventCompleted(string eventId)
    {
        if (completedEvents.Contains(eventId)) return true;

        progress = PlayerProgressionService.Instance;
        return progress != null && (progress.Data.HasFlag(EventFlagPrefix + eventId)
            || (progress.Data.completedStoryEvents != null
                && progress.Data.completedStoryEvents.Contains(eventId)));
    }

    private void HandleChoiceSelected(DemoNpcDialogueProfile profile, DemoDialogueChoice choice)
    {
        if (choice == null) return;
        if (string.IsNullOrWhiteSpace(activeDialogueEventId))
        {
            if (profile != null)
                RecordLastEvent("Dialogue:" + GetObjectId(profile.gameObject), choice.choiceId,
                    null, null, false, profile, choice.sessionId);
            return;
        }

        StoryEventDefinition definition = FindEvent(activeDialogueEventId);
        if (definition == null || definition.dialogueProfile != profile)
        {
            activeDialogueEventId = null;
            if (profile != null)
                RecordLastEvent("Dialogue:" + GetObjectId(profile.gameObject), choice.choiceId,
                    null, null, false, profile, choice.sessionId);
            return;
        }

        foreach (StoryEventChoiceAction choiceAction in definition.choices ?? Array.Empty<StoryEventChoiceAction>())
        {
            if (choiceAction == null || choiceAction.choiceId != choice.choiceId) continue;
            StoryEventAction actionSnapshot;
            if (HasConfiguredActions(choiceAction.actions))
            {
                pendingReadableItem = string.Empty;
                actionSnapshot = ExecuteActionModules(choiceAction.actions, true);
            }
            else
            {
                if (choiceAction.action != null)
                    pendingReadableItem = choiceAction.action.readableItemToOpen;
                actionSnapshot = CreateChoiceActionSnapshot(choiceAction.action);
                ApplyAction(actionSnapshot);
            }
            CompleteEvent(definition);
            activeDialogueEventId = null;
            RecordLastEvent(definition.eventId, choice.choiceId, actionSnapshot,
                null, false, profile, choice.sessionId);
            return;
        }

        Debug.LogError(
            $"[StoryEventDirector] Event '{definition.eventId}' has no configured action for choice '{choice.choiceId}'.",
            this);
        activeDialogueEventId = null;
        RecordLastEvent(definition.eventId, choice.choiceId, null, null,
            false, profile, choice.sessionId);
    }

    private void HandleDialogueClosed(DemoNpcDialogueProfile profile)
    {
        StoryEventDefinition definition = FindEvent(activeDialogueEventId);
        if (definition != null && definition.dialogueProfile == profile)
            activeDialogueEventId = null;

        if (string.IsNullOrWhiteSpace(pendingReadableItem)) return;
        string itemName = pendingReadableItem;
        pendingReadableItem = null;
        InventoryGridUI inventory = FindAnyObjectByType<InventoryGridUI>(FindObjectsInactive.Include);
        if (inventory == null)
            Debug.LogError("[StoryEventDirector] Cannot open a book: InventoryGridUI is missing.", this);
        else if (!inventory.OpenReadableItem(itemName))
            Debug.LogError($"[StoryEventDirector] '{itemName}' is not a readable item.", this);
    }

    private void ApplyAction(StoryEventAction action)
    {
        if (action == null) return;

        SetObjectsActive(action.activateObjects, true);
        SetObjectsActive(action.deactivateObjects, false);

        foreach (string flag in action.flagsToSet ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(flag)) progress?.SetFlag(flag);
        }

        foreach (string flag in action.flagsToRemove ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(flag)) progress?.RemoveFlag(flag);
        }

        CompleteSessionOnce(action.completeSessionId, action.nextSessionId);

        if (!string.IsNullOrWhiteSpace(action.itemToAdd) && progress != null)
        {
            int amount = Mathf.Max(1, action.itemToAddAmount);
            if (progress.AddItem(action.itemToAdd, amount))
                ItemChangeNotifier.Show(itemChangeText, action.itemToAdd, amount, true);
            else
                Debug.LogWarning($"[StoryEventDirector] Could not add '{action.itemToAdd}' to the inventory.", this);
        }

        if (!string.IsNullOrWhiteSpace(action.itemToRemove) && progress != null)
        {
            int amount = Mathf.Max(1, action.itemToRemoveAmount);
            if (progress.RemoveItem(action.itemToRemove, amount))
                ItemChangeNotifier.Show(itemChangeText, action.itemToRemove, amount, false);
            else
                Debug.LogWarning($"[StoryEventDirector] Could not remove '{action.itemToRemove}' from the inventory.", this);
        }

        if (!string.IsNullOrWhiteSpace(action.readableItemToOpen))
        {
            InventoryGridUI inventory = FindAnyObjectByType<InventoryGridUI>(FindObjectsInactive.Include);
            if (inventory == null)
                Debug.LogError("[StoryEventDirector] Cannot open a book: InventoryGridUI is missing.", this);
            else if (!inventory.OpenReadableItem(action.readableItemToOpen))
                Debug.LogError($"[StoryEventDirector] '{action.readableItemToOpen}' is not a readable item.", this);
        }

        foreach (ZoneEnemySpawner spawner in action.spawnSpawners ?? Array.Empty<ZoneEnemySpawner>())
        {
            if (spawner != null) spawner.Spawn();
        }
    }

    internal PlayerProgressionService CurrentProgress => progress ?? PlayerProgressionService.Instance;

    private bool HasConfiguredActions(List<IStoryAction> actions)
    {
        if (actions == null) return false;
        foreach (IStoryAction action in actions)
            if (action != null) return true;
        return false;
    }

    private bool AreConditionsMet(StoryEventDefinition definition)
    {
        if (definition == null || definition.conditions == null || definition.conditions.Count == 0)
            return true;

        StoryEventContext context = new StoryEventContext(this, progress, false);
        foreach (IStoryCondition condition in definition.conditions)
        {
            if (condition == null)
            {
                if (invalidConditionWarnings.Add(definition.eventId ?? string.Empty))
                    Debug.LogError(
                        $"[StoryEventDirector] Event '{definition.eventId}' has a missing condition module; the event is blocked.",
                        this);
                return false;
            }

            if (!condition.IsMet(context)) return false;
        }

        return true;
    }

    private StoryEventAction ExecuteActionModules(List<IStoryAction> actions, bool deferReadableItem)
    {
        StoryEventAction snapshot = new StoryEventAction();
        StoryEventContext context = new StoryEventContext(this, progress, deferReadableItem);
        foreach (IStoryAction action in actions)
        {
            if (action == null) continue;
            action.Execute(context);
            if (action is IStoryActionSnapshot snapshotAction)
                snapshotAction.AppendToLegacySnapshot(snapshot, deferReadableItem);
        }
        return snapshot;
    }

    private static StoryEventAction CreateChoiceActionSnapshot(StoryEventAction source)
    {
        if (source == null) return null;
        return new StoryEventAction
        {
            activateObjects = source.activateObjects,
            deactivateObjects = source.deactivateObjects,
            spawnSpawners = source.spawnSpawners,
            flagsToSet = source.flagsToSet,
            flagsToRemove = source.flagsToRemove,
            itemToAdd = source.itemToAdd,
            itemToAddAmount = source.itemToAddAmount,
            itemToRemove = source.itemToRemove,
            itemToRemoveAmount = source.itemToRemoveAmount,
            readableItemToOpen = string.Empty,
            completeSessionId = source.completeSessionId,
            nextSessionId = source.nextSessionId
        };
    }

    internal void SetObjectsActiveFromModule(GameObject[] objects, bool active)
    {
        SetObjectsActive(objects, active);
    }

    internal void CompleteSessionFromModule(string sessionId, string nextSessionId)
    {
        CompleteSessionOnce(sessionId, nextSessionId);
    }

    internal void AddItemFromModule(string itemName, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemName) || progress == null) return;
        amount = Mathf.Max(1, amount);
        if (progress.AddItem(itemName, amount))
            ItemChangeNotifier.Show(itemChangeText, itemName, amount, true);
        else
            Debug.LogWarning($"[StoryEventDirector] Could not add '{itemName}' to the inventory.", this);
    }

    internal void RemoveItemFromModule(string itemName, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemName) || progress == null) return;
        amount = Mathf.Max(1, amount);
        if (progress.RemoveItem(itemName, amount))
            ItemChangeNotifier.Show(itemChangeText, itemName, amount, false);
        else
            Debug.LogWarning($"[StoryEventDirector] Could not remove '{itemName}' from the inventory.", this);
    }

    internal void OpenReadableItemFromModule(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        InventoryGridUI inventory = FindAnyObjectByType<InventoryGridUI>(FindObjectsInactive.Include);
        if (inventory == null)
            Debug.LogError("[StoryEventDirector] Cannot open a book: InventoryGridUI is missing.", this);
        else if (!inventory.OpenReadableItem(itemName))
            Debug.LogError($"[StoryEventDirector] '{itemName}' is not a readable item.", this);
    }

    internal void DeferReadableItemFromModule(string itemName)
    {
        pendingReadableItem = itemName;
    }

    private void CompleteSessionOnce(string sessionId, string nextSessionId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(sessionId)) return;
        if (string.IsNullOrWhiteSpace(nextSessionId)) nextSessionId = sessionId;

        // Flag trùng với ConnectedStoryDirector: Session1Complete, Session2AComplete, ...
        string completeFlag = "Session" + sessionId + "Complete";
        if (progress.Data.HasFlag(completeFlag))
        {
            string currentSession = progress.Data.currentSession;
            if (currentSession == nextSessionId) return;
            if (currentSession != sessionId && currentSession != sessionId + ".5") return;
        }

        bool ownsBatch = !progress.IsStoryEventBatchActive;
        if (ownsBatch) progress.BeginStoryEventBatch();
        try
        {
            // Zone lấy theo zone hiện tại (do ZoneTrigger set), không cần điền nextZone
            progress.CompleteSession(sessionId, nextSessionId);//, progress.Data.currentZone);
            progress.SetFlag(completeFlag);
        }
        finally
        {
            if (ownsBatch) progress.EndStoryEventBatch();
        }
    }

    private void ClearCompletedEventForPendingChoice(string eventId)
    {
        completedEvents.Remove(eventId);
        if (progress.Data.completedStoryEvents != null)
            progress.Data.completedStoryEvents.RemoveAll(id => id == eventId);
        progress.RemoveFlag(EventFlagPrefix + eventId);
        progress.Save();
        Debug.LogWarning($"[StoryEventDirector] Cleared stale completion for '{eventId}' because its current-session choice is still pending.", this);
    }
    private void CompleteEvent(StoryEventDefinition definition)
    {
        if (definition.oneShot)
        {
            completedEvents.Add(definition.eventId);
            progress = PlayerProgressionService.Instance;
            if (progress != null)
            {
                if (progress.Data.completedStoryEvents == null)
                    progress.Data.completedStoryEvents = new List<string>();
                if (!progress.Data.completedStoryEvents.Contains(definition.eventId))
                    progress.Data.completedStoryEvents.Add(definition.eventId);
                progress.SetFlag(EventFlagPrefix + definition.eventId);
            }
        }
    }

    private StoryEventDefinition FindEvent(string eventId)
    {
        foreach (StoryEventDefinition definition in events ?? Array.Empty<StoryEventDefinition>())
        {
            if (definition != null && definition.eventId == eventId) return definition;
        }

        return null;
    }

    public void RecordDialogueOpened(DemoNpcDialogueProfile profile)
    {
        if (startingConfiguredDialogue || replayingLastEvent || profile == null) return;
        progress = PlayerProgressionService.Instance;
        if (progress == null) return;

        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DemoNpcDialogueController>();
        bool pendingChoice = dialogueController != null
            && dialogueController.HasPendingChoiceForCurrentSession(profile, progress);
        bool ownsBatch = !progress.IsStoryEventBatchActive;
        if (ownsBatch) progress.BeginStoryEventBatch();
        try
        {
            RecordLastEvent("Dialogue:" + GetObjectId(profile.gameObject),
                string.Empty, null, profile, pendingChoice);
        }
        finally
        {
            if (ownsBatch) progress.EndStoryEventBatch();
        }
    }

    public void NotifySpawnerSpawned(ZoneEnemySpawner spawner)
    {
        if (spawner == null || replayingLastEvent) return;
        progress = PlayerProgressionService.Instance;
        if (progress == null || progress.IsStoryEventBatchActive) return;

        progress.BeginStoryEventBatch();
        try
        {
            StoryEventAction action = new StoryEventAction { spawnSpawners = new[] { spawner } };
            RecordLastEvent("SpawnerSpawn:" + GetObjectId(spawner.gameObject),
                string.Empty, action, null, false);
        }
        finally
        {
            progress.EndStoryEventBatch();
        }
    }

    public void NotifySpawnerCleared(ZoneEnemySpawner spawner)
    {
        progress = PlayerProgressionService.Instance;
        LastEventData lastEvent = progress != null ? progress.Data.lastEvent : null;
        if (spawner == null || progress == null || progress.Data.pendingStorySpawnerIds == null)
            return;

        string spawnerId = GetObjectId(spawner.gameObject);
        if (!progress.Data.pendingStorySpawnerIds.Remove(spawnerId)) return;
        bool ownsBatch = !progress.IsStoryEventBatchActive;
        if (ownsBatch) progress.BeginStoryEventBatch();
        try
        {
            if (lastEvent != null)
            {
                lastEvent.spawnedSpawnerIds = new List<string>(progress.Data.pendingStorySpawnerIds);
                lastEvent.pendingSpawn = lastEvent.spawnedSpawnerIds.Count > 0;
                if (lastEvent.pendingSpawn)
                {
                    if (!lastEvent.actionTypes.Contains(SpawnSpawnerAction))
                        lastEvent.actionTypes.Add(SpawnSpawnerAction);
                }
                else
                {
                    lastEvent.actionTypes.Remove(SpawnSpawnerAction);
                }
            }
            progress.Save();
        }
        finally
        {
            if (ownsBatch) progress.EndStoryEventBatch();
        }
    }

    private void RecordLastEvent(
        string eventId,
        string choiceId,
        StoryEventAction action,
        DemoNpcDialogueProfile dialogueProfile,
        bool pendingChoice,
        DemoNpcDialogueProfile choiceProfile = null,
        string sessionId = null)
    {
        progress = PlayerProgressionService.Instance;
        if (progress == null) return;
        PruneCompletedSpawners();

        LastEventData lastEvent = new LastEventData
        {
            eventId = eventId,
            choiceId = choiceId,
            dialogueProfileId = dialogueProfile != null
                ? GetObjectId(dialogueProfile.gameObject)
                : choiceProfile != null && pendingChoice ? GetObjectId(choiceProfile.gameObject) : string.Empty,
            pendingChoice = pendingChoice,
            pendingSpawn = false,
            sessionId = string.IsNullOrWhiteSpace(sessionId) ? progress.Data.currentSession : sessionId
        };

        if (dialogueProfile != null)
            lastEvent.actionTypes.Add(OpenDialogueAction);
        if (action != null)
        {
            if (action.spawnSpawners != null)
            {
                foreach (ZoneEnemySpawner spawner in action.spawnSpawners)
                {
                    if (spawner == null || !spawner.HasSpawned || spawner.IsCleared) continue;
                    string spawnerId = GetObjectId(spawner.gameObject);
                    if (!progress.Data.pendingStorySpawnerIds.Contains(spawnerId))
                        progress.Data.pendingStorySpawnerIds.Add(spawnerId);
                }
            }
            if (action.activateObjects != null && action.activateObjects.Length > 0)
                lastEvent.actionTypes.Add(ActivateObjectsAction);
            if (action.deactivateObjects != null && action.deactivateObjects.Length > 0)
                lastEvent.actionTypes.Add(DeactivateObjectsAction);
            if (action.flagsToSet != null && action.flagsToSet.Length > 0)
                lastEvent.actionTypes.Add("SetFlags");
            if (action.flagsToRemove != null && action.flagsToRemove.Length > 0)
                lastEvent.actionTypes.Add("RemoveFlags");
            if (!string.IsNullOrWhiteSpace(action.itemToRemove))
                lastEvent.actionTypes.Add("RemoveItem");
            if (!string.IsNullOrWhiteSpace(action.itemToAdd))
                lastEvent.actionTypes.Add("AddItem");
            if (!string.IsNullOrWhiteSpace(action.readableItemToOpen))
                lastEvent.actionTypes.Add("OpenReadableItem");
            if (action.activateObjects != null)
            {
                foreach (GameObject target in action.activateObjects)
                    if (target != null) lastEvent.activatedObjectIds.Add(GetObjectId(target));
            }
            if (action.deactivateObjects != null)
            {
                foreach (GameObject target in action.deactivateObjects)
                    if (target != null) lastEvent.deactivatedObjectIds.Add(GetObjectId(target));
            }
        }

        lastEvent.spawnedSpawnerIds = new List<string>(progress.Data.pendingStorySpawnerIds);
        lastEvent.pendingSpawn = lastEvent.spawnedSpawnerIds.Count > 0;
        if (lastEvent.pendingSpawn && !lastEvent.actionTypes.Contains(SpawnSpawnerAction))
            lastEvent.actionTypes.Add(SpawnSpawnerAction);
        progress.SetLastEvent(lastEvent);
    }

    private void PruneCompletedSpawners()
    {
        List<string> pendingSpawnerIds = progress.Data.pendingStorySpawnerIds;
        if (pendingSpawnerIds == null) return;
        for (int i = pendingSpawnerIds.Count - 1; i >= 0; i--)
        {
            GameObject target = ResolveObjectId(pendingSpawnerIds[i]);
            ZoneEnemySpawner spawner = target != null ? target.GetComponent<ZoneEnemySpawner>() : null;
            if (spawner == null || spawner.IsCleared) pendingSpawnerIds.RemoveAt(i);
        }
    }

    private void RestoreCompletedEventsFromSave()
    {
        if (progress.Data.completedStoryEvents == null)
            progress.Data.completedStoryEvents = new List<string>();
        completedEvents.Clear();
        foreach (string eventId in progress.Data.completedStoryEvents)
        {
            if (!string.IsNullOrWhiteSpace(eventId)) completedEvents.Add(eventId);
        }
        foreach (string flag in progress.Data.flags ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(flag) || !flag.StartsWith(EventFlagPrefix, StringComparison.Ordinal))
                continue;
            string eventId = flag.Substring(EventFlagPrefix.Length);
            if (!progress.Data.completedStoryEvents.Contains(eventId))
                progress.Data.completedStoryEvents.Add(eventId);
            completedEvents.Add(eventId);
        }
    }

    private void RestoreStoryObjectStates()
    {
        if (progress.Data.storyObjectStates == null) return;
        foreach (StoryObjectState state in progress.Data.storyObjectStates)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.objectId)) continue;
            GameObject target = ResolveObjectId(state.objectId);
            if (target == null)
            {
                Debug.LogWarning($"[StoryEventDirector] Saved story object '{state.objectId}' was not found in this scene.", this);
                continue;
            }
            if (target.activeSelf != state.active) target.SetActive(state.active);
        }
    }

    private void RefreshDialogueProfiles()
    {
        dialogueController = FindAnyObjectByType<DemoNpcDialogueController>();
        if (dialogueController != null) dialogueController.RefreshNpcProfiles();
    }

    private System.Collections.IEnumerator ResumeLastEventAfterContinue()
    {
        yield return null;

        progress = PlayerProgressionService.Instance;
        if (progress == null) yield break;
        LastEventData lastEvent = progress.Data.lastEvent;
        if (lastEvent == null)
        {
            progress.CompleteContinueRestore();
            progress.Save();
            yield break;
        }

        progress.RestoreLastEventSnapshot(lastEvent);

        if (!string.IsNullOrWhiteSpace(lastEvent.sceneName)
            && SceneManager.GetActiveScene().name == "CoreCombatDemo"
            && lastEvent.sceneName.StartsWith("Zone", StringComparison.OrdinalIgnoreCase)
            && Application.CanStreamedLevelBeLoaded(lastEvent.sceneName))
        {
            float waitRemaining = 15f;
            Scene eventScene = SceneManager.GetSceneByName(lastEvent.sceneName);
            while ((!eventScene.IsValid() || !eventScene.isLoaded) && waitRemaining > 0f)
            {
                waitRemaining -= Time.unscaledDeltaTime;
                yield return null;
                eventScene = SceneManager.GetSceneByName(lastEvent.sceneName);
            }
        }

        Scene lastEventScene = string.IsNullOrWhiteSpace(lastEvent.sceneName)
            ? SceneManager.GetActiveScene()
            : SceneManager.GetSceneByName(lastEvent.sceneName);
        if (!lastEventScene.IsValid() || !lastEventScene.isLoaded)
        {
            Debug.LogWarning(
                $"[StoryEventDirector] Last event scene '{lastEvent.sceneName}' is not loaded; replay skipped.",
                this);
            progress.CompleteContinueRestore();
            progress.Save();
            yield break;
        }

        RestoreStoryObjectStates();
        RestoreCompletedEventsFromSave();
        RefreshDialogueProfiles();

        replayingLastEvent = true;
        try
        {
            if (lastEvent.pendingSpawn)
            {
                foreach (string spawnerId in lastEvent.spawnedSpawnerIds ?? new List<string>())
                {
                    GameObject target = ResolveObjectId(spawnerId);
                    ZoneEnemySpawner spawner = target != null ? target.GetComponent<ZoneEnemySpawner>() : null;
                    if (spawner == null)
                    {
                        Debug.LogError($"[StoryEventDirector] Cannot replay missing spawner '{spawnerId}'.", this);
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(spawner.EventIdOnCleared)
                        && IsEventCompleted(spawner.EventIdOnCleared))
                        continue;
                    spawner.ReplaySpawn();
                }
            }

            if (lastEvent.pendingChoice)
            {
                GameObject profileObject = ResolveObjectId(lastEvent.dialogueProfileId);
                DemoNpcDialogueProfile profile = profileObject != null
                    ? profileObject.GetComponent<DemoNpcDialogueProfile>()
                    : null;
                if (profile == null)
                {
                    Debug.LogError(
                        $"[StoryEventDirector] Cannot replay missing dialogue profile '{lastEvent.dialogueProfileId}'.",
                        this);
                }
                else
                {
                    StoryEventDefinition definition = FindEvent(lastEvent.eventId);
                    if (definition != null && definition.dialogueProfile == profile)
                        activeDialogueEventId = definition.eventId;
                    if (dialogueController == null || !dialogueController.StartDialogueForContinueRestore(profile))
                    {
                        activeDialogueEventId = null;
                        Debug.LogError($"[StoryEventDirector] Could not replay dialogue event '{lastEvent.eventId}'.", this);
                    }
                }
            }
        }
        finally
        {
            replayingLastEvent = false;
            progress.CompleteContinueRestore();
            progress.Save();
        }
    }

    public static string GetObjectId(GameObject target)
    {
        if (target == null) return string.Empty;
        List<string> segments = new List<string>();
        Transform current = target.transform;
        while (current != null)
        {
            segments.Add(current.GetSiblingIndex() + ":" + Uri.EscapeDataString(current.name));
            current = current.parent;
        }
        segments.Reverse();
        return target.scene.name + "|" + string.Join("/", segments);
    }

    private static GameObject ResolveObjectId(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return null;
        int separator = objectId.IndexOf('|');
        if (separator <= 0 || separator == objectId.Length - 1) return null;
        Scene scene = SceneManager.GetSceneByName(objectId.Substring(0, separator));
        if (!scene.IsValid() || !scene.isLoaded) return null;

        string[] segments = objectId.Substring(separator + 1).Split('/');
        if (segments.Length == 0) return null;
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject currentObject = ResolveChild(roots, segments[0]);
        if (currentObject == null) return null;
        for (int i = 1; i < segments.Length; i++)
        {
            currentObject = ResolveChild(currentObject.transform, segments[i]);
            if (currentObject == null) return null;
        }
        return currentObject;
    }

    private static GameObject ResolveChild(GameObject[] siblings, string segment)
    {
        if (!TryParseSegment(segment, out int index, out string name)
            || index < 0 || index >= siblings.Length)
            return null;
        GameObject candidate = siblings[index];
        return candidate.name == name ? candidate : null;
    }

    private static GameObject ResolveChild(Transform parent, string segment)
    {
        if (!TryParseSegment(segment, out int index, out string name)
            || index < 0 || index >= parent.childCount)
            return null;
        Transform candidate = parent.GetChild(index);
        return candidate.name == name ? candidate.gameObject : null;
    }

    private static bool TryParseSegment(string segment, out int index, out string name)
    {
        index = -1;
        name = string.Empty;
        int separator = segment.IndexOf(':');
        if (separator <= 0 || !int.TryParse(segment.Substring(0, separator), out index)) return false;
        name = Uri.UnescapeDataString(segment.Substring(separator + 1));
        return true;
    }

    private bool HasRecordedChoice(DemoNpcDialogueProfile profile)
    {
        if (profile == null || progress == null || progress.Data.choices == null) return false;

        foreach (DemoDialogueNode node in profile.Dialogue ?? Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? Array.Empty<DemoDialogueChoice>())
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.sessionId)) continue;
                if (progress.Data.choices.Exists(record => record != null && record.sessionId == choice.sessionId && record.choiceId == choice.choiceId))
                    return true;
            }
        }

        return false;
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        foreach (GameObject target in objects ?? Array.Empty<GameObject>())
        {
            if (target == null) continue;
            progress?.RecordStoryObjectState(GetObjectId(target), active);
            if (target.activeSelf != active) target.SetActive(active);
        }
    }
}
