using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler,
    IEndDragHandler, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text countText;
    [SerializeField] private Text equippedText;

    private InventoryGridUI inventory;
    private int slotIndex;
    private static InventorySlotView draggedSlot;

    public int SlotIndex => slotIndex;

    public void Initialize(InventoryGridUI owner, int index, Image icon, Text count, Text equipped)
    {
        inventory = owner;
        slotIndex = index;
        iconImage = icon;
        countText = count;
        equippedText = equipped;
    }

    public void SetItem(ItemDefinition definition, int count, bool isEquipped, bool isSelected)
    {
        bool hasItem = definition != null && count > 0;
        iconImage.enabled = hasItem;
        iconImage.sprite = hasItem ? definition.icon : null;
        countText.text = hasItem && count > 1 ? count.ToString() : string.Empty;
        equippedText.text = hasItem && isEquipped ? "EQUIPPED" : string.Empty;

        Image background = GetComponent<Image>();
        if (background != null)
            background.color = isSelected
                ? new Color(0.7f, 0.48f, 0.2f, 1f)
                : hasItem ? new Color(0.24f, 0.22f, 0.2f, 1f) : new Color(0.1f, 0.11f, 0.13f, 1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventory != null && eventData.button == PointerEventData.InputButton.Left)
            inventory.SelectSlot(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventory == null || !inventory.HasItemAt(slotIndex)) return;
        draggedSlot = this;
        if (iconImage != null) iconImage.raycastTarget = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (iconImage != null) iconImage.raycastTarget = false;
        if (draggedSlot == this) draggedSlot = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedSlot == null || inventory == null || draggedSlot.inventory != inventory) return;
        inventory.MoveItem(draggedSlot.slotIndex, slotIndex);
        draggedSlot = null;
    }
}
