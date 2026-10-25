using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects optional settings controls to VolumeManager and the display mode.
/// Assign controls in the Inspector.
/// </summary>
public sealed class VolumeSettingsUI : MonoBehaviour
{
    [Header("Optional sliders (range 0 to 1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Optional mute toggles")]
    [SerializeField] private Toggle masterMuteToggle;
    [SerializeField] private Toggle musicMuteToggle;
    [SerializeField] private Toggle sfxMuteToggle;

    [Header("Optional display control")]
    [SerializeField] private Toggle fullscreenToggle;

    private void OnEnable()
    {
        VolumeManager.OnAudioSettingsChanged += SyncControls;
        BindSliders();
        BindMuteToggles();
        Bind(fullscreenToggle, OnFullscreenChanged);
        SyncControls();
    }

    private void Start() => SyncControls();

    private void OnDisable()
    {
        VolumeManager.OnAudioSettingsChanged -= SyncControls;
        UnbindSliders();
        UnbindMuteToggles();
        Unbind(fullscreenToggle, OnFullscreenChanged);
    }

    public void OnMasterVolumeChanged(float value) => VolumeManager.SetMasterVolume(value);
    public void OnMusicVolumeChanged(float value) => VolumeManager.SetMusicVolume(value);
    public void OnSFXVolumeChanged(float value) => VolumeManager.SetSFXVolume(value);

    public void OnMasterMuteChanged(bool isEnabled) => VolumeManager.SetMasterMuted(!isEnabled);
    public void OnMusicMuteChanged(bool isEnabled) => VolumeManager.SetMusicMuted(!isEnabled);
    public void OnSFXMuteChanged(bool isEnabled) => VolumeManager.SetSFXMuted(!isEnabled);
    public void OnFullscreenChanged(bool isFullscreen) => Screen.fullScreen = isFullscreen;

    public void BindSliders(Slider master, Slider music, Slider sfx)
    {
        UnbindSliders();
        masterSlider = master;
        musicSlider = music;
        sfxSlider = sfx;
        if (isActiveAndEnabled) BindSliders();
        SyncControls();
    }

    public void BindMasterSlider(Slider slider)
    {
        Unbind(masterSlider, OnMasterVolumeChanged);
        masterSlider = slider;
        if (isActiveAndEnabled) Bind(masterSlider, OnMasterVolumeChanged);
        SyncControls();
    }

    private void BindSliders()
    {
        Bind(masterSlider, OnMasterVolumeChanged);
        Bind(musicSlider, OnMusicVolumeChanged);
        Bind(sfxSlider, OnSFXVolumeChanged);
    }

    private void UnbindSliders()
    {
        Unbind(masterSlider, OnMasterVolumeChanged);
        Unbind(musicSlider, OnMusicVolumeChanged);
        Unbind(sfxSlider, OnSFXVolumeChanged);
    }

    private void BindMuteToggles()
    {
        Bind(masterMuteToggle, OnMasterMuteChanged);
        Bind(musicMuteToggle, OnMusicMuteChanged);
        Bind(sfxMuteToggle, OnSFXMuteChanged);
    }

    private void UnbindMuteToggles()
    {
        Unbind(masterMuteToggle, OnMasterMuteChanged);
        Unbind(musicMuteToggle, OnMusicMuteChanged);
        Unbind(sfxMuteToggle, OnSFXMuteChanged);
    }

    private void SyncControls()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);

        if (!VolumeManager.IsReady) return;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(VolumeManager.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(VolumeManager.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(VolumeManager.SFXVolume);

        if (masterMuteToggle != null) masterMuteToggle.SetIsOnWithoutNotify(!VolumeManager.MasterMuted);
        if (musicMuteToggle != null) musicMuteToggle.SetIsOnWithoutNotify(!VolumeManager.MusicMuted);
        if (sfxMuteToggle != null) sfxMuteToggle.SetIsOnWithoutNotify(!VolumeManager.SFXMuted);
    }

    private static void Bind(Toggle toggle, UnityEngine.Events.UnityAction<bool> callback)
    {
        if (toggle == null) return;
        toggle.onValueChanged.RemoveListener(callback);
        toggle.onValueChanged.AddListener(callback);
    }

    private static void Unbind(Toggle toggle, UnityEngine.Events.UnityAction<bool> callback)
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(callback);
    }

    private static void Bind(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.RemoveListener(callback);
        slider.onValueChanged.AddListener(callback);
    }

    private static void Unbind(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider != null) slider.onValueChanged.RemoveListener(callback);
    }
}