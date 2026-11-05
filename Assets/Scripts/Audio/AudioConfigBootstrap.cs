using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class AudioConfigBootstrap
{
    public const string ResourceName = "AudioConfig";

    public static AudioConfig EnsureLoaded()
    {
        AudioConfig config = Resources.Load<AudioConfig>(ResourceName);
        if (config != null)
        {
            config.EnsureDefaults();
            return config;
        }

#if UNITY_EDITOR
        string assetPath = Path.Combine(Application.dataPath, "Resources", "AudioConfig.asset");
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        AudioConfig created = ScriptableObject.CreateInstance<AudioConfig>();
        created.EnsureDefaults();
        AssetDatabase.CreateAsset(created, "Assets/Resources/AudioConfig.asset");
        AssetDatabase.SaveAssets();
        return created;
#else
        Debug.LogWarning("[AudioConfigBootstrap] Không tìm thấy Assets/Resources/AudioConfig.asset. Hãy tạo asset AudioConfig trong Unity để dùng music/SFX ID.");
        return null;
#endif
    }
}
