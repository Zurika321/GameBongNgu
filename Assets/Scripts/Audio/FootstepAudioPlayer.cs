using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class FootstepAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Min(0.05f)] private float interval = 0.32f;
    private AudioSource source;
    private Rigidbody2D body;
    private float timer;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        body = GetComponent<Rigidbody2D>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        if (footstepClip == null) footstepClip = Resources.Load<AudioClip>("Volume/SFX/tieng_di_bo");
        VolumeManager.RegisterSFXSource(source);
    }
    private void Update()
    {
        if (GameplayPause.IsPaused || footstepClip == null || body == null || body.linearVelocity.sqrMagnitude < 0.05f)
        {
            timer = 0f;
            return;
        }
        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;
        source.PlayOneShot(footstepClip);
    }
    private void OnDestroy() => VolumeManager.UnregisterSFXSource(source);
}
