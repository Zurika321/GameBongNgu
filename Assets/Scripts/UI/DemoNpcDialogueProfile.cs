using System;
using UnityEngine;

[Serializable]
public class DemoDialogueChoice
{
    [RegistryId(RegistryIdType.Session)] public string sessionId;
    public string choiceId;
    [Tooltip("Tick when label contains a key from Assets/transitions/<language>.json; otherwise it is displayed as written.")]
    public bool labelIsLocalizationKey;
    public string label;
    [RegistryId(RegistryIdType.Flag)] public string requiredFlag;
    [Tooltip("Ẩn lựa chọn nếu người chơi không có vật phẩm này trong inventory. Để trống nếu không yêu cầu vật phẩm.")]
    [RegistryId(RegistryIdType.Item)] public string requiredItemName;
    [Min(1), Tooltip("Minimum inventory quantity required to show this choice.")]
    public int requiredItemAmount = 1;
    [TextArea, Tooltip("Optional line shown after selecting this choice.")] public string resultText;
    [Tooltip("Tick when resultText contains a localization key.")]
    public bool resultTextIsLocalizationKey;
    [Tooltip("Speaker ID for resultText. Use the player ID or an ID from Dialogue Speakers.")]
    public string resultSpeakerId;
    public int nextNodeIndex = -1;
    public int ltDelta;
    public int qtDelta;
}

[Serializable]
public class DemoDialogueSpeaker
{
    [Tooltip("ID referenced by the speakerId field on dialogue nodes.")]
    public string id;
    public string characterName;
    [Tooltip("Optional scene character. If assigned, it faces the player and plays idle during dialogue.")]
    public GameObject character;
    public Sprite[] portraitFrames = Array.Empty<Sprite>();
    [Min(0.1f)] public float portraitFrameRate = 4f;
}

[Serializable]
public class DemoDialogueNode
{
    [Tooltip("Matches an entry in DemoNpcDialogueController Dialogue Speakers. Leave empty to use the interacting NPC.")]
    public string speakerId;
    [Tooltip("Tick when text contains a key from Assets/transitions/<language>.json; otherwise it is displayed as written.")]
    public bool textIsLocalizationKey;
    [TextArea(2, 5)] public string text;
    public int nextNodeIndex = -1;
    public DemoDialogueChoice[] choices;
}

public class DemoNpcDialogueProfile : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Tooltip("Set to 0 or less to disable E interaction.")] private float interactionRange = 2.25f;

    [Header("Dialogue Data")]
    [SerializeField] private DemoDialogueAsset dialogueAsset;
    [SerializeField] private DemoDialogueNode[] dialogue = new DemoDialogueNode[0];

    public float InteractionRange => interactionRange;
    public DemoDialogueAsset DialogueAsset => dialogueAsset;
    public string PostDialogueText => dialogueAsset != null ? dialogueAsset.PostDialogueText : string.Empty;
    public bool PostDialogueTextIsLocalizationKey => dialogueAsset != null && dialogueAsset.PostDialogueTextIsLocalizationKey;
    public DemoDialogueNode[] Dialogue => dialogueAsset != null ? dialogueAsset.Nodes : dialogue;
}