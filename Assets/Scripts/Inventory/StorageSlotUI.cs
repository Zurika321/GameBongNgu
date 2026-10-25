using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Một ô đồ. Được clone từ "SlotTemplate" (đã tắt enable) trong StorageWindowUI.
/// Bạn có thể sửa thoải mái giao diện của SlotTemplate (gán sprite khung cho Image gốc,
/// đổi viền chọn, đổi font...) - mọi ô clone ra sẽ giống như mẫu.
/// </summary>
[RequireComponent(typeof(Image))]
public class StorageSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [Header("Thành phần của ô (builder tự gán)")]
    [SerializeField] private Image frameImage;          // Image gốc = khung ô (gán sprite khung ở đây)
    [SerializeField] private Image iconImage;           // Ảnh vật phẩm
    [SerializeField] private TMP_Text nameText;         // Tên vật phẩm
    [SerializeField] private TMP_Text amountText;       // Số lượng
    [SerializeField] private GameObject equippedBadge;  // Nhãn "đang trang bị"
    [SerializeField] private GameObject selectedBorder; // Viền khi ô được chọn
    [SerializeField] private Button actionButton;       // Nút Lấy / Bỏ ra
    [SerializeField] private TMP_Text actionLabel;

    [Header("Nhãn nút")]
    [SerializeField] private string takeLabel = "Lấy";      // ô bên rương
    [SerializeField] private string putLabel = "Bỏ ra";     // ô bên túi đồ người chơi

    private StorageWindowUI window;
    private CanvasGroup group;
    private bool dragging;

    public StorageWindowUI.Side Side { get; private set; }
    public int Index { get; private set; }
    public InventoryEntry Entry { get; private set; }
    public bool HasItem => Entry != null;

    public void Init(StorageWindowUI owner, StorageWindowUI.Side side, int index)
    {
        window = owner;
        Side = side;
        Index = index;

        if (frameImage == null) frameImage = GetComponent<Image>();
        frameImage.raycastTarget = true; // khung phải nhận raycast để kéo thả / click

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        // Mọi thứ con (trừ nút) không được chặn raycast, để khung nhận được sự kiện
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject == gameObject) continue;
            if (actionButton != null && g.transform.IsChildOf(actionButton.transform)) continue;
            g.raycastTarget = false;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => window.TransferFromSlot(this));
        }

        if (actionLabel != null) actionLabel.text = side == StorageWindowUI.Side.Chest ? takeLabel : putLabel;
        SetSelected(false);
        SetEntry(null, null, null, false);
    }

    /// <summary>Cập nhật hiển thị. icon = ảnh đã chọn sẵn (kể cả ảnh dự phòng).</summary>
    public void SetEntry(InventoryEntry entry, ItemDefinition definition, Sprite icon, bool equipped)
    {
        Entry = entry;
        if (group != null) group.alpha = 1f;

        bool has = entry != null;

        if (iconImage != null)
        {
            iconImage.sprite = has ? icon : null;
            iconImage.enabled = has && icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null) nameText.text = has ? entry.itemName : string.Empty;
        if (amountText != null) amountText.text = has && entry.amount > 1 ? "x" + entry.amount : string.Empty;
        if (equippedBadge != null) equippedBadge.SetActive(has && equipped);
        if (actionButton != null) actionButton.gameObject.SetActive(has);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBorder != null) selectedBorder.SetActive(selected);
    }

    // ------------------------------------------------------------
    // Sự kiện chuột
    // ------------------------------------------------------------
    public void OnPointerClick(PointerEventData eventData)
    {
        window.SelectSlot(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasItem)
        {
            eventData.pointerDrag = null;
            return;
        }

        dragging = true;
        if (group != null) group.alpha = 0.45f;
        window.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging) window.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        if (group != null) group.alpha = 1f;
        window.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;
        StorageSlotUI source = eventData.pointerDrag.GetComponent<StorageSlotUI>();
        window.HandleDrop(source, this);
    }
}
