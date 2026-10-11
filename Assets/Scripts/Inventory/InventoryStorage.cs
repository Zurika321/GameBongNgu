using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class InventoryStorage : MonoBehaviour
{
    [SerializeField] private string containerId = "Nhà Hoang - Rương";
    [SerializeField] private string displayName = "Rương gỗ";
    [SerializeField, Min(0.1f)] private float interactionRange = 2f;

    public string ContainerId => containerId;
    public string DisplayName => displayName;
    public float InteractionRange => interactionRange;
}
