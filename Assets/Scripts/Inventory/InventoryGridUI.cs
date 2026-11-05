using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InventoryGridUI : MonoBehaviour
{
    public const int SlotCount = PlayerSaveData.InventoryCapacity;

    [SerializeField] private Transform gridRoot;

    [Header("5. Panel thông tin (cố định, chứa 3 nút Trang bị / Đọc / Dùng)")]
    [Tooltip("PC: rê chuột vào ô để xem nhanh (chưa bấm được nút), bấm vào ô để ghim và dùng nút. Mobile: bấm vào ô là hiện. Để trống thì panel luôn hiện như cũ.")]
    [SerializeField] private GameObject infoPanel;
    [Tooltip("Bật để thử kiểu cảm ứng (bấm mới hiện panel) ngay trên PC.")]
    [SerializeField] private bool forceTouchMode;
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

    [Header("Hai nút tab (đặt trong scene)")]
    [Tooltip("Tab VẬT PHẨM. Chưa gán thì giữ kiểu cũ: Skill Tab Button là nút bật/tắt giữa vật phẩm và kỹ năng.")]
    [SerializeField] private Button itemTabButton;
    [Tooltip("Tab KỸ NĂNG: chỉ hiện kỹ năng kích hoạt chủ động. Passive không hiện ở đây, xem ở PlayerPassiveSkillController.")]
    [SerializeField] private Button skillTabButton;
    [SerializeField] private Color tabSelectedColor = new Color(0.7f, 0.48f, 0.2f, 1f);
    [SerializeField] private Color tabUnselectedColor = new Color(0.24f, 0.22f, 0.2f, 1f);

    [Header("1. Ô vũ khí đang trang bị")]
    [Tooltip("Có vũ khí thì hiện icon vũ khí, không có thì tắt Image này.")]
    [SerializeField] private Image equippedWeaponImage;

    [Header("2. Ba ô kỹ năng đã trang bị (bấm vào để gỡ / đổi phím)")]
    [SerializeField] private Button[] equippedSkillSlots = new Button[PlayerSaveData.EquippedSkillCapacity];

    [Header("Popup của ô kỹ năng (chỉ hiện khi ô đang có skill)")]
    [SerializeField] private GameObject skillSlotPopup;
    [SerializeField] private Button skillUnequipButton;
    [Tooltip("Gõ 1-9, a-z hoặc space rồi Enter / bấm ra ngoài. Trùng phím với skill khác thì tự giữ phím cũ.")]
    [SerializeField] private InputField skillKeyInput;
    [SerializeField] private bool moveSkillPopupToSlot = true;
    [SerializeField] private Vector2 skillPopupOffset = new Vector2(0f, 100f);

    [Header("Kéo thả")]
    [SerializeField] private Vector2 dragGhostSize = new Vector2(56f, 56f);

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
    // Skill đang hiển thị ở tab Kỹ năng (chỉ skill chủ động, đúng thứ tự trong save). Dựng lại mỗi lần Refresh.
    private readonly List<string> visibleSkillIds = new List<string>();
    private int selectedSlot = -1;      // ô đang được ghim (bấm vào)
    private int hoverSlot = -1;         // ô đang rê chuột (chỉ PC)
    private int selectedSkillSlot = -1; // ô kỹ năng đã trang bị đang mở popup
    private enum DragKind { None, Item, Skill, Weapon }
    private DragKind dragKind;
    private int dragItemSlot = -1;
    private string dragSkillName;       // skillId của skill đang kéo
    private int dragFromSkillSlot = -1; // -1 = kéo từ lưới, >=0 = kéo từ ô kỹ năng đã trang bị
    private RectTransform dragGhost;
    private Image dragGhostImage;
    private Text dragGhostLabel;
    private Image[] equippedSkillSlotIcons;
    private bool initialized;
    private bool showingSkills;
    private string keyMessage;
    private float keyMessageUntil;

    private bool UseTouchInteraction => forceTouchMode || Application.isMobilePlatform;

    private void Awake()
    {
        if (slotPadding == null)
            slotPadding = new RectOffset(8, 8, 8, 8);

        BuildGridIfNeeded();
        if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
        if (useButton != null) useButton.onClick.AddListener(UseSelected);
        if (readBookButton != null) readBookButton.onClick.AddListener(ReadSelectedBook);
        if (closeBookButton != null) closeBookButton.onClick.AddListener(CloseBook);
        if (itemTabButton != null) itemTabButton.onClick.AddListener(OnItemTabClicked);
        if (skillTabButton != null) skillTabButton.onClick.AddListener(OnSkillTabClicked);
        SetupSkillSlotControls();
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
        if (infoPanel != null) infoPanel.SetActive(false);
        initialized = true;
        ApplyTabState();
        Refresh();
    }

    private void OnEnable()
    {
        if (initialized) Refresh();
    }

    private void OnDisable()
    {
        hoverSlot = -1;
        EndDrag();
    }

    public void RefreshInventory()
    {
        if (initialized) Refresh();
    }

    private void Update()
    {
        if (initialized && gameObject.activeInHierarchy) Refresh();
    }

    // ------------------------------------------------------------------ truy vấn ô
    public bool HasItemAt(int slotIndex)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        return !showingSkills && progress != null && progress.Data.inventory != null
            && progress.Data.inventory.Exists(entry => entry.slotIndex == slotIndex && entry.amount > 0);
    }

    /// <summary>skillId của skill ở ô lưới (theo danh sách đã lọc chỉ gồm skill chủ động). Null nếu ô trống.</summary>
    private string GetSkillIdAt(int gridIndex)
    {
        if (gridIndex < 0 || gridIndex >= visibleSkillIds.Count) return null;
        return string.IsNullOrWhiteSpace(visibleSkillIds[gridIndex]) ? null : visibleSkillIds[gridIndex];
    }

    private void RebuildVisibleSkills(PlayerProgressionService progress)
    {
        if (showingSkills && progress != null) progress.CollectInventorySkillIds(visibleSkillIds);
        else visibleSkillIds.Clear();
    }

    private bool HasContentAt(PlayerProgressionService progress, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;
        return showingSkills ? GetSkillIdAt(slotIndex) != null : HasItemAt(slotIndex);
    }

    // ------------------------------------------------------------------ tương tác ô
    /// <summary>Bấm vào ô: ghim / bỏ ghim. Gọi từ Button của ô.</summary>
    public void OnSlotClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        RebuildVisibleSkills(progress);
        if (!HasContentAt(progress, slotIndex) || selectedSlot == slotIndex) selectedSlot = -1;
        else selectedSlot = slotIndex;
        Refresh();
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        selectedSlot = slotIndex;
        Refresh();
    }

    public void SetHoverSlot(int slotIndex)
    {
        hoverSlot = slotIndex;
    }

    public void ClearHoverSlot(int slotIndex)
    {
        if (hoverSlot == slotIndex) hoverSlot = -1;
    }

    public void MoveItem(int fromSlot, int toSlot)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (showingSkills || progress == null) return;

        InventoryEntry movedEntry = progress.Data.inventory.Find(item => item.slotIndex == fromSlot);
        InventoryEntry targetEntry = progress.Data.inventory.Find(item => item.slotIndex == toSlot);
        if (!progress.Data.MoveInventoryItem(fromSlot, toSlot)) return;

        // Ô đích trống thì vật phẩm xuống cuối danh sách; không để ô trống ở giữa
        progress.CompactInventory();
        InventoryEntry focus = movedEntry != null && progress.Data.inventory.Contains(movedEntry) ? movedEntry : targetEntry;
        selectedSlot = focus != null && progress.Data.inventory.Contains(focus) ? focus.slotIndex : -1;
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
            button.onClick.AddListener(() => OnSlotClicked(capturedSlot));
            slots[i] = view;
        }
    }

    // ------------------------------------------------------------------ refresh
    private void Refresh()
    {
        if (!initialized && gridRoot == null) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;
        if (progress.Data.inventory == null) progress.Data.inventory = new List<InventoryEntry>();

        RebuildVisibleSkills(progress);
        if (selectedSlot >= 0 && !HasContentAt(progress, selectedSlot)) selectedSlot = -1;

        RefreshWeaponImage(progress);
        RefreshEquippedSkillSlots(progress);

        if (showingSkills) RefreshSkillGrid(progress);
        else RefreshItemGrid(progress);

        RefreshInfoPanel(progress);
        RefreshSkillPopup(progress);
        RefreshStatus(progress);
    }

    private void RefreshWeaponImage(PlayerProgressionService progress)
    {
        if (equippedWeaponImage == null) return;
        string weaponName = progress.Data.equippedItemName;
        ItemDefinition weapon = string.IsNullOrWhiteSpace(weaponName) ? null : ItemDefinition.Find(weaponName);
        bool hasWeapon = weapon != null && weapon.icon != null;
        // Không tắt component Image (tắt thì không nhận được kéo thả); ẩn bằng alpha = 0
        equippedWeaponImage.enabled = true;
        equippedWeaponImage.raycastTarget = true;
        equippedWeaponImage.sprite = hasWeapon ? weapon.icon : null;
        equippedWeaponImage.color = new Color(1f, 1f, 1f, hasWeapon ? 1f : 0f);
    }

    private void RefreshItemGrid(PlayerProgressionService progress)
    {
        // Dùng hết / mất vật phẩm để lại ô trống thì dồn lên, giữ nguyên vật phẩm đang chọn
        InventoryEntry selectedEntry = selectedSlot >= 0
            ? progress.Data.inventory.Find(item => item.slotIndex == selectedSlot) : null;
        if (progress.CompactInventory())
        {
            progress.Save();
            selectedSlot = selectedEntry != null ? selectedEntry.slotIndex : -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            InventoryEntry entry = progress.Data.inventory.Find(item => item.slotIndex == i);
            ItemDefinition definition = entry != null ? ItemDefinition.Find(entry.itemName) : null;
            bool isEquipped = entry != null && entry.itemName == progress.Data.equippedItemName;
            slots[i].SetItem(definition, entry != null ? entry.amount : 0, isEquipped, selectedSlot == i);
        }
    }

    private void RefreshSkillGrid(PlayerProgressionService progress)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            string skillId = GetSkillIdAt(i);
            if (skillId != null)
            {
                string badge = string.Empty;
                if (progress.GetEquippedSkillIndex(skillId) >= 0) badge = "ĐÃ TRANG BỊ";
                else if (!progress.IsWeaponRequirementMet(skillId)) badge = "CẦN VŨ KHÍ";
                slots[i].SetSkill(progress.GetSkillDisplayName(skillId), badge, true, selectedSlot == i, progress.GetSkillIcon(skillId));
            }
            else
                slots[i].SetSkill(string.Empty, string.Empty, false, selectedSlot == i);
        }
    }

    // ------------------------------------------------------------------ 3 ô kỹ năng + popup
    private void SetupSkillSlotControls()
    {
        if (equippedSkillSlots != null)
        {
            equippedSkillSlotIcons = new Image[equippedSkillSlots.Length];
            for (int i = 0; i < equippedSkillSlots.Length; i++)
            {
                Button slotButton = equippedSkillSlots[i];
                if (slotButton == null) continue;
                int captured = i;
                slotButton.onClick.AddListener(() => OnEquippedSkillSlotClicked(captured));

                // Ưu tiên dùng Image con có sẵn trong button; không có mới tự tạo
                Image icon = null;
                foreach (Image candidate in slotButton.GetComponentsInChildren<Image>(true))
                {
                    if (candidate.gameObject == slotButton.gameObject) continue; // bỏ nền của chính button
                    icon = candidate;
                    break;
                }
                if (icon == null)
                {
                    icon = CreateImage(slotButton.transform, "Skill Icon", Vector2.zero, Vector2.zero);
                    RectTransform iconRect = icon.rectTransform;
                    iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
                    iconRect.offsetMin = new Vector2(6f, 6f); iconRect.offsetMax = new Vector2(-6f, -6f);
                }
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false; // chỉ bật khi skill có ảnh, tránh che mất chữ
                equippedSkillSlotIcons[i] = icon;

                // Chữ luôn nằm trên ảnh
                Text label = slotButton.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.raycastTarget = false;
                    label.transform.SetAsLastSibling();
                }

                // Nhận skill kéo vào + kéo skill sang ô khác để đổi chỗ
                InventoryDropTarget target = slotButton.GetComponent<InventoryDropTarget>();
                if (target == null) target = slotButton.gameObject.AddComponent<InventoryDropTarget>();
                target.Initialize(this, InventoryDropKind.SkillSlot, i);
            }
        }

        if (equippedWeaponImage != null)
        {
            InventoryDropTarget weaponTarget = equippedWeaponImage.GetComponent<InventoryDropTarget>();
            if (weaponTarget == null) weaponTarget = equippedWeaponImage.gameObject.AddComponent<InventoryDropTarget>();
            weaponTarget.Initialize(this, InventoryDropKind.Weapon, 0);
        }

        if (skillUnequipButton != null) skillUnequipButton.onClick.AddListener(OnSkillUnequipClicked);
        if (skillKeyInput != null)
        {
            skillKeyInput.characterLimit = 5;
            skillKeyInput.onEndEdit.AddListener(OnSkillKeyInputEnd);
        }
        if (skillSlotPopup != null) skillSlotPopup.SetActive(false);
    }

    private void RefreshEquippedSkillSlots(PlayerProgressionService progress)
    {
        if (equippedSkillSlots == null) return;
        for (int i = 0; i < equippedSkillSlots.Length; i++)
        {
            Button button = equippedSkillSlots[i];
            if (button == null) continue;

            EquippedSkillData equipped = progress.GetEquippedSkillAtSlot(i);
            Sprite icon = equipped != null ? progress.GetSkillIcon(equipped.skillName) : null;
            if (equippedSkillSlotIcons != null && i < equippedSkillSlotIcons.Length && equippedSkillSlotIcons[i] != null)
            {
                equippedSkillSlotIcons[i].sprite = icon;
                equippedSkillSlotIcons[i].enabled = icon != null;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null) continue;
            if (equipped == null) label.text = $"Ô {i + 1}\nTRỐNG";
            else if (icon != null) label.text = $"[{equipped.inputKey.ToUpperInvariant()}]"; // có ảnh thì chỉ hiện phím
            else label.text = $"{progress.GetSkillDisplayName(equipped.skillName)}\n[{equipped.inputKey.ToUpperInvariant()}]";
        }
    }

    private void OnEquippedSkillSlotClicked(int slotIndex)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        EquippedSkillData equipped = progress != null ? progress.GetEquippedSkillAtSlot(slotIndex) : null;
        keyMessage = null;

        if (equipped == null || selectedSkillSlot == slotIndex)
        {
            selectedSkillSlot = -1; // ô trống hoặc bấm lần 2 -> đóng popup
        }
        else
        {
            selectedSkillSlot = slotIndex;
            if (moveSkillPopupToSlot && skillSlotPopup != null
                && equippedSkillSlots != null && slotIndex < equippedSkillSlots.Length && equippedSkillSlots[slotIndex] != null)
            {
                skillSlotPopup.transform.position = equippedSkillSlots[slotIndex].transform.position + (Vector3)skillPopupOffset;
            }
        }
        Refresh();
    }

    private void RefreshSkillPopup(PlayerProgressionService progress)
    {
        EquippedSkillData equipped = selectedSkillSlot >= 0 ? progress.GetEquippedSkillAtSlot(selectedSkillSlot) : null;
        if (equipped == null) selectedSkillSlot = -1;
        bool open = equipped != null;

        if (skillSlotPopup != null && skillSlotPopup.activeSelf != open) skillSlotPopup.SetActive(open);
        if (skillUnequipButton != null && skillUnequipButton.gameObject.activeSelf != open)
            skillUnequipButton.gameObject.SetActive(open);
        if (skillKeyInput != null)
        {
            if (skillKeyInput.gameObject.activeSelf != open) skillKeyInput.gameObject.SetActive(open);
            // Update() gọi Refresh mỗi frame nên không ghi đè khi người chơi đang gõ
            if (open && !skillKeyInput.isFocused)
                skillKeyInput.SetTextWithoutNotify(PlayerProgressionService.ParseSkillInputKey(equipped.inputKey).ToUpperInvariant());
        }
    }

    private void OnSkillUnequipClicked()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        EquippedSkillData equipped = progress != null && selectedSkillSlot >= 0
            ? progress.GetEquippedSkillAtSlot(selectedSkillSlot) : null;
        if (equipped != null) progress.UnequipSkill(equipped.skillName);
        selectedSkillSlot = -1;
        Refresh();
    }

    private void OnSkillKeyInputEnd(string text)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        EquippedSkillData equipped = progress != null && selectedSkillSlot >= 0
            ? progress.GetEquippedSkillAtSlot(selectedSkillSlot) : null;
        if (equipped == null) return;

        if (!progress.TrySetEquippedSkillKey(equipped.skillName, text, out string message) && !string.IsNullOrEmpty(message))
        {
            keyMessage = message;
            keyMessageUntil = Time.unscaledTime + 3f;
        }
        else
        {
            keyMessage = null;
        }
        // Refresh đặt lại ô nhập về phím thực tế (tự trả về phím cũ nếu bị từ chối)
        skillKeyInput.DeactivateInputField();
        Refresh();
    }

    // ------------------------------------------------------------------ kéo thả
    public bool BeginDragFromGrid(int slotIndex)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return false;

        if (showingSkills)
        {
            RebuildVisibleSkills(progress);
            string skillId = GetSkillIdAt(slotIndex);
            if (skillId == null) return false;
            selectedSlot = slotIndex; // kéo là chọn luôn: panel + nút hiện ngay
            StartDrag(DragKind.Skill, slotIndex, skillId, -1, progress.GetSkillIcon(skillId), progress.GetSkillDisplayName(skillId));
            return true;
        }

        InventoryEntry entry = progress.Data.inventory.Find(item => item.slotIndex == slotIndex && item.amount > 0);
        ItemDefinition definition = entry != null ? ItemDefinition.Find(entry.itemName) : null;
        if (definition == null) return false;
        selectedSlot = slotIndex; // kéo là chọn luôn: panel + nút Trang bị / Dùng / Đọc hiện ngay
        StartDrag(DragKind.Item, slotIndex, null, -1, definition.icon, definition.itemName);
        return true;
    }

    /// <summary>Kéo vũ khí đang trang bị ra lưới đồ để bỏ trang bị.</summary>
    public bool BeginDragEquippedWeapon()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null || string.IsNullOrEmpty(progress.Data.equippedItemName)) return false;
        ItemDefinition weapon = ItemDefinition.Find(progress.Data.equippedItemName);
        if (weapon == null) return false;
        StartDrag(DragKind.Weapon, -1, null, -1, weapon.icon, weapon.itemName);
        return true;
    }

    /// <summary>Kéo skill đang trang bị sang ô kỹ năng khác (đổi chỗ).</summary>
    public bool BeginDragEquippedSkill(int skillSlot)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        EquippedSkillData equipped = progress != null ? progress.GetEquippedSkillAtSlot(skillSlot) : null;
        if (equipped == null) return false;
        StartDrag(DragKind.Skill, -1, equipped.skillName, skillSlot, progress.GetSkillIcon(equipped.skillName),
            progress.GetSkillDisplayName(equipped.skillName));
        return true;
    }

    private void StartDrag(DragKind kind, int itemSlot, string skillName, int fromSkillSlot, Sprite icon, string label)
    {
        dragKind = kind;
        dragItemSlot = itemSlot;
        dragSkillName = skillName;
        dragFromSkillSlot = fromSkillSlot;
        hoverSlot = -1;

        EnsureDragGhost();
        if (dragGhost == null) return;
        dragGhost.SetAsLastSibling();
        dragGhostImage.sprite = icon;
        dragGhostImage.color = icon != null ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.9f);
        dragGhostLabel.text = icon != null ? string.Empty : label;
        dragGhost.gameObject.SetActive(true);
    }

    public void UpdateDrag(PointerEventData eventData)
    {
        if (dragKind == DragKind.None || dragGhost == null) return;
        Canvas canvas = dragGhost.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect != null
            && RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 world))
            dragGhost.position = world;
    }

    public void EndDrag()
    {
        dragKind = DragKind.None;
        dragItemSlot = -1;
        dragSkillName = null;
        dragFromSkillSlot = -1;
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
    }

    private void EnsureDragGhost()
    {
        if (dragGhost != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject ghost = new GameObject("Drag Ghost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghost.transform.SetParent(canvas.rootCanvas.transform, false);
        dragGhost = ghost.GetComponent<RectTransform>();
        dragGhost.sizeDelta = dragGhostSize;
        ghost.GetComponent<CanvasGroup>().blocksRaycasts = false; // không chặn chuột để ô bên dưới nhận được thả
        dragGhostImage = ghost.GetComponent<Image>();
        dragGhostImage.raycastTarget = false;
        dragGhostImage.preserveAspect = true;
        dragGhostLabel = CreateText(ghost.transform, "Label", 11, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, dragGhostSize, Color.white);
        dragGhostLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        dragGhostLabel.verticalOverflow = VerticalWrapMode.Overflow;
        ghost.SetActive(false);
    }

    /// <summary>Thả vào 1 ô của lưới: vật phẩm đổi chỗ / gộp stack; skill đang trang bị thả vào lưới = gỡ.</summary>
    public void DropOnGridSlot(int toSlot)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null) return;

        if (dragKind == DragKind.Weapon)
        {
            progress.UnequipItem(); // kéo vũ khí ra ô đồ = bỏ trang bị
            Refresh();
        }
        else if (dragKind == DragKind.Item && !showingSkills)
        {
            MoveItem(dragItemSlot, toSlot); // ô có đồ khác -> đổi chỗ, cùng loại -> gộp, ô trống -> xuống cuối; luôn dồn lên đầu
        }
        else if (dragKind == DragKind.Skill && showingSkills)
        {
            if (dragFromSkillSlot >= 0)
            {
                progress.UnequipSkill(dragSkillName); // skill đang trang bị thả vào lưới = gỡ
            }
            else if (progress.MoveInventorySkill(dragItemSlot, toSlot))
            {
                RebuildVisibleSkills(progress);
                int count = visibleSkillIds.Count;
                selectedSlot = toSlot >= count ? count - 1 : toSlot; // đi theo skill vừa kéo
            }
            Refresh();
        }
    }

    /// <summary>Thả vào ô vũ khí hoặc 1 trong 3 ô kỹ năng.</summary>
    public void DropOnTarget(InventoryDropKind kind, int index)
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || progress.Data == null) return;

        if (kind == InventoryDropKind.Weapon)
        {
            if (dragKind != DragKind.Item) return;
            InventoryEntry entry = progress.Data.inventory.Find(item => item.slotIndex == dragItemSlot);
            ItemDefinition definition = entry != null ? ItemDefinition.Find(entry.itemName) : null;
            if (definition == null) return;

            if (!definition.IsEquippable) ShowMessage($"'{definition.itemName}' không thể trang bị.");
            else if (progress.Data.equippedItemName != entry.itemName) progress.ToggleEquipAtSlot(dragItemSlot);
        }
        else
        {
            if (dragKind != DragKind.Skill) return;
            if (!progress.EquipSkillAtSlot(dragSkillName, index, out string message) && !string.IsNullOrEmpty(message))
                ShowMessage(message);
        }
        Refresh();
    }

    private void ShowMessage(string message)
    {
        keyMessage = message;
        keyMessageUntil = Time.unscaledTime + 3f;
    }

    // ------------------------------------------------------------------ panel thông tin
    private void ResolvePanelTarget(PlayerProgressionService progress, out int slot, out bool pinned)
    {
        int hover = UseTouchInteraction ? -1 : hoverSlot;
        slot = hover >= 0 && HasContentAt(progress, hover) ? hover : selectedSlot;
        pinned = slot >= 0 && slot == selectedSlot;
    }

    private void RefreshInfoPanel(PlayerProgressionService progress)
    {
        ResolvePanelTarget(progress, out int slot, out bool pinned);

        if (infoPanel != null)
        {
            bool show = slot >= 0;
            if (infoPanel.activeSelf != show) infoPanel.SetActive(show);

            // Xem nhanh (rê chuột) thì không chặn chuột để khỏi làm ô bên dưới nhấp nháy; ghim mới bấm được nút
            CanvasGroup group = infoPanel.GetComponent<CanvasGroup>();
            if (group == null) group = infoPanel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = pinned;
            group.interactable = pinned;
        }

        if (showingSkills) FillSkillInfo(progress, slot, pinned);
        else FillItemInfo(progress, slot, pinned);
    }

    private void FillItemInfo(PlayerProgressionService progress, int slot, bool showButtons)
    {
        InventoryEntry entry = slot >= 0
            ? progress.Data.inventory.Find(item => item.slotIndex == slot)
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
            equipButton.gameObject.SetActive(showButtons && hasSelection && definition.IsEquippable);
            Text label = equipButton.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = entry != null && entry.itemName == progress.Data.equippedItemName
                    ? "BỎ TRANG BỊ"
                    : "TRANG BỊ";
        }
        if (useButton != null)
            useButton.gameObject.SetActive(showButtons && hasSelection && definition.healAmount > 0);
        if (readBookButton != null)
            readBookButton.gameObject.SetActive(showButtons && hasSelection && definition.isReadable);
    }

    private void FillSkillInfo(PlayerProgressionService progress, int slot, bool showButtons)
    {
        string skillId = GetSkillIdAt(slot);
        bool hasSelection = skillId != null;
        string displayName = hasSelection ? progress.GetSkillDisplayName(skillId) : string.Empty;
        SkillData definition = hasSelection ? progress.GetSkillDefinition(skillId) : null;
        int equippedSlot = hasSelection ? progress.GetEquippedSkillIndex(skillId) : -1;
        EquippedSkillData equipped = equippedSlot >= 0 ? progress.GetEquippedSkillAtSlot(equippedSlot) : null;

        if (selectedItemIcon != null)
        {
            Sprite skillIcon = hasSelection ? progress.GetSkillIcon(skillId) : null;
            selectedItemIcon.sprite = skillIcon;
            selectedItemIcon.enabled = skillIcon != null;
        }
        if (selectedItemName != null) selectedItemName.text = hasSelection ? displayName : "Chọn kỹ năng";
        if (selectedItemDescription != null)
        {
            if (!hasSelection)
                selectedItemDescription.text = "Chọn kỹ năng đã mở khóa rồi bấm TRANG BỊ. Bấm vào ô kỹ năng đã trang bị để gỡ hoặc đổi phím.";
            else
            {
                string description = definition != null && !string.IsNullOrWhiteSpace(definition.effect)
                    ? definition.effect : "Kỹ năng đã được mở khóa.";
                string requirement = progress.GetWeaponRequirementText(skillId);
                if (!string.IsNullOrEmpty(requirement))
                    description += "\nYêu cầu vũ khí: " + requirement;
                string blockReason = equipped == null ? progress.GetEquipBlockReason(skillId) : null;
                if (blockReason != null)
                    description += "\nKhông thể trang bị: " + blockReason;
                if (equipped == null && blockReason == null
                    && progress.GetEquippedSkillCount() >= PlayerSaveData.EquippedSkillCapacity)
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
        }
        if (equippedWeaponLabel != null)
            equippedWeaponLabel.text = $"KỸ NĂNG ĐÃ TRANG BỊ: {progress.GetEquippedSkillCount()}/{PlayerSaveData.EquippedSkillCapacity}";

        bool canEquip = hasSelection && progress.CanEquipSkill(skillId);
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(showButtons && hasSelection
                && (equipped != null || (canEquip && progress.GetEquippedSkillCount() < PlayerSaveData.EquippedSkillCapacity)));
            Text label = equipButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = equipped != null ? "GỠ KỸ NĂNG" : "TRANG BỊ";
        }
        if (useButton != null) useButton.gameObject.SetActive(false);
        if (readBookButton != null) readBookButton.gameObject.SetActive(false);
    }

    private void RefreshStatus(PlayerProgressionService progress)
    {
        if (inventoryStatus == null) return;
        inventoryStatus.text = showingSkills
            ? $"KỸ NĂNG: {visibleSkillIds.Count} ĐÃ MỞ | TỐI ĐA {PlayerSaveData.EquippedSkillCapacity} Ô | PHÍM 1-9 / A-Z / SPACE"
            : $"TÚI ĐỒ  {progress.Data.inventory.Count}/{SlotCount} ô  •  Kéo thả để sắp xếp";
        if (!string.IsNullOrEmpty(keyMessage) && Time.unscaledTime < keyMessageUntil)
            inventoryStatus.text += "\n" + keyMessage;
    }

    // ------------------------------------------------------------------ tab / hành động
    private void OnItemTabClicked()
    {
        SetTab(false);
    }

    private void OnSkillTabClicked()
    {
        // Có Item Tab Button: bấm Skill Tab luôn mở tab Kỹ năng. Chưa có: giữ kiểu cũ (nút bật/tắt).
        SetTab(itemTabButton != null ? true : !showingSkills);
    }

    private void SetTab(bool skills)
    {
        if (showingSkills == skills)
        {
            ApplyTabState();
            return;
        }

        showingSkills = skills;
        selectedSlot = -1;
        hoverSlot = -1;
        if (bookReadPanel != null) bookReadPanel.SetActive(false);
        ApplyTabState();
        Refresh();
    }

    /// <summary>Tô màu tab đang mở; nếu chưa có Item Tab Button thì đổi chữ nút cũ như trước.</summary>
    private void ApplyTabState()
    {
        if (itemTabButton != null)
        {
            SetTabColor(itemTabButton, !showingSkills);
            SetTabColor(skillTabButton, showingSkills);
            return;
        }

        if (skillTabButton == null) return;
        Text label = skillTabButton.GetComponentInChildren<Text>(true);
        if (label != null) label.text = showingSkills ? "VẬT PHẨM" : "KỸ NĂNG";
    }

    private void SetTabColor(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = selected ? tabSelectedColor : tabUnselectedColor;
    }

    private void ToggleSelectedSkillEquipment()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        string skillId = GetSkillIdAt(selectedSlot);
        if (progress == null || string.IsNullOrWhiteSpace(skillId)) return;
        if (progress.GetEquippedSkillIndex(skillId) >= 0) progress.UnequipSkill(skillId);
        else progress.EquipSkill(skillId);
        Refresh();
    }

    private static string FormatStats(ItemDefinition definition)
    {
        List<string> stats = new List<string>();
        if (definition.attackBonus > 0) stats.Add($"+{definition.attackBonus} Tấn công");
        if (definition.defenseBonus > 0) stats.Add($"+{definition.defenseBonus} Phòng thủ");
        if (definition.maxHealthBonus > 0) stats.Add($"+{definition.maxHealthBonus} HP tối đa");
        if (definition.healAmount > 0) stats.Add($"Hồi {definition.healAmount} HP");
        if (definition.category == ItemCategory.Weapon && definition.weaponType != WeaponType.None)
            stats.Add($"Loại: {WeaponTypeInfo.GetLabel(definition.weaponType)}");
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
        if (showingSkills || selectedSlot < 0) return;
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
        ApplyTabState();
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
