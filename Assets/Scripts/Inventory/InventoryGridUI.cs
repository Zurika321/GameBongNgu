using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InventoryGridUI : MonoBehaviour
{
    public const int SlotCount = PlayerSaveData.InventoryCapacity;

    [SerializeField] private Transform gridRoot;
    [SerializeField] private Image selectedItemIcon;
    [SerializeField] private Text selectedItemName;
    [SerializeField] private Text selectedItemDescription;
    [SerializeField] private Text selectedItemStats;
    [SerializeField] private Text equippedWeaponLabel;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button useButton;
    [SerializeField] private Button readBookButton;
    [SerializeField] private GameObject bookReadPanel;
    [SerializeField] private Text bookReadText;
    [SerializeField] private Button closeBookButton;
    [SerializeField] private Text inventoryStatus;

    private readonly InventorySlotView[] slots = new InventorySlotView[SlotCount];
    private int selectedSlot = -1;
    private bool initialized;

    private void Awake()
    {
        BuildGridIfNeeded();
        if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
        if (useButton != null) useButton.onClick.AddListener(UseSelected);
        if (readBookButton != null) readBookButton.onClick.AddListener(ReadSelectedBook);
        if (closeBookButton != null) closeBookButton.onClick.AddListener(CloseBook);
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
        initialized = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (initialized) Refresh();
    }

    public void RefreshInventory()
    {
        if (initialized) Refresh();
    }

    private void Update()
    {
        if (initialized && gameObject.activeInHierarchy) Refresh();
    }

    public bool HasItemAt(int slotIndex)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        return progress != null && progress.Data.inventory != null
            && progress.Data.inventory.Exists(entry => entry.slotIndex == slotIndex && entry.amount > 0);
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        selectedSlot = slotIndex;
        Refresh();
    }

    public void MoveItem(int fromSlot, int toSlot)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || !progress.Data.MoveInventoryItem(fromSlot, toSlot)) return;
        selectedSlot = toSlot;
        progress.Save();
        Refresh();
    }

    private void BuildGridIfNeeded()
    {
        if (gridRoot == null)
        {
            Debug.LogError("[InventoryGridUI] Grid Root is not assigned.", this);
            enabled = false;
            return;
        }

        GridLayoutGroup layout = gridRoot.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 6;
        layout.cellSize = new Vector2(66f, 66f);
        layout.spacing = new Vector2(7f, 7f);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;

        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slotObject = new GameObject($"Inventory Slot {i + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
            slotObject.transform.SetParent(gridRoot, false);

            Image background = slotObject.GetComponent<Image>();
            background.color = new Color(0.1f, 0.11f, 0.13f, 1f);
            Button button = slotObject.GetComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateImage(slotObject.transform, "Item Icon", new Vector2(48f, 48f), new Vector2(0f, 4f));
            icon.raycastTarget = false;
            Text count = CreateText(slotObject.transform, "Stack Count", 13, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(-4f, 3f), new Vector2(32f, 22f), Color.white);
            Text equipped = CreateText(slotObject.transform, "Equipment Badge", 8, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(62f, 16f), new Color(1f, 0.85f, 0.3f));

            InventorySlotView view = slotObject.AddComponent<InventorySlotView>();
            view.Initialize(this, i, icon, count, equipped);
            int capturedSlot = i;
            button.onClick.AddListener(() => SelectSlot(capturedSlot));
            slots[i] = view;
        }
    }

    private void Refresh()
    {
        if (!initialized && gridRoot == null) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;
        if (progress.Data.inventory == null) progress.Data.inventory = new List<InventoryEntry>();

        if (selectedSlot >= 0 && !HasItemAt(selectedSlot)) selectedSlot = -1;

        for (int i = 0; i < SlotCount; i++)
        {
            InventoryEntry entry = progress.Data.inventory.Find(item => item.slotIndex == i);
            ItemDefinition definition = entry != null ? ItemDefinition.Find(entry.itemName) : null;
            bool isEquipped = entry != null && entry.itemName == progress.Data.equippedItemName;
            slots[i].SetItem(definition, entry != null ? entry.amount : 0, isEquipped, selectedSlot == i);
        }

        RefreshSelectedItem(progress);
    }

    private void RefreshSelectedItem(PlayerProgressionService progress)
    {
        InventoryEntry entry = selectedSlot >= 0
            ? progress.Data.inventory.Find(item => item.slotIndex == selectedSlot)
            : null;
        ItemDefinition definition = entry != null ? ItemDefinition.Find(entry.itemName) : null;

        if (selectedItemIcon != null)
        {
            selectedItemIcon.sprite = definition != null ? definition.icon : null;
            selectedItemIcon.enabled = definition != null && definition.icon != null;
        }
        if (selectedItemName != null)
            selectedItemName.text = definition != null ? definition.itemName : "Chọn vật phẩm";
        if (selectedItemDescription != null)
            selectedItemDescription.text = definition != null
                ? definition.description
                : "Chọn một ô để xem thông tin và thao tác vật phẩm.";
        if (selectedItemStats != null)
            selectedItemStats.text = definition != null ? FormatStats(definition) : string.Empty;
        if (equippedWeaponLabel != null)
        {
            ItemDefinition equipped = ItemDefinition.Find(progress.Data.equippedItemName);
            equippedWeaponLabel.text = equipped != null
                ? $"ĐANG DÙNG: {equipped.itemName}"
                : "CHƯA TRANG BỊ VŨ KHÍ";
        }

        bool hasSelection = entry != null && definition != null;
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(hasSelection && definition.IsEquippable);
            Text label = equipButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = entry != null && entry.itemName == progress.Data.equippedItemName
                    ? "BỎ TRANG BỊ"
                    : "TRANG BỊ";
        }
        if (useButton != null)
            useButton.gameObject.SetActive(hasSelection && definition.healAmount > 0);
        if (readBookButton != null)
            readBookButton.gameObject.SetActive(hasSelection && definition.isReadable);
        if (inventoryStatus != null)
        {
            int usedSlots = progress.Data.inventory.Count;
            inventoryStatus.text = $"TÚI ĐỒ  {usedSlots}/{SlotCount} ô  •  Kéo thả để sắp xếp";
        }
    }

    private static string FormatStats(ItemDefinition definition)
    {
        List<string> stats = new List<string>();
        if (definition.attackBonus > 0) stats.Add($"+{definition.attackBonus} Tấn công");
        if (definition.defenseBonus > 0) stats.Add($"+{definition.defenseBonus} Phòng thủ");
        if (definition.maxHealthBonus > 0) stats.Add($"+{definition.maxHealthBonus} HP tối đa");
        if (definition.healAmount > 0) stats.Add($"Hồi {definition.healAmount} HP");
        if (stats.Count == 0) stats.Add(definition.rarity);
        return string.Join("\n", stats);
    }

    private void EquipSelected()
    {
        if (selectedSlot < 0) return;
        PlayerProgressionService.Instance.ToggleEquipAtSlot(selectedSlot);
        Refresh();
    }

    private void UseSelected()
    {
        if (selectedSlot < 0) return;
        PlayerProgressionService.Instance.UseItemAtSlot(selectedSlot);
        Refresh();
    }

    private void ReadSelectedBook()
    {
        InventoryEntry entry = GetSelectedEntry();
        if (entry != null) OpenReadableItem(entry.itemName);
    }

    public bool OpenReadableItem(string itemName)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        ItemDefinition definition = ItemDefinition.Find(itemName);
        if (progress == null || definition == null || !definition.isReadable
            || progress.Data.GetItemAmount(itemName) <= 0)
            return false;

        DemoPauseMenu menu = FindAnyObjectByType<DemoPauseMenu>();
        if (menu == null)
        {
            Debug.LogError("[InventoryGridUI] Cannot open the book: DemoPauseMenu is missing.");
            return false;
        }

        menu.OpenInventory();
        if (bookReadText != null) bookReadText.text = definition.readingText;
        if (bookReadPanel != null) bookReadPanel.SetActive(true);
        return true;
    }

    private void CloseBook()
    {
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
    }

    private InventoryEntry GetSelectedEntry()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data.inventory == null || selectedSlot < 0) return null;
        return progress.Data.inventory.Find(item => item.slotIndex == selectedSlot);
    }

    private static Image CreateImage(Transform parent, string objectName, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return child.GetComponent<Image>();
    }

    private static Text CreateText(Transform parent, string objectName, int size, TextAnchor alignment,
        Vector2 anchor, Vector2 position, Vector2 rectSize, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        Text text = child.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
