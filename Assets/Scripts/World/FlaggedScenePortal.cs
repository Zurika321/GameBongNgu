using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class FlaggedScenePortal : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    [SerializeField] private string destinationZone;
    [SerializeField] private string requiredFlag;
    [SerializeField] private Vector2 arrivalPosition;
    private bool transitionStarted;

    private void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (transitionStarted || !other.CompareTag("HitBoxPlayer")) return;
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || string.IsNullOrWhiteSpace(destinationScene)) return;
        if (!string.IsNullOrWhiteSpace(requiredFlag) && !progress.Data.HasFlag(requiredFlag)) return;
        transitionStarted = true;
        progress.LoadScene(destinationScene, destinationZone, arrivalPosition);
    }
}
