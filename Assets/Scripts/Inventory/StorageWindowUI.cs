using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ rương: 2 lưới 24 ô (túi đồ người chơi + rương).
/// - Ô được clone từ slotTemplate (đã tắt enable).
/// - Kéo thả để đổi chỗ / hoán đổi / chuyển qua lại giữa 2 bên. Ô trống tự dồn lên.
/// - Mỗi ô có nút Lấy / Bỏ ra. Click ô để chọn (hiện viền + thông tin).
/// - Mọi thay đổi chỉ nằm trong bộ nhớ, CHỈ lưu file 1 lần khi đóng rương.
/// - Đang trang bị mà bỏ vào rương -> tự bỏ trang bị.
/// </summary>
public class StorageWindowUI : MonoBehaviour
{
    public enum Side { Player, Chest }

    public static StorageWindowUI Instance { get; private set; }
    public static bool AnyOpen => Instance != null && Instance.IsOpen;

    public static StorageWindowUI FindWindow()
    {
        if (Instance != null) return Instance;
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<StorageWindowUI>(true);
#endif
    }

    [Header("Khung chính")]
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform playerGridRoot;
    [SerializeField] private RectTransform chestGridRoot;
    [SerializeField] private TMP_Text playerTitleText;
    [SerializeField] private TMP_Text chestTitleText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button takeAllButton;
    [SerializeField] private Button storeAllButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text interactionPromptText;

    [Header("Ô mẫu (tắt enable, sẽ được clone)")]
    [SerializeField] private StorageSlotUI slotTemplate;
    [Tooltip("Ảnh dùng khi vật phẩm chưa có icon trong ItemDefinition")]
    [SerializeField] private Sprite missingIconSprite;

    [Header("Lưới ô đồ")]
    [SerializeField, Min(1)] private int columns = 6;
    [SerializeField] private Vector2 slotSize = new Vector2(88f, 88f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(6f, 6f);

    [Header("Khi mở rương")]
    [Tooltip("Kéo nút Menu (và các UI khác) vào đây để ẩn khi mở rương")]
    [SerializeField] private GameObject[] hideWhileOpen;
    [SerializeField] private bool pauseGameWhileOpen = true;

    private const int Capacity = PlayerSaveData.InventoryCapacity;
    private const int EndOfList = 100000;

    private StorageSlotUI[] playerSlots;
    private StorageSlotUI[] chestSlots;
    private StorageSlotUI selected;

    private PlayerProgressionService progress;
    private InventoryStorage current;
    private List<InventoryEntry> chestItems;
    private bool isOpen;
    private bool batchOpen;
    private bool pausedByUs;
    private bool[] hiddenWasActive;
    private InventoryStorage promptOwner;

    private Canvas rootCanvas;
    private Image dragGhost;
    private Coroutine messageRoutine;
    private readonly HashSet<string> warnedMissingIcon = new HashSet<string>();

    public bool IsOpen => isOpen;
    public bool IsOpenFor(InventoryStorage storage) => isOpen && current == storage;
    public bool HasInteractionPrompt => interactionPromptText != null;

    private List<InventoryEntry> PlayerList => progress.Data.inventory;

    // ------------------------------------------------------------
    // Khởi tạo
    // ------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[StorageWindowUI] Có nhiều hơn một cửa sổ kho đang được nạp. Bỏ qua bản trùng.", this);
            enabled = false;
            return;
        }

        if (panel == null || playerGridRoot == null || chestGridRoot == null || slotTemplate == null)
        {
            Debug.LogError("[StorageWindowUI] Thiếu tham chiếu (panel / grid / slotTemplate). Hãy gán trong Inspector.", this);
            enabled = false;
            return;
        }

        Instance = this;

        Canvas c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;

        slotTemplate.gameObject.SetActive(false);
        ConfigureGrid(playerGridRoot);
        ConfigureGrid(chestGridRoot);
        playerSlots = BuildSlots(playerGridRoot, Side.Player);
        chestSlots = BuildSlots(chestGridRoot, Side.Chest);

