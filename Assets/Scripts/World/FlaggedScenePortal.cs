using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlaggedScenePortal : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    [SerializeField] private string destinationZone;
    [RegistryId(RegistryIdType.Flag), SerializeField] private string requiredFlag;
    private bool transitionStarted;

    private void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (transitionStarted || !other.CompareTag("HitBoxPlayer")) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || string.IsNullOrWhiteSpace(destinationScene)) return;
        if (!string.IsNullOrWhiteSpace(requiredFlag) && !progress.Data.HasFlag(requiredFlag)) return;
        transitionStarted = true;
        progress.LoadSceneByZone(destinationScene, destinationZone);
    }
}
