using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 1 ô trong lưới inventory. Bấm chọn do Button của InventoryGridUI xử lý;
/// ở đây xử lý rê chuột (xem nhanh) và kéo thả (vật phẩm / kỹ năng).
/// </summary>
public class InventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text countText;
    [SerializeField] private Text equippedText;
    private InventoryGridUI inventory;
    private int slotIndex;
    private bool dragging;
    private RectTransform countRect;
    private Vector2 itemCountAnchorMin, itemCountAnchorMax, itemCountPosition, itemCountSize;
    private int itemCountFontSize;
    private TextAnchor itemCountAlignment;
    private HorizontalWrapMode itemCountHorizontalOverflow;
    private VerticalWrapMode itemCountVerticalOverflow;
    private bool countStyleCaptured;

    public int SlotIndex => slotIndex;

    public void Initialize(InventoryGridUI owner, int index, Image icon, Text count, Text equipped)
    {
        inventory = owner; slotIndex = index; iconImage = icon; countText = count; equippedText = equipped;
        countRect = countText != null ? countText.rectTransform : null;
        CaptureCountStyle();
    }

    private void CaptureCountStyle()
    {
        if (countStyleCaptured || countRect == null || countText == null) return;
        itemCountAnchorMin = countRect.anchorMin; itemCountAnchorMax = countRect.anchorMax;
        itemCountPosition = countRect.anchoredPosition; itemCountSize = countRect.sizeDelta;
        itemCountFontSize = countText.fontSize; itemCountAlignment = countText.alignment;
        itemCountHorizontalOverflow = countText.horizontalOverflow;
        itemCountVerticalOverflow = countText.verticalOverflow; countStyleCaptured = true;
    }

    private void RestoreCountStyle()
    {
        if (!countStyleCaptured || countRect == null || countText == null) return;
        countRect.anchorMin = itemCountAnchorMin; countRect.anchorMax = itemCountAnchorMax;
        countRect.anchoredPosition = itemCountPosition; countRect.sizeDelta = itemCountSize;
        countText.fontSize = itemCountFontSize; countText.alignment = itemCountAlignment;
        countText.horizontalOverflow = itemCountHorizontalOverflow;
        countText.verticalOverflow = itemCountVerticalOverflow;
    }

    public void SetItem(ItemDefinition definition, int count, bool isEquipped, bool isSelected)
    {
        RestoreCountStyle();
        bool hasItem = definition != null && count > 0;
        if (iconImage != null) { iconImage.enabled = hasItem; iconImage.sprite = hasItem ? definition.icon : null; }
        if (countText != null) countText.text = hasItem && count > 1 ? count.ToString() : string.Empty;
        if (equippedText != null) equippedText.text = hasItem && isEquipped ? "EQUIPPED" : string.Empty;
        Image background = GetComponent<Image>();
        if (background != null) background.color = inventory != null ? inventory.GetSlotColor(hasItem, isSelected) : Color.white;
    }

    /// <summary>Có ảnh (icon) thì hiện ảnh; chưa có ảnh thì hiện tên skill như cũ.</summary>
    public void SetSkill(string label, string badge, bool hasSkill, bool isSelected, Sprite icon = null)
    {
        CaptureCountStyle();
        bool showIcon = hasSkill && icon != null;
        if (iconImage != null) { iconImage.enabled = showIcon; iconImage.sprite = showIcon ? icon : null; }
        if (countText != null && countRect != null)
        {
            countRect.anchorMin = Vector2.zero; countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(4f, 20f); countRect.offsetMax = new Vector2(-4f, -4f);
            countText.text = showIcon ? string.Empty : (label ?? string.Empty);
            countText.fontSize = 11;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.horizontalOverflow = HorizontalWrapMode.Wrap;
            countText.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (equippedText != null) equippedText.text = badge ?? string.Empty;
        Image background = GetComponent<Image>();
        if (background != null) background.color = inventory != null ? inventory.GetSlotColor(hasSkill, isSelected) : Color.white;
    }

    // Rê chuột để hiện panel xem nhanh trên PC
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventory != null) inventory.SetHoverSlot(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventory != null) inventory.ClearHoverSlot(slotIndex);
    }

    // Kéo thả: phải có IDragHandler thì Unity mới phát sự kiện kéo
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = inventory != null && inventory.BeginDragFromGrid(slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging && inventory != null) inventory.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging && inventory != null) inventory.EndDrag();
        dragging = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (inventory != null) inventory.DropOnGridSlot(slotIndex);
    }
}