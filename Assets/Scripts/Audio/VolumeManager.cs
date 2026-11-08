using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VolumeManager : MonoBehaviour
{
    private const string MasterKey = "Audio.MasterVolume";
    private const string MusicKey = "Audio.MusicVolume";
    private const string SfxKey = "Audio.SFXVolume";
    private const string MasterMuteKey = "Audio.MasterMuted";
    private const string MusicMuteKey = "Audio.MusicMuted";
    private const string SfxMuteKey = "Audio.SFXMuted";

    private static VolumeManager instance;
    private static readonly HashSet<AudioSource> legacyMusicSources = new HashSet<AudioSource>();
    private static readonly HashSet<AudioSource> legacySfxSources = new HashSet<AudioSource>();

    [Header("Audio database")]
    [SerializeField] private AudioConfig audioConfig;

    [Header("Optional startup music")]
    [SerializeField] private bool playStartupMusic = false;
    [SerializeField, RegistryId(RegistryIdType.Music)] private string startupMusicId = "menu";

    [Header("Default volume")]
    [Range(0f, 1f)] [SerializeField] private float defaultMaster = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultMusic = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float defaultSfx = 1f;

    private float masterVolume;
    private float musicVolume;
    private float sfxVolume;
    private bool masterMuted;
    private bool musicMuted;
    private bool sfxMuted;

    private AudioSource musicSource;
    private readonly Dictionary<string, AudioSource> activeSfxLoops = new Dictionary<string, AudioSource>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> sfxLoopEndTimes = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<string> expiredLoopIds = new List<string>();
    private readonly HashSet<string> missingMusicWarnings = new HashSet<string>();
    private readonly HashSet<string> missingSfxWarnings = new HashSet<string>();

    private string currentMusicId;
    private float musicStopAt;

    public static event Action OnAudioSettingsChanged;

    public static float MasterVolume => instance != null ? instance.masterVolume : 1f;
    public static float MusicVolume => instance != null ? instance.musicVolume : 0.8f;
    public static float SFXVolume => instance != null ? instance.sfxVolume : 1f;
    public static bool MasterMuted => instance != null && instance.masterMuted;
    public static bool MusicMuted => instance != null && instance.musicMuted;
    public static bool SFXMuted => instance != null && instance.sfxMuted;
    public static bool IsReady => instance != null;
    public static string CurrentMusicId => instance != null ? instance.currentMusicId : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        if (FindObjectOfType<VolumeManager>() == null)
            new GameObject("Persistent Audio Manager").AddComponent<VolumeManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        CreateMusicSource();

        if (audioConfig == null)
            audioConfig = AudioConfigBootstrap.EnsureLoaded();

        if (audioConfig != null)
            audioConfig.EnsureDefaults();

        Apply();
    }

    private void Start()
    {

        if (playStartupMusic && !string.IsNullOrWhiteSpace(startupMusicId))
            ToggleMusicInternal(startupMusicId, true);
    }

    private void Update()
    {
        UpdateMusicLifetime();
        UpdateSfxLoopLifetime();
    }

    private void LoadSettings()
    {
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, defaultMaster));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, defaultMusic));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, defaultSfx));
        masterMuted = PlayerPrefs.GetInt(MasterMuteKey, 0) == 1;
        musicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(SfxMuteKey, 0) == 1;
    }

    private void CreateMusicSource()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        legacyMusicSources.Clear();
        legacySfxSources.Clear();
        OnAudioSettingsChanged = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit() => PlayerPrefs.Save();

    public static void ToggleMusic(string musicId, bool onOrOff)
    {
        if (instance == null)
        {
            Debug.LogWarning("[VolumeManager] Không tìm thấy VolumeManager trong scene khởi động.");
            return;
        }

        instance.ToggleMusicInternal(musicId, onOrOff);
    }

    public static void PlaySFX(string sfxId)
    {
        if (instance == null)
        {
            Debug.LogWarning("[VolumeManager] Không tìm thấy VolumeManager trong scene khởi động.");
            return;
        }

        instance.PlaySfxInternal(sfxId);
    }

    public static void PlaySFXLoop(string sfxId, float durationSeconds = 0f)
    {
        if (instance == null)
        {
            Debug.LogWarning("[VolumeManager] Không tìm thấy VolumeManager trong scene khởi động.");
            return;
        }

        instance.PlaySfxLoopInternal(sfxId, durationSeconds);
    }

    public static void StopSFXLoop(string sfxId)
    {
        if (instance == null) return;
        instance.StopSfxLoopInternal(sfxId);
    }

    public static void RegisterMusicSource(AudioSource source) => RegisterLegacySource(source, legacyMusicSources, true);
    public static void UnregisterMusicSource(AudioSource source) => legacyMusicSources.Remove(source);
    public static void RegisterSFXSource(AudioSource source) => RegisterLegacySource(source, legacySfxSources, false);
    public static void UnregisterSFXSource(AudioSource source) => legacySfxSources.Remove(source);

    public static void SetMasterVolume(float value) => SetLevel(value, 0);
    public static void SetMusicVolume(float value) => SetLevel(value, 1);
    public static void SetSFXVolume(float value) => SetLevel(value, 2);
    public static void SetMasterMuted(bool value) => SetMute(value, 0);
    public static void SetMusicMuted(bool value) => SetMute(value, 1);
    public static void SetSFXMuted(bool value) => SetMute(value, 2);

    private static void RegisterLegacySource(AudioSource source, HashSet<AudioSource> sources, bool isMusic)
    {
        if (source == null) return;
        sources.Add(source);
        if (instance != null)
            source.volume = isMusic ? instance.GetMusicChannelVolume() : instance.GetSfxChannelVolume();
    }

    private static void SetLevel(float value, int channel)
    {
        if (instance == null) return;
        value = Mathf.Clamp01(value);

        switch (channel)
        {
            case 0:
                instance.masterVolume = value;
                PlayerPrefs.SetFloat(MasterKey, value);
                break;
            case 1:
                instance.musicVolume = value;
                PlayerPrefs.SetFloat(MusicKey, value);
                break;
            default:
                instance.sfxVolume = value;
                PlayerPrefs.SetFloat(SfxKey, value);
                break;
        }

        instance.Apply();
        OnAudioSettingsChanged?.Invoke();
    }

    private static void SetMute(bool value, int channel)
    {
        if (instance == null) return;

        switch (channel)
        {
            case 0:
                instance.masterMuted = value;
                PlayerPrefs.SetInt(MasterMuteKey, value ? 1 : 0);
                break;
            case 1:
                instance.musicMuted = value;
                PlayerPrefs.SetInt(MusicMuteKey, value ? 1 : 0);
                break;
            default:
                instance.sfxMuted = value;
                PlayerPrefs.SetInt(SfxMuteKey, value ? 1 : 0);
                break;
        }

        instance.Apply();
        OnAudioSettingsChanged?.Invoke();
    }

    private void ToggleMusicInternal(string musicId, bool onOrOff)
    {
        if (string.IsNullOrWhiteSpace(musicId)) return;

        if (!onOrOff)
        {
            if (string.Equals(currentMusicId, musicId, StringComparison.Ordinal))
                StopCurrentMusic();
            return;
        }

        if (audioConfig == null || !audioConfig.TryGetMusic(musicId, out AudioConfig.MusicEntry entry))
        {
            WarnMissingId(musicId, true);
            return;
        }

        if (entry.clip == null)
        {
            Debug.LogWarning($"[VolumeManager] Music '{musicId}' chưa có AudioClip.");
            return;
        }

        if (currentMusicId == musicId && musicSource != null && musicSource.isPlaying)
            return;

        StopCurrentMusic();
        currentMusicId = musicId;
        musicSource.clip = entry.clip;
        musicSource.loop = entry.loop;
        musicSource.pitch = Mathf.Max(0.1f, entry.pitch);
        musicSource.volume = GetMusicChannelVolume() * Mathf.Clamp01(entry.volume);
        musicSource.Play();

        musicStopAt = entry.stopAfterSeconds > 0f ? Time.unscaledTime + entry.stopAfterSeconds : 0f;
    }

    private void StopCurrentMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        currentMusicId = null;
        musicStopAt = 0f;
    }

    private void UpdateMusicLifetime()
    {
        if (string.IsNullOrEmpty(currentMusicId) || musicSource == null) return;

        if (musicStopAt > 0f && Time.unscaledTime >= musicStopAt)
        {
            StopCurrentMusic();
            return;
        }

        if (!musicSource.isPlaying)
        {
            currentMusicId = null;
            musicStopAt = 0f;
        }
    }

    private void PlaySfxInternal(string sfxId)
    {
        if (!TryGetSfx(sfxId, out AudioConfig.SfxEntry entry) || entry.clip == null) return;

        GameObject go = new GameObject("OneShot SFX");
        go.transform.SetParent(transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = entry.clip;
        source.volume = GetSfxChannelVolume() * Mathf.Clamp01(entry.volume);
        source.pitch = Mathf.Max(0.1f, entry.pitch);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.Play();
        RegisterSFXSource(source);
        Destroy(go, entry.clip.length / Mathf.Max(0.1f, source.pitch));
    }

    private void PlaySfxLoopInternal(string sfxId, float durationSeconds)
    {
        if (!TryGetSfx(sfxId, out AudioConfig.SfxEntry entry) || entry.clip == null) return;

        if (!activeSfxLoops.TryGetValue(sfxId, out AudioSource source) || source == null)
        {
            GameObject go = new GameObject($"Loop SFX - {sfxId}");
            go.transform.SetParent(transform);
            source = go.AddComponent<AudioSource>();
            activeSfxLoops[sfxId] = source;
        }
        else
        {
            source.Stop();
        }

        source.clip = entry.clip;
        source.volume = GetSfxChannelVolume() * Mathf.Clamp01(entry.volume);
        source.pitch = Mathf.Max(0.1f, entry.pitch);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.Play();

        if (durationSeconds > 0f)
            sfxLoopEndTimes[sfxId] = Time.unscaledTime + durationSeconds;
        else
            sfxLoopEndTimes.Remove(sfxId);
    }

    private void StopSfxLoopInternal(string sfxId)
    {
        if (string.IsNullOrWhiteSpace(sfxId)) return;

        if (activeSfxLoops.TryGetValue(sfxId, out AudioSource source))
        {
            if (source != null)
            {
                source.Stop();
                if (source.gameObject != null)
                    Destroy(source.gameObject);
            }

            activeSfxLoops.Remove(sfxId);
        }

        sfxLoopEndTimes.Remove(sfxId);
    }

    private void UpdateSfxLoopLifetime()
    {
        if (activeSfxLoops.Count == 0) return;

        expiredLoopIds.Clear();
        foreach (KeyValuePair<string, AudioSource> pair in activeSfxLoops)
        {
            string id = pair.Key;
            AudioSource source = pair.Value;

            bool timedOut = sfxLoopEndTimes.TryGetValue(id, out float endTime) && Time.unscaledTime >= endTime;
            bool sourceEnded = source == null || !source.isPlaying;
            if (timedOut || sourceEnded)
                expiredLoopIds.Add(id);
        }

        for (int i = 0; i < expiredLoopIds.Count; i++)
            StopSfxLoopInternal(expiredLoopIds[i]);
    }

    private bool TryGetSfx(string id, out AudioConfig.SfxEntry entry)
    {
        if (audioConfig != null && audioConfig.TryGetSfx(id, out entry))
            return true;

        WarnMissingId(id, false);
        entry = null;
        return false;
    }

    private bool TryGetMusic(string id, out AudioConfig.MusicEntry entry)
    {
        if (audioConfig != null && audioConfig.TryGetMusic(id, out entry))
            return true;

        WarnMissingId(id, true);
        entry = null;
        return false;
    }

    private void WarnMissingId(string id, bool isMusic)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        HashSet<string> warnings = isMusic ? missingMusicWarnings : missingSfxWarnings;
        if (!warnings.Add(id)) return;

        Debug.LogWarning($"[VolumeManager] Không tìm thấy {(isMusic ? "Music" : "SFX")} ID '{id}' trong AudioConfig.");
    }

    public static AudioClip GetMusicClip(string musicId)
    {
        if (instance == null || instance.audioConfig == null)
            return null;
        return instance.audioConfig.GetMusicClip(musicId);
    }

    public static AudioClip GetSfxClip(string sfxId)
    {
        if (instance == null || instance.audioConfig == null)
            return null;
        return instance.audioConfig.GetSfxClip(sfxId);
    }

    private void Apply()
    {
        AudioListener.volume = masterMuted ? 0f : masterVolume;
        ApplyChannel(legacyMusicSources, musicMuted ? 0f : musicVolume);
        ApplyChannel(legacySfxSources, sfxMuted ? 0f : sfxVolume);

        if (musicSource != null)
            musicSource.volume = musicMuted ? 0f : musicVolume;
    }

    private static void ApplyChannel(HashSet<AudioSource> list, float volume)
    {
        if (list == null) return;
        list.RemoveWhere(s => s == null);
        foreach (AudioSource source in list)
            source.volume = volume;
    }

    private float GetMusicChannelVolume() => musicMuted ? 0f : musicVolume;
    private float GetSfxChannelVolume() => sfxMuted ? 0f : sfxVolume;
}
