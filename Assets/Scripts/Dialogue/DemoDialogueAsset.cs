using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Bóng Ngủ/Dialogue Data", fileName = "Dialogue_")]
public sealed class DemoDialogueAsset : ScriptableObject
{
    [SerializeField] private string sessionId;
    [SerializeField] private string npcName;
    [SerializeField, TextArea(2, 5)] private string postDialogueText;
    [SerializeField, Tooltip("Tick when post-dialogue text contains a key from the language JSON files.")] private bool postDialogueTextIsLocalizationKey;
    [SerializeField] private DemoDialogueNode[] nodes = Array.Empty<DemoDialogueNode>();

    public string SessionId => sessionId;
    public string NpcName => npcName;
    public string PostDialogueText => postDialogueText;
    public bool PostDialogueTextIsLocalizationKey => postDialogueTextIsLocalizationKey;
    public DemoDialogueNode[] Nodes => nodes;
}
