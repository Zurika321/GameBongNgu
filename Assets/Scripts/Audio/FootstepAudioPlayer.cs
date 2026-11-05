using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class FootstepAudioPlayer : MonoBehaviour
{
    [SerializeField, RegistryId(RegistryIdType.Sfx)] private string footstepSfxId = "footstep";
    [SerializeField, Min(0.05f)] private float interval = 0.32f;

    private AudioSource source;
    private Rigidbody2D body;
    private float timer;

    public void SetSfxId(string sfxId)
    {
        if (!string.IsNullOrWhiteSpace(sfxId))
            footstepSfxId = sfxId;
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        body = GetComponent<Rigidbody2D>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        VolumeManager.RegisterSFXSource(source);
    }

    private void Update()
    {
        if (GameplayPause.IsPaused || body == null || body.linearVelocity.sqrMagnitude < 0.05f)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < interval) return;

        timer = 0f;
        AudioClip clip = VolumeManager.GetSfxClip(footstepSfxId);
        if (clip != null)
            source.PlayOneShot(clip, VolumeManager.SFXVolume);
        else
            VolumeManager.PlaySFX(footstepSfxId);
    }

    private void OnDestroy() => VolumeManager.UnregisterSFXSource(source);
}
