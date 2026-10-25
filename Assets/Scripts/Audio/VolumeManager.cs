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
    private static readonly HashSet<AudioSource> musicSources = new HashSet<AudioSource>();
    private static readonly HashSet<AudioSource> sfxSources = new HashSet<AudioSource>();
    [Range(0f, 1f)] [SerializeField] private float defaultMaster = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultMusic = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float defaultSfx = 1f;
    private float masterVolume, musicVolume, sfxVolume;
    private bool masterMuted, musicMuted, sfxMuted;
    private AudioSource musicSource;
    public static event Action OnAudioSettingsChanged;
    public static float MasterVolume => instance != null ? instance.masterVolume : 1f;
    public static float MusicVolume => instance != null ? instance.musicVolume : 0.8f;
    public static float SFXVolume => instance != null ? instance.sfxVolume : 1f;
    public static bool MasterMuted => instance != null && instance.masterMuted;
    public static bool MusicMuted => instance != null && instance.musicMuted;
    public static bool SFXMuted => instance != null && instance.sfxMuted;
    public static bool IsReady => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null) new GameObject("Persistent Audio Manager").AddComponent<VolumeManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        masterVolume = PlayerPrefs.GetFloat(MasterKey, defaultMaster);
        musicVolume = PlayerPrefs.GetFloat(MusicKey, defaultMusic);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, defaultSfx);
        masterMuted = PlayerPrefs.GetInt(MasterMuteKey, 0) == 1;
        musicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(SfxMuteKey, 0) == 1;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = Resources.Load<AudioClip>("Volume/Music/nhac_bong_ngu");
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        RegisterMusicSource(musicSource);
        if (musicSource.clip != null) musicSource.Play();
        else Debug.LogWarning("[VolumeManager] Không tìm thấy Resources/Volume/Music/nhac_bong_ngu.wav.");
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            if (player.GetComponent<FootstepAudioPlayer>() == null)
                player.AddComponent<FootstepAudioPlayer>();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
        musicSources.Clear();
        sfxSources.Clear();
        OnAudioSettingsChanged = null;
    }

    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    private void OnApplicationQuit() => PlayerPrefs.Save();

    public static void SetMasterVolume(float v) => SetLevel(v, 0);
    public static void SetMusicVolume(float v) => SetLevel(v, 1);
    public static void SetSFXVolume(float v) => SetLevel(v, 2);
    public static void SetMasterMuted(bool v) => SetMute(v, 0);
    public static void SetMusicMuted(bool v) => SetMute(v, 1);
    public static void SetSFXMuted(bool v) => SetMute(v, 2);
    public static void RegisterMusicSource(AudioSource s) => Register(s, musicSources);
    public static void UnregisterMusicSource(AudioSource s) => musicSources.Remove(s);
    public static void RegisterSFXSource(AudioSource s) => Register(s, sfxSources);
    public static void UnregisterSFXSource(AudioSource s) => sfxSources.Remove(s);

    private static void Register(AudioSource source, HashSet<AudioSource> list)
    {
        if (source == null) return;
        list.Add(source);
        if (instance != null) instance.Apply();
    }
    private static void SetLevel(float value, int channel)
    {
        if (instance == null) Bootstrap();
        value = Mathf.Clamp01(value);
        if (channel == 0) { instance.masterVolume = value; PlayerPrefs.SetFloat(MasterKey, value); }
        else if (channel == 1) { instance.musicVolume = value; PlayerPrefs.SetFloat(MusicKey, value); }
        else { instance.sfxVolume = value; PlayerPrefs.SetFloat(SfxKey, value); }
        instance.Apply();
        OnAudioSettingsChanged?.Invoke();
    }
    private static void SetMute(bool value, int channel)
    {
        if (instance == null) Bootstrap();
        if (channel == 0) { instance.masterMuted = value; PlayerPrefs.SetInt(MasterMuteKey, value ? 1 : 0); }
        else if (channel == 1) { instance.musicMuted = value; PlayerPrefs.SetInt(MusicMuteKey, value ? 1 : 0); }
        else { instance.sfxMuted = value; PlayerPrefs.SetInt(SfxMuteKey, value ? 1 : 0); }
        instance.Apply();
        OnAudioSettingsChanged?.Invoke();
    }
    private void Apply()
    {
        AudioListener.volume = masterMuted ? 0f : masterVolume;
        ApplyChannel(musicSources, musicMuted ? 0f : musicVolume);
        ApplyChannel(sfxSources, sfxMuted ? 0f : sfxVolume);
    }
    private static void ApplyChannel(HashSet<AudioSource> list, float volume)
    {
        list.RemoveWhere(s => s == null);
        foreach (AudioSource source in list) source.volume = volume;
    }
}