        if (takeAllButton != null) takeAllButton.onClick.AddListener(() => MoveAll(false));
        if (storeAllButton != null) storeAllButton.onClick.AddListener(() => MoveAll(true));
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (playerTitleText != null) playerTitleText.text = "Túi đồ";
        if (messageText != null) messageText.text = string.Empty;
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);

        panel.SetActive(false);
    }

    private void ConfigureGrid(RectTransform root)
    {
        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = slotSize;
        grid.spacing = slotSpacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
    }

    private StorageSlotUI[] BuildSlots(RectTransform root, Side side)
    {
        StorageSlotUI[] slots = new StorageSlotUI[Capacity];
        for (int i = 0; i < Capacity; i++)
        {
            StorageSlotUI slot = Instantiate(slotTemplate, root);
            slot.name = side + "Slot_" + (i + 1);
            slot.gameObject.SetActive(true);
            slot.Init(this, side, i);
            slots[i] = slot;
        }
        return slots;
    }

    private void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetInteractionPrompt(InventoryStorage storage, bool visible)
    {
        if (interactionPromptText == null || storage == null) return;

        if (visible)
        {
            promptOwner = storage;
            interactionPromptText.text = storage.InteractionPrompt;
            interactionPromptText.gameObject.SetActive(true);
        }
        else if (promptOwner == storage)
        {
            promptOwner = null;
            interactionPromptText.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------
    // Mở / đóng
    // ------------------------------------------------------------
    public void Open(InventoryStorage storage)
    {
        if (isOpen || storage == null || !enabled) return;

        progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        if (progress.Data.inventory == null) progress.Data.inventory = new List<InventoryEntry>();
        chestItems = GetOrCreateContainer(storage.ContainerId).items;

        Compact(PlayerList);
        Compact(chestItems);
        if (chestItems.Count > Capacity)
            Debug.LogWarning("[StorageWindowUI] Rương '" + storage.ContainerId + "' có nhiều hơn " + Capacity + " ô, các ô dư sẽ không hiển thị.");

        current = storage;
        isOpen = true;
        SetInteractionPrompt(storage, false);

        // Chặn lưu file trong lúc mở (kể cả autosave) - chỉ lưu khi đóng.
        progress.BeginStoryEventBatch();
        batchOpen = true;

        panel.SetActive(true);
        HideOtherUI(true);
        if (pauseGameWhileOpen && !GameplayPause.IsPaused)
        {
            GameplayPause.Pause();
            pausedByUs = true;
        }

        if (chestTitleText != null) chestTitleText.text = storage.DisplayName;
        if (messageText != null) messageText.text = string.Empty;
        SelectSlot(null);
        RefreshAll();
    }

    public void Close()
    {
        if (!isOpen) return;

        EndDrag();
        isOpen = false;
        current = null;
        SelectSlot(null);
        panel.SetActive(false);
        HideOtherUI(false);

        if (pausedByUs)
        {
            GameplayPause.Resume();
            pausedByUs = false;
        }

        if (progress != null && progress.Data != null)
        {
            Compact(PlayerList);
            if (chestItems != null) Compact(chestItems);
            progress.RevalidateEquipment();
            progress.Save();                    // bị hoãn vì đang trong batch...
        }

        if (batchOpen && progress != null)
        {
            batchOpen = false;
            progress.EndStoryEventBatch();      // ...và ghi file đúng 1 lần ở đây
        }

        chestItems = null;
    }

    private void HideOtherUI(bool hide)
    {
        if (hideWhileOpen == null) return;

        if (hide)
        {
            hiddenWasActive = new bool[hideWhileOpen.Length];
            for (int i = 0; i < hideWhileOpen.Length; i++)
            {
                if (hideWhileOpen[i] == null) continue;
                hiddenWasActive[i] = hideWhileOpen[i].activeSelf;
                hideWhileOpen[i].SetActive(false);
            }
        }
        else if (hiddenWasActive != null)
        {
            for (int i = 0; i < hideWhileOpen.Length && i < hiddenWasActive.Length; i++)
            {
                if (hideWhileOpen[i] != null && hiddenWasActive[i]) hideWhileOpen[i].SetActive(true);
            }
            hiddenWasActive = null;
        }
    }

    private StorageContainerData GetOrCreateContainer(string containerId)
    {
        PlayerSaveData data = progress.Data;
        if (data.storageContainers == null) data.storageContainers = new List<StorageContainerData>();

        StorageContainerData container = data.storageContainers.Find(c => c.containerId == containerId);
        if (container == null)
        {
            container = new StorageContainerData { containerId = containerId };
            data.storageContainers.Add(container);
        }
        if (container.items == null) container.items = new List<InventoryEntry>();
        return container;
    }

    // ------------------------------------------------------------
    // Hiển thị
    // ------------------------------------------------------------
    private void RefreshAll()
    {
        if (!isOpen) return;
        FillSlots(playerSlots, PlayerList, true);
        FillSlots(chestSlots, chestItems, false);
        UpdateInfo();
    }

    private void FillSlots(StorageSlotUI[] slots, List<InventoryEntry> list, bool isPlayer)
    {
        string equippedName = progress.Data.equippedItemName;
        for (int i = 0; i < slots.Length; i++)
        {
            InventoryEntry entry = list.Find(e => e.slotIndex == i);
            ItemDefinition def = entry != null ? ItemDefinition.Find(entry.itemName) : null;
            Sprite icon = entry != null ? ResolveIcon(entry.itemName, def) : null;
            bool equipped = isPlayer && entry != null
                && !string.IsNullOrEmpty(equippedName) && entry.itemName == equippedName;
            slots[i].SetEntry(entry, def, icon, equipped);
        }
    }

    /// <summary>Luôn trả về 1 ảnh: icon của item, hoặc ảnh dự phòng nếu item chưa có icon.</summary>
    private Sprite ResolveIcon(string itemName, ItemDefinition def)
    {
        if (def != null && def.icon != null) return def.icon;

        if (warnedMissingIcon.Add(itemName))
        {
            Debug.LogWarning("[StorageWindowUI] Vật phẩm '" + itemName + "' chưa có icon"
                + (def == null ? " (không tìm thấy ItemDefinition trong Resources/ItemDefinitions)." : "."));
        }
        return missingIconSprite;
    }

    public void SelectSlot(StorageSlotUI slot)
    {
        if (selected != null) selected.SetSelected(false);
        selected = slot;
        if (selected != null) selected.SetSelected(true);
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (infoText == null) return;

        if (selected == null || selected.Entry == null)
        {
            infoText.text = "Click ô để xem thông tin. Kéo thả để đổi chỗ hoặc chuyển giữa túi đồ và rương.";
            return;
        }

        InventoryEntry entry = selected.Entry;
        ItemDefinition def = ItemDefinition.Find(entry.itemName);
        StringBuilder sb = new StringBuilder();
        sb.Append("<b>").Append(entry.itemName).Append("</b>  x").Append(entry.amount);

        if (selected.Side == Side.Player && !string.IsNullOrEmpty(progress.Data.equippedItemName)
            && progress.Data.equippedItemName == entry.itemName)
            sb.Append("  <color=#6CFF8A>[Đang trang bị]</color>");

        if (def != null)
        {
            sb.Append("\n").Append(def.category);
            if (def.attackBonus > 0) sb.Append("  | ATK +").Append(def.attackBonus);
            if (def.defenseBonus > 0) sb.Append("  | DEF +").Append(def.defenseBonus);
            if (def.maxHealthBonus > 0) sb.Append("  | HP +").Append(def.maxHealthBonus);
            if (def.healAmount > 0) sb.Append("  | Hồi ").Append(def.healAmount);
            if (!string.IsNullOrWhiteSpace(def.description)) sb.Append("\n").Append(def.description);
        }

        infoText.text = sb.ToString();
    }

    private void ShowMessage(string text)
    {
        if (messageText == null) return;
        messageText.text = text;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        if (isActiveAndEnabled) messageRoutine = StartCoroutine(ClearMessageLater());
    }

    private IEnumerator ClearMessageLater()
    {
        yield return new WaitForSecondsRealtime(2f);
        if (messageText != null) messageText.text = string.Empty;
    }

    // ------------------------------------------------------------
    // Kéo thả
    // ------------------------------------------------------------
    public void BeginDrag(StorageSlotUI source, PointerEventData eventData)
    {
        SelectSlot(source);

        if (dragGhost == null)
        {
            GameObject go = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            Transform parent = rootCanvas != null ? rootCanvas.transform : transform;
            go.transform.SetParent(parent, false);
            dragGhost = go.GetComponent<Image>();
            dragGhost.raycastTarget = false;
            dragGhost.preserveAspect = true;
        }

        ItemDefinition def = ItemDefinition.Find(source.Entry.itemName);
        Sprite icon = ResolveIcon(source.Entry.itemName, def);
        dragGhost.sprite = icon;
        dragGhost.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        dragGhost.rectTransform.sizeDelta = slotSize * 0.8f;
        dragGhost.transform.SetAsLastSibling();
        dragGhost.gameObject.SetActive(true);
        UpdateDrag(eventData);
    }

    public void UpdateDrag(PointerEventData eventData)
    {
        if (dragGhost == null) return;
        RectTransform parent = dragGhost.transform.parent as RectTransform;
        if (parent == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
            dragGhost.rectTransform.localPosition = local;
    }

    public void EndDrag()
    {
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
    }

    public void HandleDrop(StorageSlotUI source, StorageSlotUI target)
    {
        if (!isOpen || source == null || target == null || source == target || !source.HasItem) return;

        List<InventoryEntry> srcList = ListOf(source.Side);
        List<InventoryEntry> dstList = ListOf(target.Side);
        InventoryEntry a = srcList.Find(e => e.slotIndex == source.Index);
        InventoryEntry b = dstList.Find(e => e.slotIndex == target.Index);
        if (a == null) return;

        if (source.Side == target.Side)
        {
            if (b == null)
            {
                a.slotIndex = EndOfList;            // ô trống -> xuống cuối, Compact dồn lại
            }
            else if (!TryMerge(a, b, srcList, false))
            {
                int tmp = a.slotIndex;              // hoán đổi vị trí
                a.slotIndex = b.slotIndex;
                b.slotIndex = tmp;
            }
        }
        else
        {
            bool fromPlayer = source.Side == Side.Player;

            if (b == null)
            {
                if (dstList.Count >= Capacity)
                {
                    ShowMessage(fromPlayer ? "Rương đã đầy" : "Túi đồ đã đầy");
                    return;
                }
                srcList.Remove(a);
                a.slotIndex = EndOfList;
                dstList.Add(a);
                progress.AdjustRareAmmoForTransfer(a.itemName, fromPlayer ? -a.amount : a.amount);
            }
            else if (!TryMerge(a, b, srcList, true))
            {
                // Hoán đổi giữa 2 bên
                int aSlot = a.slotIndex;
                int bSlot = b.slotIndex;
                srcList.Remove(a);
                dstList.Remove(b);
                a.slotIndex = bSlot;
                b.slotIndex = aSlot;
                dstList.Add(a);
                srcList.Add(b);
                progress.AdjustRareAmmoForTransfer(a.itemName, fromPlayer ? -a.amount : a.amount);
                progress.AdjustRareAmmoForTransfer(b.itemName, fromPlayer ? b.amount : -b.amount);
            }
        }

        Finish();
        SelectSlot(target);
    }

    /// <summary>Gộp a vào b nếu cùng tên và còn chỗ. Trả về true nếu đã gộp (một phần hoặc hết).</summary>
    private bool TryMerge(InventoryEntry a, InventoryEntry b, List<InventoryEntry> srcList, bool crossSide)
    {
        if (a.itemName != b.itemName) return false;

        int room = MaxStack(a.itemName) - b.amount;
        if (room <= 0) return false;

        int moved = Mathf.Min(a.amount, room);
        b.amount += moved;
        a.amount -= moved;
        if (a.amount <= 0) srcList.Remove(a);

        if (crossSide)
        {
            bool fromPlayer = srcList == PlayerList;
            progress.AdjustRareAmmoForTransfer(a.itemName, fromPlayer ? -moved : moved);
        }
        return true;
    }

    // ------------------------------------------------------------
    // Nút Lấy / Bỏ ra / Lấy tất cả / Bỏ tất cả
    // ------------------------------------------------------------
    public void TransferFromSlot(StorageSlotUI slot)
    {
        if (!isOpen || slot == null || slot.Entry == null) return;

        bool fromPlayer = slot.Side == Side.Player;
        InventoryEntry entry = slot.Entry;
        int moved = MoveStack(entry, fromPlayer);

        if (moved <= 0) ShowMessage(fromPlayer ? "Rương đã đầy" : "Túi đồ đã đầy");
        else if (entry.amount > 0) ShowMessage("Không đủ chỗ để chuyển hết");

        Finish();
    }

    private void MoveAll(bool fromPlayer)
    {
        if (!isOpen) return;

        List<InventoryEntry> from = fromPlayer ? PlayerList : chestItems;
        bool leftover = false;
        foreach (InventoryEntry entry in from.ToList())
        {
            MoveStack(entry, fromPlayer);
            if (entry.amount > 0) leftover = true;
        }

        if (leftover) ShowMessage(fromPlayer ? "Rương không đủ chỗ cho tất cả" : "Túi đồ không đủ chỗ cho tất cả");
        Finish();
    }

    /// <summary>Chuyển cả stack sang bên kia (gộp vào stack cùng tên trước). Trả về số lượng đã chuyển.</summary>
    private int MoveStack(InventoryEntry entry, bool fromPlayer)
    {
        List<InventoryEntry> from = fromPlayer ? PlayerList : chestItems;
        List<InventoryEntry> to = fromPlayer ? chestItems : PlayerList;

        string itemName = entry.itemName;
        int max = MaxStack(itemName);
        int remaining = entry.amount;

        foreach (InventoryEntry existing in to)
        {
            if (remaining <= 0) break;
            if (existing.itemName != itemName || existing.amount >= max) continue;

            int add = Mathf.Min(remaining, max - existing.amount);
            existing.amount += add;
            remaining -= add;
        }

        while (remaining > 0 && to.Count < Capacity)
        {
            int stack = Mathf.Min(remaining, max);
            to.Add(new InventoryEntry { itemName = itemName, amount = stack, slotIndex = EndOfList });
            remaining -= stack;
        }

        int moved = entry.amount - remaining;
        entry.amount = remaining;
        if (remaining <= 0) from.Remove(entry);

        if (moved > 0) progress.AdjustRareAmmoForTransfer(itemName, fromPlayer ? -moved : moved);
        return moved;
    }

    // ------------------------------------------------------------
    // Tiện ích
    // ------------------------------------------------------------
    private void Finish()
    {
        Compact(PlayerList);
        Compact(chestItems);
        progress.RevalidateEquipment();     // đồ đang equip bị bỏ vào rương -> tự bỏ trang bị
        RefreshAll();
    }

    private List<InventoryEntry> ListOf(Side side)
    {
        return side == Side.Player ? PlayerList : chestItems;
    }

    private static int MaxStack(string itemName)
    {
        ItemDefinition def = ItemDefinition.Find(itemName);
        return def != null ? Mathf.Max(1, def.maxStack) : 99;
    }

    /// <summary>Xoá entry lỗi, sắp theo slotIndex rồi đánh lại 0..n-1 (ô trống tự dồn lên).</summary>
    private static void Compact(List<InventoryEntry> list)
    {
        if (list == null) return;
        list.RemoveAll(e => e == null || e.amount <= 0 || string.IsNullOrWhiteSpace(e.itemName));
        List<InventoryEntry> sorted = list.OrderBy(e => e.slotIndex).ToList();   // OrderBy ổn định
        list.Clear();
        list.AddRange(sorted);
        for (int i = 0; i < list.Count; i++) list[i].slotIndex = i;
    }
}
