using System.Collections.Generic;
using UnityEngine;

public class DoorTransition : MonoBehaviour
{
    private enum Direction { Up, Down, Left, Right }

    [SerializeField] private PolygonCollider2D newBounds;
    [SerializeField] private Transform destinationPosition;
    [RegistryId(RegistryIdType.Flag), SerializeField] private string requiredFlag;
    [SerializeField] private Direction direction;
    [SerializeField] private float pushDistance = 2f;

    private readonly HashSet<Collider2D> waitingForUnlock = new HashSet<Collider2D>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("HitBoxPlayer")) return;

        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (!IsUnlocked(progress))
        {
            if (!string.IsNullOrWhiteSpace(requiredFlag))
                waitingForUnlock.Add(other);
            return;
        }

        TryTransition(progress);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!waitingForUnlock.Contains(other)) return;

        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (!IsUnlocked(progress)) return;

        waitingForUnlock.Remove(other);
        TryTransition(progress);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        waitingForUnlock.Remove(other);
    }

    private bool IsUnlocked(PlayerProgressionService progress)
    {
        return string.IsNullOrWhiteSpace(requiredFlag)
            || (progress != null && progress.Data.HasFlag(requiredFlag));
    }

    private void TryTransition(PlayerProgressionService progress)
    {
        if (destinationPosition == null)
        {
            Debug.LogError("[DoorTransition] Destination Position is not assigned.", this);
            return;
        }
        if (RoomManager.Instance == null)
        {
            Debug.LogError("[DoorTransition] RoomManager.Instance is missing.", this);
            return;
        }

        RoomManager.Instance.TransitionRoom(newBounds, destinationPosition.position, GetPushOffset());
    }

    private Vector2 GetPushOffset()
    {
        switch (direction)
        {
            case Direction.Up: return Vector2.up * pushDistance;
            case Direction.Down: return Vector2.down * pushDistance;
            case Direction.Left: return Vector2.left * pushDistance;
            case Direction.Right: return Vector2.right * pushDistance;
            default: return Vector2.zero;
        }
    }
}