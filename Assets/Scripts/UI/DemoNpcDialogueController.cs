using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class DemoNpcDialogueController : MonoBehaviour
{
    public static event System.Action<DemoNpcDialogueProfile, DemoDialogueChoice> ChoiceSelected;
    public static event System.Action<DemoNpcDialogueProfile> DialogueClosed;
    public static bool IsInteractionBlocked => dialogueIsOpen || interactionKeyHandledFrame == Time.frameCount;

    private static bool dialogueIsOpen;
    private static int interactionKeyHandledFrame = -1;

    [Header("Interaction")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private Text interactionPromptText;

    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite[] playerPortraitFrames;
    [SerializeField] private string playerName = "\u004B\u0068\u1EA3i";
    [SerializeField, Tooltip("Use this ID in dialogue lines when the player speaks.")] private string playerSpeakerId = "player";
    [SerializeField, Min(0.1f)] private float playerPortraitFrameRate = 4f;
    [SerializeField] private DemoDialogueSpeaker[] dialogueSpeakers = System.Array.Empty<DemoDialogueSpeaker>();
    [SerializeField] private Text speakerText;
    [SerializeField] private Text dialogueText;
    [SerializeField] private Text continueButtonLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField, Min(1f)] private float typingCharactersPerSecond = 36f;

    private DemoNpcDialogueProfile[] npcs = new DemoNpcDialogueProfile[0];
    private DemoNpcDialogueProfile nearbyNpc;
    private DemoNpcDialogueProfile activeNpc;
    private DemoDialogueSpeaker activeSpeaker;
    private DemoDialogueChoice[] visibleChoices = new DemoDialogueChoice[0];
    private string fullDialogueText = string.Empty;
    private float typedCharacterCount;
    private float portraitTimer;
    private int portraitFrameIndex;
    private int currentNodeIndex = -1;
    private int pendingNodeIndex = -1;
    private bool isTyping;
    private bool activeSpeakerIsPlayer;
    private bool dialogueOpen;

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null || interactionPrompt == null || interactionPromptText == null
            || dialogueRoot == null || portraitImage == null || speakerText == null
            || dialogueText == null || choiceButtons == null)
        {
            Debug.LogError("[DemoNpcDialogueController] Assign the Player and prebuilt dialogue UI references.", this);
            enabled = false;
            return;
        }

        RefreshNpcProfiles();
        if (continueButton != null) continueButton.onClick.AddListener(ContinueDialogue);
        if (closeButton != null) closeButton.onClick.AddListener(CloseDialogue);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int choiceIndex = i;
            choiceButtons[i].onClick.AddListener(() => SelectChoice(choiceIndex));
            choiceButtons[i].gameObject.SetActive(false);
        }

        interactionPrompt.SetActive(false);
        dialogueRoot.SetActive(false);
    }

    private void Update()
    {
        bool gameplayPaused = GameplayPause.IsPaused;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null && progress.IsContinueRestorePending)
        {
            interactionPrompt.SetActive(false);
            dialogueRoot.SetActive(false);
            return;
        }
        UpdateNearbyNpc();
        interactionPrompt.SetActive(!gameplayPaused && !dialogueOpen && nearbyNpc != null);
        dialogueRoot.SetActive(!gameplayPaused && dialogueOpen);

        if (gameplayPaused) return;

        if (!dialogueOpen && nearbyNpc != null && Input.GetKeyDown(KeyCode.E))
        {
            interactionKeyHandledFrame = Time.frameCount;
            OpenDialogue(nearbyNpc);
        }
        else if (dialogueOpen && Input.GetKeyDown(KeyCode.E))
        {
            interactionKeyHandledFrame = Time.frameCount;
            ContinueDialogue();
        }

        if (dialogueOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseDialogue();
        }

        if (!dialogueOpen) return;

        UpdateTypewriter();
        UpdatePortraitAnimation();
    }

    private void UpdateNearbyNpc()
    {
        nearbyNpc = null;
        if (dialogueOpen || player == null) return;

        float nearestDistanceSquared = float.PositiveInfinity;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        foreach (DemoNpcDialogueProfile npc in npcs)
        {
            if (npc == null || !npc.isActiveAndEnabled) continue;
            if (HasSessionChoices(npc)
                && !HasPendingChoiceForCurrentSession(npc, progress)
                && FindRecordedChoice(npc, progress) == null)
                continue;

            if (npc.InteractionRange <= 0f) continue;

            float distanceSquared = ((Vector2)npc.transform.position - (Vector2)player.position).sqrMagnitude;
            float npcRange = npc.InteractionRange;
            if (distanceSquared > npcRange * npcRange) continue;
            if (distanceSquared > nearestDistanceSquared) continue;

            nearbyNpc = npc;
            nearestDistanceSquared = distanceSquared;
        }

        if (nearbyNpc != null)
        {
            interactionPromptText.text = "Press E to talk to " + GetNpcDisplayName(nearbyNpc);
        }
    }

    private void OpenDialogue(DemoNpcDialogueProfile npc, bool allowPendingChoiceRestore = false)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        bool hasChoiceFlow = HasSessionChoices(npc);
        bool hasPendingChoice = HasPendingChoiceForCurrentSession(npc, progress);
        ChoiceRecord recordedChoice = hasPendingChoice ? null : FindRecordedChoice(npc, progress);
        if (!allowPendingChoiceRestore && hasChoiceFlow && !hasPendingChoice && recordedChoice == null)
            return;

        activeNpc = npc;
        dialogueOpen = true;
        dialogueIsOpen = true;
        visibleChoices = new DemoDialogueChoice[0];
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.LockMovement(true);
        FaceDialogueParticipants(npc);
        StoryEventDirector storyEvents = FindAnyObjectByType<StoryEventDirector>();
        if (storyEvents != null) storyEvents.RecordDialogueOpened(activeNpc);

        SetDialogueSpeaker(string.Empty);

        string followUp = null;
        bool followUpIsLocalizationKey = false;
        if (recordedChoice != null)
        {
            if (!string.IsNullOrWhiteSpace(activeNpc.PostDialogueText))
            {
                followUp = activeNpc.PostDialogueText;
                followUpIsLocalizationKey = activeNpc.PostDialogueTextIsLocalizationKey;
            }
            else
            {
                followUp = GetRecordedChoiceText(activeNpc, recordedChoice);
                followUpIsLocalizationKey = GetRecordedChoiceTextIsLocalizationKey(activeNpc, recordedChoice);
            }
        }
        if (!string.IsNullOrEmpty(followUp))
        {
            SetDialogueSpeaker(string.IsNullOrWhiteSpace(activeNpc.PostDialogueText)
                ? GetRecordedChoiceSpeakerId(activeNpc, recordedChoice)
                : string.Empty);
            currentNodeIndex = -1;
            pendingNodeIndex = -1;
            SetChoicesVisible(false);
            StartTyping(followUp, followUpIsLocalizationKey);
            return;
        }

        if (activeNpc.Dialogue == null || activeNpc.Dialogue.Length == 0)
        {
            SetChoicesVisible(false);
            StartTyping(string.Empty);
            return;
        }

        LoadDialogueNode(0);
    }

    private void FaceDialogueParticipants(DemoNpcDialogueProfile npc)
    {
        if (player == null || npc == null) return;
        CharacterAnimation playerAnimation = player.GetComponentInChildren<CharacterAnimation>();
        DemoDialogueSpeaker profileSpeaker = FindProfileSpeaker(npc);
        if (profileSpeaker != null && profileSpeaker.character != null)
        {
            Vector2 towardNpc = (Vector2)(profileSpeaker.character.transform.position - player.position);
            if (playerAnimation != null)
            {
                playerAnimation.SetFacing(towardNpc);
                playerAnimation.Play(AnimState.Idle);
            }
        }

        foreach (DemoDialogueSpeaker speaker in dialogueSpeakers ?? System.Array.Empty<DemoDialogueSpeaker>())
        {
            if (speaker == null || speaker.character == null) continue;
            CharacterAnimation animation = speaker.character.GetComponentInChildren<CharacterAnimation>();
            if (animation == null) continue;
            Vector2 towardPlayer = (Vector2)(player.position - speaker.character.transform.position);
            animation.SetFacing(towardPlayer);
            animation.Play(AnimState.Idle);
        }
    }

    public bool StartDialogue(DemoNpcDialogueProfile npc)
    {
        if (npc == null || dialogueOpen || !isActiveAndEnabled) return false;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (HasSessionChoices(npc) && !HasPendingChoiceForCurrentSession(npc, progress))
            return false;
        OpenDialogue(npc);
        return dialogueOpen;
    }

    public bool StartDialogueForContinueRestore(DemoNpcDialogueProfile npc)
    {
        if (npc == null || dialogueOpen || !isActiveAndEnabled) return false;
        OpenDialogue(npc, true);
        return dialogueOpen;
    }

    public void RefreshNpcProfiles()
    {
        npcs = FindObjectsByType<DemoNpcDialogueProfile>(
            FindObjectsInactive.Include);
    }

    public bool HasPendingChoiceForCurrentSession(DemoNpcDialogueProfile npc, PlayerProgressionService progress)
    {
        if (npc == null || progress == null || progress.Data == null
            || string.IsNullOrWhiteSpace(progress.Data.currentSession))
            return false;

        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
            {
                if (choice == null) continue;
                if (!string.IsNullOrWhiteSpace(choice.requiredFlag)
                    && !progress.Data.HasFlag(choice.requiredFlag))
                    continue;
                if (!HasRequiredItem(choice, progress)) continue;
                if (string.IsNullOrWhiteSpace(choice.sessionId)) return true;
                if (choice.sessionId != progress.Data.currentSession) continue;
                if (FindChoiceRecord(progress, choice.sessionId) == null) return true;
            }
        }

        return false;
    }

    public static bool HasSessionChoices(DemoNpcDialogueProfile npc)
    {
        if (npc == null) return false;
        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
            {
                if (choice != null && !string.IsNullOrWhiteSpace(choice.sessionId)) return true;
            }
        }

        return false;
    }

    private static ChoiceRecord FindRecordedChoice(DemoNpcDialogueProfile npc, PlayerProgressionService progress)
    {
        if (npc == null || progress == null || progress.Data == null || progress.Data.choices == null)
            return null;

        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.sessionId)) continue;
                ChoiceRecord record = FindChoiceRecord(progress, choice.sessionId);
                if (record != null) return record;
            }
        }

        return null;
    }

    private static ChoiceRecord FindChoiceRecord(PlayerProgressionService progress, string sessionId)
    {
        if (progress == null || progress.Data == null || progress.Data.choices == null) return null;
        return progress.Data.choices.Find(record => record != null && record.sessionId == sessionId);
    }

    private static string GetRecordedChoiceText(DemoNpcDialogueProfile npc, ChoiceRecord record)
    {
        if (npc == null || record == null) return null;

        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
            {
                if (choice == null || choice.sessionId != record.sessionId || choice.choiceId != record.choiceId)
                    continue;
                return string.IsNullOrWhiteSpace(choice.resultText) ? null : choice.resultText;
            }
        }

        return null;
    }
    private void LoadDialogueNode(int index)
    {
        DemoDialogueNode[] nodes = activeNpc != null ? activeNpc.Dialogue : null;
        if (nodes == null || index < 0 || index >= nodes.Length)
        {
            currentNodeIndex = -1;
            pendingNodeIndex = -1;
            speakerText.text = GetNpcDisplayName(activeNpc);
            SetChoicesVisible(false);
            StartTyping(string.Empty);
            return;
        }

        currentNodeIndex = index;
        DemoDialogueNode node = nodes[index];
        SetDialogueSpeaker(node.speakerId);
        pendingNodeIndex = node.nextNodeIndex;
        StartTyping(node.text, node.textIsLocalizationKey);
        ShowChoices(node.choices);
    }

    private static bool GetRecordedChoiceTextIsLocalizationKey(DemoNpcDialogueProfile npc, ChoiceRecord record)
    {
        if (npc == null || record == null) return false;
        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
                if (choice != null && choice.sessionId == record.sessionId && choice.choiceId == record.choiceId)
                    return choice.resultTextIsLocalizationKey;
        }
        return false;
    }

    private static string GetRecordedChoiceSpeakerId(DemoNpcDialogueProfile npc, ChoiceRecord record)
    {
        if (npc == null || record == null) return string.Empty;

        foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null) continue;
            foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
            {
                if (choice == null || choice.sessionId != record.sessionId || choice.choiceId != record.choiceId)
                    continue;
                return choice.resultSpeakerId;
            }
        }

        return string.Empty;
    }

    private void SetDialogueSpeaker(string speakerId)
    {
        activeSpeakerIsPlayer = !string.IsNullOrWhiteSpace(playerSpeakerId)
            && string.Equals(speakerId, playerSpeakerId, System.StringComparison.OrdinalIgnoreCase);
        activeSpeaker = activeSpeakerIsPlayer
            ? null
            : string.IsNullOrWhiteSpace(speakerId)
                ? FindProfileSpeaker(activeNpc)
                : FindDialogueSpeaker(speakerId);
        if (!activeSpeakerIsPlayer && activeSpeaker == null)
            activeSpeaker = FindProfileSpeaker(activeNpc);
        string displayName = activeSpeakerIsPlayer
            ? playerName
            : activeSpeaker != null && !string.IsNullOrWhiteSpace(activeSpeaker.characterName)
                ? activeSpeaker.characterName
                : GetNpcDisplayName(activeNpc);
        speakerText.text = displayName;
        portraitTimer = 0f;
        portraitFrameIndex = 0;

        Sprite[] frames = activeSpeakerIsPlayer
            ? playerPortraitFrames
            : activeSpeaker != null ? activeSpeaker.portraitFrames : null;
        if (frames != null && frames.Length > 0 && frames[0] != null)
            portraitImage.sprite = frames[0];
        else if (activeSpeakerIsPlayer)
            portraitImage.sprite = null;
        else
        {
            SpriteRenderer renderer = activeSpeaker != null && activeSpeaker.character != null
                ? activeSpeaker.character.GetComponentInChildren<SpriteRenderer>()
                : null;
            portraitImage.sprite = renderer != null ? renderer.sprite : null;
        }
    }

    private DemoDialogueSpeaker FindProfileSpeaker(DemoNpcDialogueProfile profile)
    {
        if (profile == null) return null;
        foreach (DemoDialogueSpeaker speaker in dialogueSpeakers ?? System.Array.Empty<DemoDialogueSpeaker>())
        {
            if (speaker == null || speaker.character == null) continue;
            Transform characterTransform = speaker.character.transform;
            if (characterTransform == profile.transform
                || characterTransform.IsChildOf(profile.transform)
                || profile.transform.IsChildOf(characterTransform))
                return speaker;
        }

        foreach (DemoDialogueNode node in profile.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
        {
            if (node == null || string.IsNullOrWhiteSpace(node.speakerId)
                || string.Equals(node.speakerId, playerSpeakerId, System.StringComparison.OrdinalIgnoreCase))
                continue;
            DemoDialogueSpeaker speaker = FindDialogueSpeaker(node.speakerId);
            if (speaker != null) return speaker;
        }

        return null;
    }

    private string GetNpcDisplayName(DemoNpcDialogueProfile profile)
    {
        DemoDialogueSpeaker speaker = FindProfileSpeaker(profile);
        return speaker != null && !string.IsNullOrWhiteSpace(speaker.characterName)
            ? speaker.characterName
            : "NPC";
    }

    private DemoDialogueSpeaker FindDialogueSpeaker(string speakerId)
    {
        if (string.IsNullOrWhiteSpace(speakerId)) return null;
        foreach (DemoDialogueSpeaker speaker in dialogueSpeakers ?? System.Array.Empty<DemoDialogueSpeaker>())
            if (speaker != null && speaker.id == speakerId) return speaker;
        Debug.LogWarning($"[DemoNpcDialogueController] No dialogue speaker is configured with ID '{speakerId}'. Using the interacting NPC.", this);
        return null;
    }

    private void ShowChoices(DemoDialogueChoice[] choices)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        List<DemoDialogueChoice> availableChoices = new List<DemoDialogueChoice>();
        if (choices != null)
        {
            foreach (DemoDialogueChoice choice in choices)
            {
                if (choice == null) continue;
                if (!string.IsNullOrWhiteSpace(choice.requiredFlag)
                    && (progress == null || !progress.Data.HasFlag(choice.requiredFlag)))
                    continue;
                if (!HasRequiredItem(choice, progress)) continue;
                if (!string.IsNullOrWhiteSpace(choice.sessionId))
                {
                    if (progress == null || choice.sessionId != progress.Data.currentSession
                        || FindChoiceRecord(progress, choice.sessionId) != null)
                        continue;
                }
                availableChoices.Add(choice);
            }
        }
        visibleChoices = availableChoices.ToArray();

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool hasChoice = !isTyping && i < visibleChoices.Length;
            choiceButtons[i].gameObject.SetActive(hasChoice);
            if (!hasChoice) continue;

            Text label = choiceButtons[i].GetComponentInChildren<Text>();
            if (label != null) label.text = ResolveLocalizedText(visibleChoices[i].label, visibleChoices[i].labelIsLocalizationKey);
        }

        if (visibleChoices.Length > choiceButtons.Length)
        {
            Debug.LogWarning(
                $"[DemoNpcDialogueController] Dialogue has {visibleChoices.Length} available choices, but only {choiceButtons.Length} choice buttons are assigned.",
                this);
        }
    }

    private static bool HasRequiredItem(DemoDialogueChoice choice, PlayerProgressionService progress)
    {
        if (choice == null || string.IsNullOrWhiteSpace(choice.requiredItemName)) return true;
        return progress != null && progress.Data != null
            && progress.Data.GetItemAmount(choice.requiredItemName) >= Mathf.Max(1, choice.requiredItemAmount);
    }

    private void SetChoicesVisible(bool visible)
    {
        foreach (Button button in choiceButtons)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }
    }

    private void SelectChoice(int index)
    {
        if (activeNpc == null || isTyping || currentNodeIndex < 0) return;

        if (index < 0 || index >= visibleChoices.Length) return;
        DemoDialogueChoice choice = visibleChoices[index];
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null) progress.BeginStoryEventBatch();
        try
        {
            if (!string.IsNullOrWhiteSpace(choice.sessionId) && !string.IsNullOrWhiteSpace(choice.choiceId))
            {
                if (progress == null
                    || !progress.RecordChoice(choice.sessionId, choice.choiceId, choice.ltDelta, choice.qtDelta))
                {
                    Debug.LogWarning($"[DemoNpcDialogueController] Could not record choice {choice.sessionId}/{choice.choiceId}.", this);
                    return;
                }
            }
            ChoiceSelected?.Invoke(activeNpc, choice);
            pendingNodeIndex = choice.nextNodeIndex;
            SetChoicesVisible(false);
            visibleChoices = new DemoDialogueChoice[0];
            if (!string.IsNullOrWhiteSpace(choice.resultText))
            {
                SetDialogueSpeaker(choice.resultSpeakerId);
                StartTyping(choice.resultText, choice.resultTextIsLocalizationKey);
            }
            else if (pendingNodeIndex >= 0)
            {
                LoadDialogueNode(pendingNodeIndex);
            }
            else
            {
                CloseDialogue();
            }
        }
        finally
        {
            if (progress != null) progress.EndStoryEventBatch();
        }
    }

    private void ContinueDialogue()
    {
        if (!dialogueOpen) return;

        if (isTyping)
        {
            dialogueText.text = fullDialogueText;
            typedCharacterCount = fullDialogueText.Length;
            isTyping = false;
            UpdateContinueHint();
            if (visibleChoices.Length > 0) ShowChoices(visibleChoices);
            return;
        }

        if (visibleChoices.Length > 0) return;

        if (pendingNodeIndex >= 0)
        {
            LoadDialogueNode(pendingNodeIndex);
        }
        else
        {
            CloseDialogue();
        }
    }

    private void CloseDialogue()
    {
        DemoNpcDialogueProfile closedNpc = activeNpc;
        dialogueOpen = false;
        dialogueIsOpen = false;
        PlayerController playerController = player != null ? player.GetComponent<PlayerController>() : null;
        if (playerController != null) playerController.LockMovement(false);
        activeNpc = null;
        currentNodeIndex = -1;
        pendingNodeIndex = -1;
        isTyping = false;
        visibleChoices = new DemoDialogueChoice[0];
        SetChoicesVisible(false);
        dialogueRoot.SetActive(false);
        if (closedNpc != null) DialogueClosed?.Invoke(closedNpc);
    }

    private void StartTyping(string text, bool isLocalizationKey = false)
    {
        fullDialogueText = ResolveLocalizedText(text, isLocalizationKey);
        typedCharacterCount = 0f;
        isTyping = true;
        dialogueText.text = string.Empty;
        UpdateContinueHint();
    }

    private static string ResolveLocalizedText(string text, bool isLocalizationKey)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return isLocalizationKey ? transition.dich(text, transition.CurrentLanguage) : text;
    }

    private void UpdateTypewriter()
    {
        if (!isTyping) return;

        typedCharacterCount += Time.unscaledDeltaTime * typingCharactersPerSecond;
        int visibleCharacters = Mathf.Min(fullDialogueText.Length, Mathf.FloorToInt(typedCharacterCount));
        dialogueText.text = fullDialogueText.Substring(0, visibleCharacters);

        if (visibleCharacters >= fullDialogueText.Length)
        {
            isTyping = false;
            UpdateContinueHint();
            if (visibleChoices.Length > 0) ShowChoices(visibleChoices);
        }
    }

    private void UpdateContinueHint()
    {
        if (continueButtonLabel == null) return;
        continueButtonLabel.text = isTyping
            ? "E: BỎ QUA CHỮ"
            : visibleChoices.Length > 0 ? "CHỌN CÂU TRẢ LỜI"
            : pendingNodeIndex >= 0 ? "E: TIẾP TỤC"
            : "E: KẾT THÚC";
    }

    private void UpdatePortraitAnimation()
    {
        if (activeNpc == null) return;

        if (activeSpeaker == null && !activeSpeakerIsPlayer) return;
        Sprite[] frames = activeSpeakerIsPlayer ? playerPortraitFrames : activeSpeaker.portraitFrames;
        if (frames == null || frames.Length == 0) return;

        float frameRate = activeSpeakerIsPlayer
            ? playerPortraitFrameRate
            : activeSpeaker.portraitFrameRate;
        portraitTimer += Time.unscaledDeltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, frameRate);
        if (portraitTimer < frameDuration) return;

        portraitTimer %= frameDuration;
        portraitFrameIndex = (portraitFrameIndex + 1) % frames.Length;
        portraitImage.sprite = frames[portraitFrameIndex];
    }

    private void OnDisable()
    {
        if (dialogueOpen)
        {
            CloseDialogue();
            return;
        }

        dialogueIsOpen = false;
        PlayerController playerController = player != null ? player.GetComponent<PlayerController>() : null;
        if (playerController != null) playerController.LockMovement(false);
    }
}
