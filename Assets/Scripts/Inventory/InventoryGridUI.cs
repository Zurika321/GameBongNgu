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
    [SerializeField] private Button skillTabButton;

    [Header("Slot appearance / layout")]
    [Tooltip("Sprite khung dùng cho mọi ô inventory. Để trống để dùng nền màu mặc định.")]
    [SerializeField] private Sprite slotFrameSprite;
    [SerializeField] private Image.Type slotFrameType = Image.Type.Sliced;
    [SerializeField, Min(1)] private int columns = 6;
    [SerializeField] private Vector2 slotSize = new Vector2(66f, 66f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(7f, 7f);
    [SerializeField] private RectOffset slotPadding;
    [SerializeField] private Color emptySlotColor = new Color(0.1f, 0.11f, 0.13f, 1f);
    [SerializeField] private Color itemSlotColor = new Color(0.24f, 0.22f, 0.2f, 1f);
    [SerializeField] private Color selectedSlotColor = new Color(0.7f, 0.48f, 0.2f, 1f);
    [SerializeField] private bool drawSlotOutline = true;
    [SerializeField] private Color slotOutlineColor = new Color(0.56f, 0.43f, 0.25f, 1f);
    [SerializeField] private Vector2 slotOutlineDistance = new Vector2(1f, -1f);

    private readonly InventorySlotView[] slots = new InventorySlotView[SlotCount];
    private int selectedSlot = -1;
    private bool initialized;
    private bool showingSkills;

    private void Awake()
    {
        if (slotPadding == null)
            slotPadding = new RectOffset(8, 8, 8, 8);

        BuildGridIfNeeded();
        if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
        if (useButton != null) useButton.onClick.AddListener(UseSelected);
        if (readBookButton != null) readBookButton.onClick.AddListener(ReadSelectedBook);
        if (closeBookButton != null) closeBookButton.onClick.AddListener(CloseBook);
        if (skillTabButton != null) skillTabButton.onClick.AddListener(ToggleInventoryTab);
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
        initialized = true;
        UpdateTabButtonLabel();
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
        return !showingSkills && progress != null && progress.Data.inventory != null
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
        if (showingSkills || progress == null || !progress.Data.MoveInventoryItem(fromSlot, toSlot)) return;
        selectedSlot = toSlot;
        progress.Save();
        Refresh();
    }

    internal Color GetSlotColor(bool hasItem, bool isSelected)
    {
        if (isSelected) return selectedSlotColor;
        return hasItem ? itemSlotColor : emptySlotColor;
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
        layout.constraintCount = Mathf.Max(1, columns);
        layout.cellSize = slotSize;
        layout.spacing = slotSpacing;
        layout.padding = slotPadding;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;

        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slotObject = new GameObject($"Inventory Slot {i + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
            slotObject.transform.SetParent(gridRoot, false);

            Image background = slotObject.GetComponent<Image>();
            background.sprite = slotFrameSprite;
            background.type = slotFrameSprite != null ? slotFrameType : Image.Type.Simple;
            background.color = GetSlotColor(false, false);
            Outline outline = slotObject.AddComponent<Outline>();
            outline.effectColor = slotOutlineColor;
            outline.effectDistance = slotOutlineDistance;
            outline.enabled = drawSlotOutline;
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

        if (showingSkills)
        {
            RefreshSkillGrid(progress);
            return;
        }
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

    private void RefreshSkillGrid(PlayerProgressionService progress)
    {
        List<string> unlockedSkills = progress.Data.unlockedSkills;
        for (int i = 0; i < SlotCount; i++)
        {
            if (i < PlayerSaveData.EquippedSkillCapacity)
            {
                EquippedSkillData equipped = progress.GetEquippedSkillAtSlot(i);
                string title = equipped != null ? equipped.skillName : $"Ô KỸ NĂNG {i + 1}";
                string badge = equipped != null ? $"PHÍM {equipped.inputKey.ToUpperInvariant()}" : "TRỐNG";
                slots[i].SetSkill(title, badge, equipped != null, selectedSlot == i);
                continue;
            }
            int skillIndex = i - PlayerSaveData.EquippedSkillCapacity;
            if (unlockedSkills != null && skillIndex < unlockedSkills.Count
                && !string.IsNullOrWhiteSpace(unlockedSkills[skillIndex]))
            {
                string skillName = unlockedSkills[skillIndex];
                bool equipped = progress.GetEquippedSkillIndex(skillName) >= 0;
                slots[i].SetSkill(skillName, equipped ? "ĐÃ TRANG BỊ" : string.Empty, true, selectedSlot == i);
            }
            else
                slots[i].SetSkill(string.Empty, string.Empty, false, selectedSlot == i);
        }
        RefreshSelectedSkill(progress);
    }

    private void RefreshSelectedItem(PlayerProgressionService progress)
    {
        if (showingSkills)
        {
            RefreshSelectedSkill(progress);
            return;
        }
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

    private string GetSelectedSkillName(PlayerProgressionService progress)
    {
        if (progress == null || selectedSlot < 0 || selectedSlot >= SlotCount) return null;
        if (selectedSlot < PlayerSaveData.EquippedSkillCapacity)
            return progress.GetEquippedSkillAtSlot(selectedSlot)?.skillName;
        int skillIndex = selectedSlot - PlayerSaveData.EquippedSkillCapacity;
        List<string> unlockedSkills = progress.Data.unlockedSkills;
        return unlockedSkills != null && skillIndex < unlockedSkills.Count ? unlockedSkills[skillIndex] : null;
    }

    private void RefreshSelectedSkill(PlayerProgressionService progress)
    {
        string skillName = GetSelectedSkillName(progress);
        bool hasSelection = !string.IsNullOrWhiteSpace(skillName);
        SkillData definition = hasSelection ? progress.GetSkillDefinition(skillName) : null;
        int equippedSlot = hasSelection ? progress.GetEquippedSkillIndex(skillName) : -1;
        EquippedSkillData equipped = equippedSlot >= 0 ? progress.GetEquippedSkillAtSlot(equippedSlot) : null;
        if (selectedItemIcon != null) { selectedItemIcon.sprite = null; selectedItemIcon.enabled = false; }
        if (selectedItemName != null) selectedItemName.text = hasSelection ? skillName : "Chọn kỹ năng";
        if (selectedItemDescription != null)
        {
            if (!hasSelection)
                selectedItemDescription.text = "Ba ô đầu là ô trang bị. Chọn kỹ năng đã mở khóa bên dưới rồi bấm TRANG BỊ.";
            else
            {
                string description = definition != null && !string.IsNullOrWhiteSpace(definition.effect)
                    ? definition.effect : "Kỹ năng đã được mở khóa.";
                if (equipped == null && !progress.CanEquipSkill(skillName))
                    description += "\nChưa có hành động chiến đấu được cấu hình cho kỹ năng này.";
                if (equipped == null && progress.GetEquippedSkillCount() >= PlayerSaveData.EquippedSkillCapacity
                    && progress.CanEquipSkill(skillName))
                    description += "\nBa ô đã đầy. Gỡ một kỹ năng trước khi trang bị thêm.";
                selectedItemDescription.text = description;
            }
        }
        if (selectedItemStats != null)
        {
            if (definition == null)
                selectedItemStats.text = equipped != null
                    ? $"Đang trang bị ở ô {equippedSlot + 1} | Phím: {equipped.inputKey.ToUpperInvariant()}"
                    : "Chưa có mô tả bổ sung.";
            else
            {
                selectedItemStats.text = $"{definition.branch} | {definition.tier}\nMở khóa: {definition.unlockCondition}";
                selectedItemStats.text += equipped != null
                    ? $"\nÔ trang bị: {equippedSlot + 1} | Phím: {equipped.inputKey.ToUpperInvariant()}"
                    : "\nChưa trang bị";
            }
            if (equipped != null)
                selectedItemStats.text += "\nBấm nút PHÍM để đổi phím (1-9, SPACE).";
        }
        if (equippedWeaponLabel != null)
            equippedWeaponLabel.text = $"KỸ NĂNG ĐÃ TRANG BỊ: {progress.GetEquippedSkillCount()}/{PlayerSaveData.EquippedSkillCapacity}";
        bool canEquip = hasSelection && progress.CanEquipSkill(skillName);
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(hasSelection && (equipped != null || (canEquip && progress.GetEquippedSkillCount() < PlayerSaveData.EquippedSkillCapacity)));
            Text label = equipButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = equipped != null ? "GỠ KỸ NĂNG" : "TRANG BỊ";
        }
        if (useButton != null)
        {
            useButton.gameObject.SetActive(equipped != null);
            Text label = useButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = equipped != null ? $"PHÍM: {equipped.inputKey.ToUpperInvariant()}" : "CHỌN PHÍM";
        }
        if (readBookButton != null) readBookButton.gameObject.SetActive(false);
        if (inventoryStatus != null)
            inventoryStatus.text = $"KỸ NĂNG: {progress.Data.unlockedSkills?.Count ?? 0} ĐÃ MỞ | TỐI ĐA 3 Ô | PHÍM 1-9 / SPACE";
    }

    private void ToggleInventoryTab()
    {
        showingSkills = !showingSkills;
        selectedSlot = -1;
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
        UpdateTabButtonLabel();
        Refresh();
    }

    private void UpdateTabButtonLabel()
    {
        if (skillTabButton == null) return;
        Text label = skillTabButton.GetComponentInChildren<Text>(true);
        if (label != null) label.text = showingSkills ? "VẬT PHẨM" : "KỸ NĂNG";
    }

    private void ToggleSelectedSkillEquipment()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        string skillName = GetSelectedSkillName(progress);
        if (progress == null || string.IsNullOrWhiteSpace(skillName)) return;
        if (progress.GetEquippedSkillIndex(skillName) >= 0) progress.UnequipSkill(skillName);
        else progress.EquipSkill(skillName);
        Refresh();
    }

    private void CycleSelectedSkillKey()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        string skillName = GetSelectedSkillName(progress);
        if (progress != null && !string.IsNullOrWhiteSpace(skillName))
            progress.CycleEquippedSkillKey(skillName);
        Refresh();
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
        if (showingSkills) { ToggleSelectedSkillEquipment(); return; }
        if (selectedSlot < 0) return;
        PlayerProgressionService.Instance.ToggleEquipAtSlot(selectedSlot);
        Refresh();
    }

    private void UseSelected()
    {
        if (showingSkills) { CycleSelectedSkillKey(); return; }
        if (selectedSlot < 0) return;
        PlayerProgressionService.Instance.UseItemAtSlot(selectedSlot);
        Refresh();
    }

    private void ReadSelectedBook()
    {
        if (showingSkills) return;
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

        showingSkills = false;
        UpdateTabButtonLabel();
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
