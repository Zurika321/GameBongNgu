using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StoryEventTrigger : MonoBehaviour
{
    [SerializeField] private StoryEventDirector director;
    [SerializeField] private string eventId;
    [RegistryId(RegistryIdType.Flag), SerializeField] private string requiredFlag;

    private bool triggered;

    private void Reset()
    {
        Collider2D eventCollider = GetComponent<Collider2D>();
        eventCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        TryTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        TryTrigger(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (director == null || !director.IsEventCompleted(eventId)) triggered = false;
    }

    public void Configure(StoryEventDirector eventDirector, string storyEventId)
    {
        director = eventDirector;
        eventId = storyEventId;
    }

    private void TryTrigger(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress != null && progress.IsContinueRestorePending) return;
        if (!string.IsNullOrWhiteSpace(requiredFlag)
            && (progress == null || progress.Data == null || !progress.Data.HasFlag(requiredFlag))) return;

        if (director == null)
        {
            director = FindAnyObjectByType<StoryEventDirector>();
            if (director == null)
            {
                Debug.LogError($"[StoryEventTrigger] No StoryEventDirector is available for '{eventId}'.", this);
                return;
            }
        }

        if (!director.TriggerEvent(eventId)) return;

        triggered = true;
    }
}

