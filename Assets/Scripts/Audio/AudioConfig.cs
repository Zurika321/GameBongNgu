using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioConfig", menuName = "Bóng Ngủ/Audio Config")]
public sealed class AudioConfig : ScriptableObject
{
    [Serializable]
    public sealed class MusicEntry
    {
        [RegistryId(RegistryIdType.Music)]
        public string id = "menu";

        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool loop = true;

        [Tooltip("Thời gian phát tối đa tính bằng giây. 0 = không tự dừng; nếu Loop tắt thì clip tự kết thúc theo độ dài thực tế.")]
        [Min(0f)] public float stopAfterSeconds;
    }

    [Serializable]
    public sealed class SfxEntry
    {
        [RegistryId(RegistryIdType.Sfx)]
        public string id = "footstep";

        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
    }

    [Header("Music")]
    [SerializeField] private List<MusicEntry> musics = new List<MusicEntry>();

    [Header("SFX")]
    [SerializeField] private List<SfxEntry> sfx = new List<SfxEntry>();

    [NonSerialized] private Dictionary<string, MusicEntry> musicLookup;
    [NonSerialized] private Dictionary<string, SfxEntry> sfxLookup;

    public IReadOnlyList<MusicEntry> Musics => musics;
    public IReadOnlyList<SfxEntry> Sfx => sfx;

    private void OnEnable() => RebuildLookup();
    private void OnValidate() => RebuildLookup();

    public void EnsureDefaults()
    {
        if (musics == null || musics.Count == 0)
        {
            musics = new List<MusicEntry>
            {
                new MusicEntry
                {
                    id = "menu",
                    clip = Resources.Load<AudioClip>("Volume/Music/nhac_bong_ngu"),
                    volume = 1f,
                    pitch = 1f,
                    loop = true,
                    stopAfterSeconds = 0f,
                }
            };
        }

        if (sfx == null || sfx.Count == 0)
        {
            sfx = new List<SfxEntry>
            {
                new SfxEntry
                {
                    id = "footstep",
                    clip = Resources.Load<AudioClip>("Volume/SFX/tieng_di_bo"),
                    volume = 1f,
                    pitch = 1f,
                }
            };
        }

        RebuildLookup();
    }

    public void RebuildLookup()
    {
        musicLookup = new Dictionary<string, MusicEntry>(StringComparer.Ordinal);
        sfxLookup = new Dictionary<string, SfxEntry>(StringComparer.Ordinal);

        if (musics != null)
        {
            foreach (MusicEntry entry in musics)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                if (musicLookup.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[AudioConfig] Music ID trùng: '{entry.id}'. Chỉ giữ dòng đầu tiên.");
                    continue;
                }

                musicLookup.Add(entry.id, entry);
            }
        }

        if (sfx != null)
        {
            foreach (SfxEntry entry in sfx)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                if (sfxLookup.ContainsKey(entry.id))
                {
                    Debug.LogWarning($"[AudioConfig] SFX ID trùng: '{entry.id}'. Chỉ giữ dòng đầu tiên.");
                    continue;
                }

                sfxLookup.Add(entry.id, entry);
            }
        }
    }

    public bool TryGetMusic(string id, out MusicEntry entry)
    {
        EnsureLookup();
        if (!string.IsNullOrWhiteSpace(id) && musicLookup.TryGetValue(id, out entry))
            return true;

        entry = null;
        return false;
    }

    public bool TryGetSfx(string id, out SfxEntry entry)
    {
        EnsureLookup();
        if (!string.IsNullOrWhiteSpace(id) && sfxLookup.TryGetValue(id, out entry))
            return true;

        entry = null;
        return false;
    }

    public AudioClip GetMusicClip(string id)
    {
        if (TryGetMusic(id, out MusicEntry entry))
            return entry.clip;
        return null;
    }

    public AudioClip GetSfxClip(string id)
    {
        if (TryGetSfx(id, out SfxEntry entry))
            return entry.clip;
        return null;
    }

    private void EnsureLookup()
    {
        if (musicLookup == null || sfxLookup == null)
            RebuildLookup();
    }
}
