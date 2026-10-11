using System;
using UnityEngine;

[Serializable]
public class DemoDialogueChoice
{
    public string sessionId;
    public string choiceId;
    public string label;
    public string requiredFlag;
    [TextArea] public string resultText;
    public int nextNodeIndex = -1;
    public int ltDelta;
    public int qtDelta;
}

[Serializable]
public class DemoDialogueNode
{
    [TextArea(2, 5)] public string text;
    public int nextNodeIndex = -1;
    public DemoDialogueChoice[] choices;
}

public class DemoNpcDialogueProfile : MonoBehaviour
{
    [SerializeField] private string npcName = "Mira";
    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 2.25f;
    [Tooltip("Portrait sẽ lần lượt chạy các sprite này trong khung thoại. Có thể kéo frame animation vào đây.")]
    [SerializeField] private Sprite[] portraitFrames;
    [SerializeField, Min(0.1f)] private float portraitFrameRate = 4f;
    [Header("Dialogue Data")]
    [SerializeField] private DemoDialogueAsset dialogueAsset;
    [SerializeField] private DemoDialogueNode[] dialogue = new DemoDialogueNode[0];

    public string NpcName => dialogueAsset != null && !string.IsNullOrWhiteSpace(dialogueAsset.NpcName)
        ? dialogueAsset.NpcName
        : npcName;
    public float InteractionRange => interactionRange;
    public Sprite[] PortraitFrames => portraitFrames;
    public float PortraitFrameRate => portraitFrameRate;
    public DemoDialogueAsset DialogueAsset => dialogueAsset;
    public string PostDialogueText => dialogueAsset != null ? dialogueAsset.PostDialogueText : string.Empty;
    public DemoDialogueNode[] Dialogue => dialogueAsset != null ? dialogueAsset.Nodes : dialogue;

    public Sprite GetFallbackPortrait()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }
}
