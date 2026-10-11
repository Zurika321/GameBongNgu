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
    private DemoDialogueChoice[] visibleChoices = new DemoDialogueChoice[0];
    private string fullDialogueText = string.Empty;
    private float typedCharacterCount;
    private float portraitTimer;
    private int portraitFrameIndex;
    private int currentNodeIndex = -1;
    private int pendingNodeIndex = -1;
    private bool isTyping;
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

            float distanceSquared = ((Vector2)npc.transform.position - (Vector2)player.position).sqrMagnitude;
            float npcRange = Mathf.Max(0.1f, npc.InteractionRange);
            if (distanceSquared > npcRange * npcRange) continue;
            if (distanceSquared > nearestDistanceSquared) continue;

            nearbyNpc = npc;
            nearestDistanceSquared = distanceSquared;
        }

        if (nearbyNpc != null)
        {
            interactionPromptText.text = "Press E to talk to " + nearbyNpc.NpcName;
        }
    }

    private void OpenDialogue(DemoNpcDialogueProfile npc)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        bool hasChoiceFlow = HasSessionChoices(npc);
        bool hasPendingChoice = HasPendingChoiceForCurrentSession(npc, progress);
        ChoiceRecord recordedChoice = hasPendingChoice ? null : FindRecordedChoice(npc, progress);
        if (hasChoiceFlow && !hasPendingChoice && recordedChoice == null)
            return;

        activeNpc = npc;
        dialogueOpen = true;
        dialogueIsOpen = true;
        visibleChoices = new DemoDialogueChoice[0];
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.LockMovement(true);
        StoryEventDirector storyEvents = FindAnyObjectByType<StoryEventDirector>();
        if (storyEvents != null) storyEvents.RecordDialogueOpened(activeNpc);

        Sprite[] frames = activeNpc.PortraitFrames;
        portraitImage.sprite = frames != null && frames.Length > 0
            ? frames[0]
            : activeNpc.GetFallbackPortrait();
        portraitTimer = 0f;
        portraitFrameIndex = 0;
        speakerText.text = activeNpc.NpcName;

        string followUp = recordedChoice != null
            ? GetPostChoiceDialogue(activeNpc, progress) ?? GetRecordedChoiceResponse(activeNpc, recordedChoice)
            : null;
        if (!string.IsNullOrEmpty(followUp))
        {
            currentNodeIndex = -1;
            pendingNodeIndex = -1;
            SetChoicesVisible(false);
            StartTyping(followUp);
            return;
        }

        if (activeNpc.Dialogue == null || activeNpc.Dialogue.Length == 0)
        {
            SetChoicesVisible(false);
            StartTyping("No dialogue has been configured for this NPC yet.");
            return;
        }

        LoadDialogueNode(0);
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

    private static string GetRecordedChoiceResponse(DemoNpcDialogueProfile npc, ChoiceRecord record)
    {
        if (npc != null && record != null)
        {
            foreach (DemoDialogueNode node in npc.Dialogue ?? System.Array.Empty<DemoDialogueNode>())
            {
                if (node == null) continue;
                foreach (DemoDialogueChoice choice in node.choices ?? System.Array.Empty<DemoDialogueChoice>())
                {
                    if (choice == null || choice.sessionId != record.sessionId || choice.choiceId != record.choiceId)
                        continue;
                    if (!string.IsNullOrWhiteSpace(choice.resultText)) return choice.resultText;
                    if (!string.IsNullOrWhiteSpace(choice.label)) return npc.NpcName + ": “" + choice.label + "”";
                }
            }
        }

        return "Khải không có gì mới để nói lúc này.";
    }

    private static string GetPostChoiceDialogue(
        DemoNpcDialogueProfile npc, PlayerProgressionService progress)
    {
        if (npc == null || progress == null || progress.Data.choices == null) return null;

        string sessionId;
        switch (npc.NpcName)
        {
            case "Khải": sessionId = "1"; break;
            case "Trưởng Làng Tứ": sessionId = "2A"; break;
            case "Thầy Trừ Tà Hạo": sessionId = "3"; break;
            case "Vô Diện": sessionId = "4A"; break;
            case "Én": sessionId = "4B"; break;
            case "Bà Tư Bán Thuốc": sessionId = "5"; break;
            default: return null;
        }

        ChoiceRecord choice = progress.Data.choices.Find(
            record => record != null && record.sessionId == sessionId);
        if (choice == null) return null;

        switch (npc.NpcName)
        {
            case "Khải":
                const string selfTalkShownFlag = "KhaiPostChoiceSelfTalkShown";
                if (progress.Data.HasFlag(selfTalkShownFlag)) return null;
                progress.SetFlag(selfTalkShownFlag);
                return choice.choiceId == "A"
                    ? "Khải khẽ gật đầu: “Cánh cửa đã mở. Mình phải tiếp tục đi thôi.”"
                    : "Khải nhìn lên cầu thang: “Manh mối vẫn còn ở trên gác. Mình phải tìm ra sự thật.”";
            case "Trưởng Làng Tứ":
                return choice.choiceId == "A"
                    ? "Tứ khẽ thở dài: “Nếu con đã chọn giữ kín chuyện đó, hãy cẩn thận trên đường đi.”"
                    : "Tứ gật đầu: “Cảm ơn con đã thành thật. Cố lên, ta sẽ giúp trong khả năng của mình.”";
            case "Thầy Trừ Tà Hạo":
                return choice.choiceId == "A"
                    ? "Hạo nói nhỏ: “Lá bùa đã được lập. Hãy giữ vững ký ức còn lại của con.”"
                    : "Hạo gật đầu: “Ta tôn trọng quyết định của con. Hãy chuẩn bị cho điều đang chờ phía trước.”";
            case "Vô Diện":
                return choice.choiceId == "A"
                    ? "Vô Diện thì thầm: “Ngươi đã chịu lắng nghe. Đừng quên điều ta vừa tiết lộ.”"
                    : "Vô Diện bật cười sau mặt gương: “Cứ chống lại đi. Ta vẫn ở đây.”";
            case "Én":
                return choice.choiceId == "A"
                    ? "Én khẽ nói: “Tôi hiểu. Hãy tự giữ an toàn nhé.”"
                    : "Én gật đầu: “Tôi sẽ canh ở đây. Cố lên, Khải.”";
            case "Bà Tư Bán Thuốc":
                return choice.choiceId == "A"
                    ? "Bà Tư mỉm cười: “Cố lên chàng trai, cả thị trấn đang trông cậy vào con!”"
                    : "Bà Tư nhìn theo Khải: “Nếu đổi ý, thị trấn vẫn cần một người dám đứng ra giúp đỡ.”";
            default:
                return null;
        }
    }

    private void LoadDialogueNode(int index)
    {
        DemoDialogueNode[] nodes = activeNpc != null ? activeNpc.Dialogue : null;
        if (nodes == null || index < 0 || index >= nodes.Length)
        {
            currentNodeIndex = -1;
            pendingNodeIndex = -1;
            speakerText.text = activeNpc != null ? activeNpc.NpcName : "NPC";
            SetChoicesVisible(false);
            StartTyping("The conversation has ended.");
            return;
        }

        currentNodeIndex = index;
        DemoDialogueNode node = nodes[index];
        speakerText.text = activeNpc.NpcName;
        pendingNodeIndex = node.nextNodeIndex;
        StartTyping(node.text);
        ShowChoices(node.choices);
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
            if (label != null) label.text = visibleChoices[i].label;
        }

        if (visibleChoices.Length > choiceButtons.Length)
        {
            Debug.LogWarning(
                $"[DemoNpcDialogueController] Dialogue has {visibleChoices.Length} available choices, but only {choiceButtons.Length} choice buttons are assigned.",
                this);
        }
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
            speakerText.text = "YOU  /  " + activeNpc.NpcName;
            StartTyping("You: " + choice.label + "\n\n" + activeNpc.NpcName + ": " + choice.resultText);
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

    private void StartTyping(string text)
    {
        fullDialogueText = text ?? string.Empty;
        typedCharacterCount = 0f;
        isTyping = true;
        dialogueText.text = string.Empty;
        UpdateContinueHint();
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

        Sprite[] frames = activeNpc.PortraitFrames;
        if (frames == null || frames.Length == 0) return;

        portraitTimer += Time.unscaledDeltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, activeNpc.PortraitFrameRate);
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
