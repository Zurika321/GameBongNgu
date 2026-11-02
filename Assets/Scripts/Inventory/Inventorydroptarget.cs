using UnityEngine;
using UnityEngine.EventSystems;

public enum InventoryDropKind { Weapon, SkillSlot }

/// <summary>
/// Gắn (tự động bởi InventoryGridUI) vào ô vũ khí và 3 ô kỹ năng:
/// - nhận vật phẩm / kỹ năng được kéo thả vào;
/// - ô kỹ năng đang có skill kéo được sang ô khác để đổi chỗ;
/// - ô vũ khí đang có vũ khí kéo ra lưới đồ = bỏ trang bị.
/// </summary>
public class InventoryDropTarget : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private InventoryGridUI owner;
    private InventoryDropKind kind;
    private int index;
    private bool dragging;

    public void Initialize(InventoryGridUI inventory, InventoryDropKind dropKind, int targetIndex)
    {
        owner = inventory;
        kind = dropKind;
        index = targetIndex;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (owner != null) owner.DropOnTarget(kind, index);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null) { dragging = false; return; }
        dragging = kind == InventoryDropKind.SkillSlot
            ? owner.BeginDragEquippedSkill(index)
            : owner.BeginDragEquippedWeapon();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging) owner.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging) owner.EndDrag();
        dragging = false;
    }
}