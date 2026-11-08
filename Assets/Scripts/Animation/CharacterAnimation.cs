using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>State API tương thích với các Controller cũ. JSON animation vẫn được tra theo tên string.</summary>
public enum AnimState
{
    Idle,
    Run,
    Attack,
    Skill,
    Hurt,
    Die,
    Stun
}

/// <summary>
/// Âm thanh cho một animation. Các ID được chọn từ GameIdRegistry thông qua RegistryId.
/// Với animation Attack/Skill: dùng Charge -> Cast -> End.
/// Animation khác chỉ dùng SfxId.
/// </summary>
[Serializable]
public sealed class CharacterAnimationAudioBinding
{
    [HideInInspector] public string animationName;
    [HideInInspector] public bool isCombat;
    [Min(0), Tooltip("Frame bắt đầu pha Cast. Charge dùng frame 0 đến Cast Start - 1.")]
    public int castStartFrame;
    [Min(0), Tooltip("Frame cuối cùng thuộc pha Cast. End bắt đầu từ Cast End + 1.")]
    public int castEndFrame;

    [Tooltip("SFX duy nhất phát khi animation thường bắt đầu.")]
    [RegistryId(RegistryIdType.Sfx)] public string sfxId;

    [Tooltip("Attack/Skill: tiếng lấy đà, phát ở đầu animation (frame 0).")]
    [RegistryId(RegistryIdType.Sfx)] public string chargeSfxId;

    [Tooltip("Attack/Skill: tiếng tung chiêu, phát tại Cast Start Frame.")]
    [RegistryId(RegistryIdType.Sfx)] public string castSfxId;

    [Tooltip("Attack/Skill: tiếng kết thúc đòn, phát tại Cast End Frame + 1.")]
    [RegistryId(RegistryIdType.Sfx)] public string endSfxId;
}

/// <summary>Lựa chọn GroupBlock lưu theo tên, không phụ thuộc index Inspector.</summary>
[Serializable]
public sealed class CharacterAnimationGroupSelection
{
    public string groupName;
    public string variantName;
}

/// <summary>Thông tin dropdown lấy từ JSON (không hard-code nhóm/vũ khí).</summary>
public sealed class CharacterAnimationGroupInfo
{
    public string name;
    public string[] variants;
}

/// <summary>
/// CharacterAnimation chạy rig 2D được xuất từ Bone Animator Studio.
/// JSON được kéo vào Unity JSON ở Inspector. Bone transform được nội suy liên tục theo thời gian;
/// Block SpriteRenderer đổi ảnh/visible ở các mốc Block Keyframe.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class CharacterAnimation : MonoBehaviour
{
    [Header("Bone Animator Studio - Unity JSON")]
    [SerializeField, Tooltip("Drag the exported *_unity.json file here.")] private TextAsset unityJsonAsset;
    // Preserve JSON strings already saved in scenes/prefabs during initial testing.
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("unityJson")]
    private string legacyUnityJson;
    private string unityJson => unityJsonAsset != null ? unityJsonAsset.text : legacyUnityJson;
    [SerializeField, Tooltip("0 = dùng Pixels Per Unit trong file Unity JSON; giá trị lớn hơn 0 sẽ ghi đè.")] private float pixelsPerUnitOverride;
    [SerializeField] private bool useUnscaledTime;

    [Header("Âm thanh theo animation (tự sinh tên từ JSON)")]
    [SerializeField] private List<CharacterAnimationAudioBinding> audioBindings = new List<CharacterAnimationAudioBinding>();
    [Header("GroupBlock (Weapon, Outfit, ...)")]
    [SerializeField] private List<CharacterAnimationGroupSelection> groupSelections = new List<CharacterAnimationGroupSelection>();
    [SerializeField, HideInInspector] private bool editorJsonValid;
    [SerializeField, HideInInspector] private string editorValidationMessage;

    [Header("Debug")]
    [SerializeField] private bool logAnimationWarnings = true;

    public string CurrentAnimation => currentAnimation != null ? currentAnimation.name : string.Empty;
    public AnimState CurrentState => string.Equals(CurrentAnimation, "Death", StringComparison.OrdinalIgnoreCase) ? AnimState.Die : (Enum.TryParse(CurrentAnimation, true, out AnimState state) ? state : AnimState.Idle);
    public string CurrentDirection => currentDirection;
    public bool IsPlaying => isPlaying;
    public float CurrentFrame => currentFrame;
    public event Action<string> OnAnimationCompleted;

    private UnityCharacterAnimationData data;
    private UnityAnimationData currentAnimation;
    private UnityAnimationDirectionData currentAnimationDirection;
    private UnityDirectionData currentRigData;
    private string currentDirection = "down";
    private bool isPlaying;
    private bool completedRaised;
    private float elapsed;
    private float currentFrame;
    private bool rootRendererWasEnabled;
    private SpriteRenderer rootSpriteRenderer;

    private Transform rigRoot;
    private readonly Dictionary<int, RuntimeBone> runtimeBones = new Dictionary<int, RuntimeBone>();
    private readonly Dictionary<int, RuntimeBlock> runtimeBlocks = new Dictionary<int, RuntimeBlock>();
    private readonly Dictionary<int, GroupBlockMembership> groupBlockById = new Dictionary<int, GroupBlockMembership>();
    private readonly Dictionary<string, Sprite> runtimeSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> runtimeTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private Sprite whitePixelSprite;
    private readonly Dictionary<int, float> evaluatedBoneLengths = new Dictionary<int, float>();
    private readonly HashSet<string> warningOnce = new HashSet<string>(StringComparer.Ordinal);

    private const float DirectionThreshold = 0.0001f;

    private sealed class GroupBlockMembership
    {
        public string groupName;
        public string variantName;
    }

    private sealed class RuntimeBone
    {
        public UnityBoneData data;
        public Transform transform;
    }

    private sealed class RuntimeBlock
    {
        public UnityBlockData data;
        public Transform container;
        public Transform visual;
        public SpriteRenderer renderer;
        public string lastImageName;
        public bool lastVisible;
    }

    #region Unity lifecycle

    private void Awake()
    {
        rootSpriteRenderer = GetComponent<SpriteRenderer>();
        if (rootSpriteRenderer != null)
        {
            rootRendererWasEnabled = rootSpriteRenderer.enabled;
            // Các SpriteRenderer thực tế được tạo bên dưới Rig từ từng Block của JSON.
            rootSpriteRenderer.enabled = false;
        }

        if (!TryLoadJson(out data, out string error))
        {
            if (rootSpriteRenderer != null) rootSpriteRenderer.enabled = rootRendererWasEnabled;
            Warn("json-awake", "Không thể khởi tạo animation: " + error);
            return;
        }

        // Chỉ số Block ID được xây một lần, tránh duyệt toàn bộ Group mỗi frame.
        RebuildGroupBlockIndex();
        // Đọc mặc định từ JSON trước khi tạo SpriteRenderer.
        SyncGroupSelectionsFromJson();
        BuildDirectionRig(currentDirection);
        currentAnimation = FindAnimation("Default") ?? (data.animations.Length > 0 ? data.animations[0] : null);
        currentAnimationDirection = FindAnimationDirection(currentAnimation, currentDirection);
        if (currentAnimationDirection != null)
        {
            ApplyAnimationPose(0f);
            ApplyBlockStates(0f);
        }
        SyncAudioBindingsFromJson(false);
    }

    private void Update()
    {
        if (GameplayPause.IsPaused) return;
        if (customFrames != null) { UpdateCustomFrames(); return; }
        if (!isPlaying || currentAnimation == null || currentAnimationDirection == null)
            return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta < 0f) delta = 0f;

        float previousFrame = currentFrame;
        float previousElapsed = elapsed;
        elapsed += delta;
        float fps = Mathf.Max(1f, currentAnimationDirection.frameRate);
        float frameCount = Mathf.Max(1, currentAnimationDirection.frameCount);
        float rawFrame = elapsed * fps;

        if (holdingSkillCharge)
        {
            int castStart = FindAudioBinding(CurrentAnimation)?.castStartFrame ?? currentAnimation.castStartFrame;
            // Stop just before Cast so its SFX/effects only start on release.
            float chargeEnd = Mathf.Max(0f, castStart - 0.001f);
            rawFrame = Mathf.Min(rawFrame, chargeEnd);
            elapsed = rawFrame / fps;
        }

        if (currentAnimation.loop && !playingSkill)
        {
            int previousCycle = Mathf.FloorToInt(previousElapsed * fps / frameCount);
            int currentCycle = Mathf.FloorToInt(rawFrame / frameCount);
            bool wrapped = currentCycle > previousCycle;
            currentFrame = Mathf.Repeat(rawFrame, frameCount);
            if (wrapped && !currentAnimation.isCombat) PlayAnimationStartSound(currentAnimation);
            TriggerCombatPhaseCrossings(previousFrame, currentFrame, wrapped);
        }
        else
        {
            currentFrame = Mathf.Min(rawFrame, frameCount);
            TriggerCombatPhaseCrossings(previousFrame, currentFrame, false);
        }

        ApplyAnimationPose(currentFrame);
        ApplyBlockStates(currentFrame);

        if ((!currentAnimation.loop || playingSkill) && !holdingSkillCharge && rawFrame >= frameCount && !completedRaised)
        {
            isPlaying = false;
            completedRaised = true;
            playingSkill = false;
            holdingSkillCharge = false;
            OnAnimationCompleted?.Invoke(currentAnimation.name);
        }
    }

    private void OnDestroy()
    {
        RestoreRootRenderer();
        DestroyGeneratedRig();

        foreach (Sprite sprite in runtimeSprites.Values)
            if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in runtimeTextures.Values)
            if (texture != null) Destroy(texture);

        runtimeSprites.Clear();
        runtimeTextures.Clear();
        if (whitePixelSprite != null) Destroy(whitePixelSprite);
        whitePixelSprite = null;
    }

    private void OnValidate()
    {
        cachedEditorGroups = null; // JSON asset vừa thay đổi trong Inspector.
        pixelsPerUnitOverride = Mathf.Max(0f, pixelsPerUnitOverride);
        if (string.IsNullOrWhiteSpace(unityJson))
        {
            editorJsonValid = false;
            editorValidationMessage = "Chưa gán Unity JSON. Dán nội dung file *_unity.json vào ô phía trên.";
        }
        else if (TryLoadJson(out _, out string error))
        {
            editorJsonValid = true;
            editorValidationMessage = "JSON hợp lệ · schema 1 · đủ 4 hướng · " + (data != null && data.animations != null ? data.animations.Length : CountAnimationsInHeader()) + " animation.";
        }
        else
        {
            editorJsonValid = false;
            editorValidationMessage = error;
            Warn("inspector-invalid-json", "Unity JSON chưa hợp lệ: " + error);
        }
        SyncAudioBindingsFromJson(true);
        if (editorJsonValid) SyncGroupSelectionsFromJson();
    }

    /// <summary>Dùng để đồng bộ lại danh sách âm thanh khi JSON được dán/thay đổi trong Inspector.</summary>
    public void RefreshAudioBindings()
    {
        SyncAudioBindingsFromJson(true);
        cachedEditorGroups = null;
        if (editorJsonValid) SyncGroupSelectionsFromJson();
    }

    public bool GetInspectorJsonStatus(out string message)
    {
        message = editorValidationMessage;
        return editorJsonValid;
    }

    private int CountAnimationsInHeader()
    {
        try { return JsonUtility.FromJson<UnityAnimationHeaderRoot>(unityJson)?.animations?.Length ?? 0; }
        catch { return 0; }
    }

    /// <summary>Danh sách GroupBlock/biến thể trong JSON cho dropdown Editor.</summary>
    public CharacterAnimationGroupInfo[] GetGroupVariantDefinitions()
    {
        UnityGroupBlockData[] groups = GetConfiguredGroups();
        List<CharacterAnimationGroupInfo> infos = new List<CharacterAnimationGroupInfo>(groups.Length);
        foreach (UnityGroupBlockData group in groups)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.name)) continue;
            List<string> variants = new List<string>();
            if (group.variants != null)
                foreach (UnityGroupVariantData variant in group.variants)
                    if (variant != null && !string.IsNullOrWhiteSpace(variant.name)) variants.Add(variant.name);
            infos.Add(new CharacterAnimationGroupInfo { name = group.name, variants = variants.ToArray() });
        }
        return infos.ToArray();
    }

    /// <summary>Chọn biến thể bằng đúng tên Group / Variant, chuỗi rỗng nghĩa là None.</summary>
    public bool SetGroupVariant(string groupName, string variantName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return false;
        if (variantName == null) variantName = string.Empty;
        UnityGroupBlockData[] groups = GetConfiguredGroups();
        UnityGroupBlockData group = Array.Find(groups, g => g != null && string.Equals(g.name, groupName, StringComparison.Ordinal));
        if (group == null)
        {
            Warn("group-not-found:" + groupName, "GroupBlock '" + groupName + "' không tồn tại.");
            return false;
        }
        if (variantName.Length > 0 && (group.variants == null || Array.Find(group.variants, v => v != null && string.Equals(v.name, variantName, StringComparison.Ordinal)) == null))
        {
            Warn("variant-not-found:" + groupName + ":" + variantName, "Biến thể '" + variantName + "' không tồn tại trong group '" + groupName + "'.");
            return false;
        }
        if (groupSelections == null) groupSelections = new List<CharacterAnimationGroupSelection>();
        CharacterAnimationGroupSelection selection = groupSelections.Find(x => x != null && string.Equals(x.groupName, groupName, StringComparison.Ordinal));
        if (selection == null)
        {
            selection = new CharacterAnimationGroupSelection { groupName = groupName };
            groupSelections.Add(selection);
        }
        selection.variantName = variantName;
        RefreshGroupVariantVisibility();
        return true;
    }

    public string GetGroupVariant(string groupName)
    {
        if (groupSelections != null)
        {
            CharacterAnimationGroupSelection selection = groupSelections.Find(x => x != null && string.Equals(x.groupName, groupName, StringComparison.Ordinal));
            if (selection != null) return selection.variantName ?? string.Empty;
        }
        UnityGroupBlockData group = Array.Find(GetConfiguredGroups(), g => g != null && string.Equals(g.name, groupName, StringComparison.Ordinal));
        return group?.defaultVariant ?? string.Empty;
    }

    /// <summary>Cập nhật SpriteRenderer ngay, không khởi động lại frame hoặc animation.</summary>
    public void RefreshGroupVariantVisibility()
    {
        if (data != null && currentAnimationDirection != null) ApplyBlockStates(currentFrame);
    }

    private UnityGroupBlockData[] cachedEditorGroups;
    private TextAsset cachedGroupJsonAsset;
    private string cachedGroupLegacyJson;

    private UnityGroupBlockData[] GetConfiguredGroups()
    {
        if (data?.groupBlocks != null) return data.groupBlocks;
        if (cachedEditorGroups != null && cachedGroupJsonAsset == unityJsonAsset &&
            string.Equals(cachedGroupLegacyJson, legacyUnityJson, StringComparison.Ordinal)) return cachedEditorGroups;
        try
        {
            if (string.IsNullOrWhiteSpace(unityJson)) return Array.Empty<UnityGroupBlockData>();
            UnityGroupBlockHeaderRoot parsed = JsonUtility.FromJson<UnityGroupBlockHeaderRoot>(unityJson);
            cachedEditorGroups = parsed?.groupBlocks ?? Array.Empty<UnityGroupBlockData>();
            cachedGroupJsonAsset = unityJsonAsset;
            cachedGroupLegacyJson = legacyUnityJson;
            return cachedEditorGroups;
        }
        catch { return Array.Empty<UnityGroupBlockData>(); }
    }

    private void SyncGroupSelectionsFromJson()
    {
        UnityGroupBlockData[] groups = GetConfiguredGroups();
        if (groupSelections == null) groupSelections = new List<CharacterAnimationGroupSelection>();
        Dictionary<string, string> previous = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CharacterAnimationGroupSelection selection in groupSelections)
            if (selection != null && !string.IsNullOrWhiteSpace(selection.groupName)) previous[selection.groupName] = selection.variantName ?? string.Empty;
        List<CharacterAnimationGroupSelection> next = new List<CharacterAnimationGroupSelection>();
        foreach (UnityGroupBlockData group in groups)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.name)) continue;
            string desired = previous.TryGetValue(group.name, out string saved) ? saved : (group.defaultVariant ?? string.Empty);
            if (desired.Length > 0 && (group.variants == null || Array.Find(group.variants, v => v != null && v.name == desired) == null))
                desired = group.defaultVariant ?? string.Empty;
            next.Add(new CharacterAnimationGroupSelection { groupName = group.name, variantName = desired });
        }
        // Không thay danh sách nếu giống hệt để tránh OnValidate lặp/dirty Inspector vô ích.
        if (next.Count == groupSelections.Count)
        {
            bool equal = true;
            for (int i = 0; i < next.Count; i++)
                if (groupSelections[i] == null || groupSelections[i].groupName != next[i].groupName || groupSelections[i].variantName != next[i].variantName) { equal = false; break; }
            if (equal) return;
        }
        groupSelections = next;
        RefreshGroupVariantVisibility();
    }

    private void RebuildGroupBlockIndex()
    {
        groupBlockById.Clear();
        foreach (UnityGroupBlockData group in GetConfiguredGroups())
        {
            if (group?.variants == null) continue;
            foreach (UnityGroupVariantData variant in group.variants)
            {
                if (variant?.blockIds == null) continue;
                foreach (int id in variant.blockIds)
                    groupBlockById[id] = new GroupBlockMembership { groupName = group.name, variantName = variant.name };
            }
        }
    }

    private bool IsBlockVisibleInGroup(int blockId)
    {
        if (!groupBlockById.TryGetValue(blockId, out GroupBlockMembership group)) return true;
        return string.Equals(GetGroupVariant(group.groupName), group.variantName, StringComparison.Ordinal);
    }

    #endregion

    // Compatibility for existing skill sprites and projectile flipbooks.
    private Sprite[] customFrames;
    private float[] customDurations;
    private float customFps;
    private bool customLoop;
    private int customIndex;
    private float customTimer;

    public void PlayCustom(Sprite[] frames, float customFrameRate = 10f, bool customLoop = false)
        => PlayCustom(frames, null, customFrameRate, customLoop);

    public void PlayCustom(Sprite[] frames, float[] frameDurations, float customFrameRate = 10f, bool customLoop = false)
    {
        if (frames == null || frames.Length == 0 || rootSpriteRenderer == null) return;
        Stop();
        customFrames = frames;
        customDurations = frameDurations;
        customFps = customFrameRate;
        this.customLoop = customLoop;
        customIndex = 0;
        customTimer = 0f;
        if (rigRoot != null) rigRoot.gameObject.SetActive(false);
        rootSpriteRenderer.enabled = true;
        rootSpriteRenderer.sprite = frames[0];
    }

    private void UpdateCustomFrames()
    {
        customTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        while (customFrames != null)
        {
            float duration = customDurations != null && customIndex < customDurations.Length
                ? customDurations[customIndex] : 0f;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
                duration = !float.IsNaN(customFps) && !float.IsInfinity(customFps) && customFps > 0f ? 1f / customFps : 0.1f;
            if (customTimer < duration) return;
            customTimer -= duration;
            if (customIndex == customFrames.Length - 1 && !customLoop) { customTimer = 0f; return; }
            customIndex = (customIndex + 1) % customFrames.Length;
            rootSpriteRenderer.sprite = customFrames[customIndex];
        }
    }

    private bool startingSkill;
    private bool awaitingSkillCast;
    private bool playingSkill;
    private bool holdingSkillCharge;
    public bool IsPlayingSkill(string skillId) => playingSkill && currentAnimation != null && FindAnimation(skillId) == currentAnimation;

    public bool PlayAnimationSkill(string skillId, bool holdCharge = false)
    {
        CancelSkillAnimation();
        startingSkill = true;
        bool started = Play(skillId, true);
        startingSkill = false;
        if (!started) return false;
        awaitingSkillCast = true;
        playingSkill = true;
        holdingSkillCharge = holdCharge;
        if (!holdCharge) BeginSkillCast();
        else
        {
            CharacterAnimationAudioBinding binding = FindAudioBinding(CurrentAnimation);
            if (binding != null && binding.castStartFrame > 0) PlaySfx(binding.chargeSfxId, CurrentAnimation, "Charge");
        }
        return true;
    }

    public void BeginSkillCast()
    {
        if (!playingSkill || !awaitingSkillCast || currentAnimationDirection == null) return;
        awaitingSkillCast = false;
        CharacterAnimationAudioBinding binding = FindAudioBinding(CurrentAnimation);
        if (binding != null) PlaySfx(binding.castSfxId, CurrentAnimation, "Cast");
        int castStart = FindAudioBinding(CurrentAnimation)?.castStartFrame ?? currentAnimation.castStartFrame;
        float nextFrame = Mathf.Clamp(castStart, 0, currentAnimationDirection.frameCount);
        if (currentFrame < nextFrame)
        {
            currentFrame = nextFrame;
            elapsed = nextFrame / Mathf.Max(1f, currentAnimationDirection.frameRate);
            ApplyAnimationPose(currentFrame);
            ApplyBlockStates(currentFrame);
        }
        holdingSkillCharge = false;
    }

    public void CancelSkillAnimation()
    {
        if (playingSkill) Stop();
        playingSkill = false;
        holdingSkillCharge = false;
    }

    #region Public API

    /// <summary>
    /// Chạy animation theo đúng tên Animation Set đã xuất từ Bone Animator Studio.
    /// Ví dụ: Play("Idle"), Play("Run"), Play("Attack_Slash"), Play("Skill_FlockDive").
    /// Trả về false và phát Warning nếu tên animation/hướng không tồn tại.
    /// </summary>
    public bool Play(AnimState state, bool restart = false)
    {
        if (playingSkill)
        {
            if (state == AnimState.Idle || state == AnimState.Run) return false;
            CancelSkillAnimation();
        }
        string animationName = state.ToString();
        if (state == AnimState.Die)
        {
            if (data == null && TryLoadJson(out UnityCharacterAnimationData loaded, out _)) data = loaded;
            if (FindAnimation("Die") == null && FindAnimation("Death") != null)
                animationName = "Death";
        }
        return Play(animationName, restart);
    }

    public bool Play(string animationName, bool restart = false)
    {
        if (customFrames != null) return false;
        if (string.IsNullOrWhiteSpace(animationName))
        {
            Warn("play-empty", "Play(string) nhận tên animation rỗng.");
            return false;
        }

        if (data == null && !TryLoadJson(out data, out string error))
        {
            Warn("play-json", "Không thể Play vì Unity JSON chưa hợp lệ: " + error);
            return false;
        }

        UnityAnimationData next = FindAnimation(animationName);
        if (next == null)
        {
            Warn("missing-animation:" + animationName,
                $"Không tìm thấy animation '{animationName}' trong model '{data.modelName}'. Các tên hợp lệ: {GetAnimationNames()}.");
            return false;
        }

        // Controllers request Die every physics tick; completion must keep its final pose.
        if (!restart && currentAnimation == next &&
            (isPlaying || (completedRaised && CurrentState == AnimState.Die)))
            return true;

        currentAnimation = next;
        currentAnimationDirection = FindAnimationDirection(next, currentDirection);
        if (currentAnimationDirection == null)
        {
            Warn("missing-animation-direction:" + animationName + ":" + currentDirection,
                $"Animation '{animationName}' không có track cho hướng '{currentDirection}'.");
            isPlaying = false;
            return false;
        }

        if (rigRoot == null || currentRigData == null || currentRigData.name != currentDirection)
            BuildDirectionRig(currentDirection);

        elapsed = 0f;
        currentFrame = 0f;
        isPlaying = true;
        completedRaised = false;
        ApplyAnimationPose(0f);
        ApplyBlockStates(0f);
        if (!startingSkill) PlayAnimationStartSound(next);
        return true;
    }

    /// <summary>Chọn hướng top-down theo trục di chuyển trội.</summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < DirectionThreshold)
            return;

        string next = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? (direction.x < 0f ? "left" : "right")
            : (direction.y < 0f ? "down" : "up");

        if (next == currentDirection)
            return;

        float normalizedProgress = currentAnimationDirection != null
            ? currentFrame / Mathf.Max(1, currentAnimationDirection.frameCount)
            : 0f;
        currentDirection = next;
        BuildDirectionRig(currentDirection);

        if (currentAnimation != null)
        {
            currentAnimationDirection = FindAnimationDirection(currentAnimation, currentDirection);
            if (currentAnimationDirection == null)
            {
                Warn("facing-track:" + currentAnimation.name + ":" + currentDirection,
                    $"Animation '{currentAnimation.name}' thiếu dữ liệu hướng '{currentDirection}'. Animation đã dừng.");
                isPlaying = false;
                return;
            }

            currentFrame = Mathf.Clamp01(normalizedProgress) * currentAnimationDirection.frameCount;
            if (isPlaying)
                elapsed = currentFrame / Mathf.Max(1f, currentAnimationDirection.frameRate);
            ApplyAnimationPose(currentFrame);
            ApplyBlockStates(currentFrame);
        }
    }

    /// <summary>Dừng animation tại pose hiện tại; không tự phát lại Idle.</summary>
    public void Stop()
    {
        isPlaying = false;
    }

    /// <summary>Alias tương thích với code skill cũ; animation custom giờ cũng là Animation Set trong JSON.</summary>
    public void StopCustomPlayback()
    {
        Stop();
        if (customFrames == null) return;
        customFrames = null;
        customDurations = null;
        if (rigRoot != null) rigRoot.gameObject.SetActive(true);
        if (rootSpriteRenderer != null) rootSpriteRenderer.enabled = rigRoot == null && rootRendererWasEnabled;
    }

    /// <summary>Đổi hướng bằng tên: down, up, left, right.</summary>
    public bool SetFacing(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return false;

        string normalized = direction.Trim().ToLowerInvariant();
        if (normalized != "down" && normalized != "up" && normalized != "left" && normalized != "right")
        {
            Warn("invalid-facing:" + normalized, $"Hướng '{direction}' không hợp lệ. Chỉ hỗ trợ down/up/left/right.");
            return false;
        }

        if (normalized == currentDirection)
            return true;

        Vector2 vector = normalized == "left" ? Vector2.left
            : normalized == "right" ? Vector2.right
            : normalized == "up" ? Vector2.up : Vector2.down;
        SetFacing(vector);
        return currentDirection == normalized;
    }

    #endregion

    #region JSON loading and validation

    private bool TryLoadJson(out UnityCharacterAnimationData parsed, out string error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(unityJson))
        {
            error = "Biến Unity JSON đang trống. Hãy dán nội dung file *_unity.json vào Inspector.";
            return false;
        }

        try
        {
            parsed = JsonUtility.FromJson<UnityCharacterAnimationData>(unityJson);
            if (parsed == null)
            {
                error = "JsonUtility không đọc được dữ liệu.";
                return false;
            }
            if (!string.Equals(parsed.format, "BoneAnimatorUnity", StringComparison.Ordinal))
            {
                error = $"Sai format '{parsed.format}'. Cần file được xuất bằng nút Unity JSON.";
                return false;
            }
            if (parsed.schemaVersion != 1)
            {
                error = $"Schema version {parsed.schemaVersion} chưa được hỗ trợ (hiện hỗ trợ 1).";
                return false;
            }
            if (parsed.directions == null || parsed.directions.Length == 0)
            {
                error = "JSON không có dữ liệu directions.";
                return false;
            }
            if (parsed.animations == null || parsed.animations.Length == 0)
            {
                error = "JSON không có Animation Set.";
                return false;
            }
            if (parsed.assets == null)
                parsed.assets = Array.Empty<UnityAssetData>();

            error = ValidateParsedData(parsed);
            if (!string.IsNullOrEmpty(error))
            {
                parsed = null;
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "JSON parse lỗi: " + exception.Message;
            parsed = null;
            return false;
        }
    }

    private static string ValidateParsedData(UnityCharacterAnimationData parsed)
    {
        string[] required = { "down", "up", "left", "right" };
        HashSet<string> assetNames = new HashSet<string>(StringComparer.Ordinal);
        if (parsed.assets != null)
        {
            foreach (UnityAssetData asset in parsed.assets)
                if (asset != null && !string.IsNullOrWhiteSpace(asset.name) && !string.IsNullOrWhiteSpace(asset.dataUrl))
                    assetNames.Add(asset.name);
        }

        Dictionary<string, HashSet<int>> boneIdsByDirection = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        Dictionary<string, HashSet<int>> blockIdsByDirection = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        for (int i = 0; i < required.Length; i++)
        {
            UnityDirectionData rig = FindDirection(parsed, required[i]);
            if (rig == null)
                return "JSON thiếu hướng '" + required[i] + "'.";
            if (rig.bones == null || rig.bones.Length == 0)
                return "JSON hướng '" + required[i] + "' chưa có Bone.";

            HashSet<int> boneIds = new HashSet<int>();
            foreach (UnityBoneData bone in rig.bones)
            {
                if (bone == null) continue;
                if (!boneIds.Add(bone.id))
                    return $"Hướng '{required[i]}' có Bone ID trùng ({bone.id}).";
            }
            foreach (UnityBoneData bone in rig.bones)
            {
                if (bone != null && bone.parentId != 0 && !boneIds.Contains(bone.parentId))
                    return $"Hướng '{required[i]}': Bone '{bone.name}' trỏ parent {bone.parentId} không tồn tại.";
            }

            // Phát hiện vòng lặp parent trước khi dựng Transform hierarchy.
            Dictionary<int, int> parentMap = new Dictionary<int, int>();
            foreach (UnityBoneData bone in rig.bones)
                if (bone != null) parentMap[bone.id] = bone.parentId;
            foreach (UnityBoneData bone in rig.bones)
            {
                if (bone == null) continue;
                HashSet<int> visited = new HashSet<int>();
                int id = bone.id;
                while (id != 0 && parentMap.TryGetValue(id, out int parentId))
                {
                    if (!visited.Add(id)) return $"Hướng '{required[i]}' có Bone hierarchy bị vòng lặp tại ID {id}.";
                    id = parentId;
                }
            }

            if (rig.blocks == null || rig.blocks.Length == 0)
                return "JSON hướng '" + required[i] + "' chưa có Block/SpriteRenderer để vẽ.";
            HashSet<int> blockIds = new HashSet<int>();
            if (rig.blocks != null)
            {
                foreach (UnityBlockData block in rig.blocks)
                {
                    if (block == null) continue;
                    if (!blockIds.Add(block.id))
                        return $"Hướng '{required[i]}' có Block ID trùng ({block.id}).";
                    if (block.boneId != 0 && !boneIds.Contains(block.boneId))
                        return $"Block '{block.name}' hướng '{required[i]}' trỏ Bone {block.boneId} không tồn tại.";

                    if (block.drawImage)
                    {
                        string imageName = required[i] == "up" ? (string.IsNullOrEmpty(block.imageUp) ? block.imageDown : block.imageUp)
                            : required[i] == "left" ? (string.IsNullOrEmpty(block.imageLeft) ? block.imageDown : block.imageLeft)
                            : required[i] == "right" ? (string.IsNullOrEmpty(block.imageRight) ? block.imageDown : block.imageRight)
                            : block.imageDown;
                        if (string.IsNullOrEmpty(imageName) || !assetNames.Contains(imageName))
                            return $"Block '{block.name}' thiếu ảnh PNG hợp lệ cho hướng '{required[i]}'.";
                    }
                }
            }

            boneIdsByDirection[required[i]] = boneIds;
            blockIdsByDirection[required[i]] = blockIds;
        }

        // GroupBlock là extension optional của schema 1: file cũ không cần groupBlocks.
        if (parsed.groupBlocks != null)
        {
            HashSet<string> groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> assignedBlocks = new HashSet<int>();
            HashSet<int> allBlockIds = new HashSet<int>();
            foreach (HashSet<int> perDirection in blockIdsByDirection.Values) allBlockIds.UnionWith(perDirection);
            foreach (UnityGroupBlockData group in parsed.groupBlocks)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.name) || !groupNames.Add(group.name))
                    return "GroupBlock có nhóm rỗng hoặc trùng tên.";
                HashSet<string> variantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (group.variants != null)
                {
                    foreach (UnityGroupVariantData variant in group.variants)
                    {
                        if (variant == null || string.IsNullOrWhiteSpace(variant.name) || !variantNames.Add(variant.name))
                            return "GroupBlock '" + group.name + "' có biến thể rỗng hoặc trùng tên.";
                        if (variant.blockIds == null) continue;
                        foreach (int id in variant.blockIds)
                        {
                            if (!allBlockIds.Contains(id)) return "GroupBlock '" + group.name + "' tham chiếu Block #" + id + " không tồn tại.";
                            if (!assignedBlocks.Add(id)) return "Block #" + id + " được gán vào nhiều GroupBlock/biến thể.";
                        }
                    }
                }
                if (!string.IsNullOrEmpty(group.defaultVariant) && !variantNames.Contains(group.defaultVariant))
                    return "GroupBlock '" + group.name + "' có lựa chọn mặc định không tồn tại.";
            }
        }

        for (int i = 0; i < parsed.animations.Length; i++)
        {
            UnityAnimationData animation = parsed.animations[i];
            if (animation == null || string.IsNullOrWhiteSpace(animation.name))
                return "Có Animation Set rỗng tên.";
            if (animation.directions == null)
                return "Animation '" + animation.name + "' thiếu directions.";
            for (int d = 0; d < required.Length; d++)
            {
                UnityAnimationDirectionData direction = FindAnimationDirection(animation, required[d]);
                if (direction == null)
                    return $"Animation '{animation.name}' thiếu track hướng '{required[d]}'.";
                if (direction.frameRate <= 0f || direction.frameCount <= 0)
                    return $"Animation '{animation.name}' ({required[d]}) có FPS/frameCount không hợp lệ.";
                if (animation.isCombat && (animation.castStartFrame < 0 || animation.castEndFrame < animation.castStartFrame || animation.castEndFrame >= direction.frameCount))
                    return $"Animation '{animation.name}' có Cast Start/End ngoài phạm vi hướng '{required[d]}' (0–{direction.frameCount - 1}).";

                if (direction.boneTracks != null)
                {
                    foreach (UnityBoneTrackData track in direction.boneTracks)
                    {
                        if (track != null && !boneIdsByDirection[required[d]].Contains(track.boneId))
                            return $"Animation '{animation.name}' ({required[d]}) có track cho Bone {track.boneId} không tồn tại.";
                    }
                }
                if (direction.blockKeyframes != null)
                {
                    foreach (UnityBlockKeyframeData key in direction.blockKeyframes)
                    {
                        if (key == null) continue;
                        if (key.frame < 0f || key.frame > direction.frameCount)
                            return $"Animation '{animation.name}' ({required[d]}) có Block Keyframe ngoài phạm vi.";
                        if (key.states == null) continue;
                        foreach (UnityBlockStateData state in key.states)
                        {
                            if (state != null && !blockIdsByDirection[required[d]].Contains(state.blockId))
                                return $"Animation '{animation.name}' ({required[d]}) trỏ Block {state.blockId} không tồn tại.";
                            if (state != null && !string.IsNullOrEmpty(state.imageName) && !assetNames.Contains(state.imageName))
                                return $"Animation '{animation.name}' ({required[d]}) thiếu ảnh Block Keyframe '{state.imageName}'.";
                        }
                    }
                }
            }
        }
        return null;
    }

    private UnityAnimationData FindAnimation(string animationName)
    {
        animationName = ResolveAnimationSourceName(animationName);
        if (data?.animations == null) return null;
        for (int i = 0; i < data.animations.Length; i++)
            if (data.animations[i] != null && string.Equals(data.animations[i].name, animationName, StringComparison.Ordinal))
                return data.animations[i];
        return null;
    }

    private static UnityDirectionData FindDirection(UnityCharacterAnimationData source, string direction)
    {
        if (source?.directions == null) return null;
        for (int i = 0; i < source.directions.Length; i++)
            if (source.directions[i] != null && string.Equals(source.directions[i].name, direction, StringComparison.Ordinal))
                return source.directions[i];
        return null;
    }

    private static UnityAnimationDirectionData FindAnimationDirection(UnityAnimationData animation, string direction)
    {
        if (animation?.directions == null) return null;
        for (int i = 0; i < animation.directions.Length; i++)
            if (animation.directions[i] != null && string.Equals(animation.directions[i].name, direction, StringComparison.Ordinal))
                return animation.directions[i];
        return null;
    }

    private string GetAnimationNames()
    {
        if (data?.animations == null) return "(không có)";
        List<string> names = new List<string>();
        foreach (UnityAnimationData animation in data.animations)
            if (animation != null) names.Add(GetBoundAnimationName(animation.name));
        return string.Join(", ", names);
    }

    #endregion

    #region Rig and SpriteRenderer construction

    private float PixelsPerUnit
    {
        get
        {
            if (pixelsPerUnitOverride > 0f) return pixelsPerUnitOverride;
            return Mathf.Max(1f, data != null ? data.pixelsPerUnit : 100f);
        }
    }

    private void BuildDirectionRig(string direction)
    {
        if (data == null) return;

        UnityDirectionData directionData = FindDirection(data, direction);
        if (directionData == null)
        {
            Warn("rig-direction:" + direction, $"Không có rig data cho hướng '{direction}'.");
            return;
        }

        DestroyGeneratedRig();
        currentRigData = directionData;
        rigRoot = new GameObject("__CharacterAnimationRig_" + direction).transform;
        rigRoot.SetParent(transform, false);
        rigRoot.localPosition = Vector3.zero;
        rigRoot.localRotation = Quaternion.identity;
        rigRoot.localScale = Vector3.one;

        runtimeBones.Clear();
        evaluatedBoneLengths.Clear();

        if (directionData.bones != null)
        {
            for (int i = 0; i < directionData.bones.Length; i++)
            {
                UnityBoneData bone = directionData.bones[i];
                if (bone == null) continue;
                if (runtimeBones.ContainsKey(bone.id))
                {
                    Warn("duplicate-bone:" + direction + ":" + bone.id, $"Bone ID {bone.id} bị trùng ở hướng '{direction}'.");
                    continue;
                }

                GameObject boneObject = new GameObject("Bone_" + bone.name);
                Transform boneTransform = boneObject.transform;
                boneTransform.SetParent(rigRoot, false);
                boneTransform.localPosition = new Vector3(bone.x / PixelsPerUnit, -bone.y / PixelsPerUnit, 0f);
                boneTransform.localRotation = Quaternion.Euler(0f, 0f, -bone.angle);
                boneTransform.localScale = Vector3.one;
                runtimeBones.Add(bone.id, new RuntimeBone { data = bone, transform = boneTransform });
                evaluatedBoneLengths[bone.id] = bone.length;
            }

            // Parent sau khi đã tạo đủ Transform để thứ tự trong JSON không phụ thuộc hierarchy.
            foreach (RuntimeBone bone in runtimeBones.Values)
            {
                float parentLength = 0f;
                if (bone.data.parentId != 0 && runtimeBones.TryGetValue(bone.data.parentId, out RuntimeBone parentBone))
                {
                    bone.transform.SetParent(parentBone.transform, false);
                    parentLength = parentBone.data.length;
                }
                else
                {
                    bone.transform.SetParent(rigRoot, false);
                }

                bone.transform.localPosition = new Vector3((parentLength + bone.data.x) / PixelsPerUnit, -bone.data.y / PixelsPerUnit, 0f);
                bone.transform.localRotation = Quaternion.Euler(0f, 0f, -bone.data.angle);
            }
        }

        runtimeBlocks.Clear();
        if (directionData.blocks != null)
        {
            for (int i = 0; i < directionData.blocks.Length; i++)
            {
                UnityBlockData block = directionData.blocks[i];
                if (block == null) continue;
                if (runtimeBlocks.ContainsKey(block.id))
                {
                    Warn("duplicate-block:" + direction + ":" + block.id, $"Block ID {block.id} bị trùng ở hướng '{direction}'.");
                    continue;
                }

                Transform parent = rigRoot;
                bool attached = block.boneId != 0;
                if (attached)
                {
                    if (!runtimeBones.TryGetValue(block.boneId, out RuntimeBone bone))
                    {
                        Warn("block-parent:" + block.id + ":" + direction, $"Block '{block.name}' trỏ tới Bone {block.boneId} không tồn tại.");
                        continue;
                    }
                    parent = bone.transform;
                }

                GameObject containerObject = new GameObject("Block_" + block.name);
                Transform container = containerObject.transform;
                container.SetParent(parent, false);

                GameObject visualObject = new GameObject("Sprite_" + block.name);
                Transform visual = visualObject.transform;
                visual.SetParent(container, false);
                SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                renderer.color = block.drawImage ? Color.white : ParseColor(block.color);
                renderer.sortingOrder = direction == "up" ? -block.sortingOrder : block.sortingOrder;

                runtimeBlocks.Add(block.id, new RuntimeBlock
                {
                    data = block,
                    container = container,
                    visual = visual,
                    renderer = renderer,
                    lastImageName = null,
                    lastVisible = false
                });
            }
        }

        if (currentAnimation != null)
        {
            currentAnimationDirection = FindAnimationDirection(currentAnimation, direction);
            if (currentAnimationDirection != null)
            {
                ApplyAnimationPose(currentFrame);
                ApplyBlockStates(currentFrame);
            }
        }
    }

    private void DestroyGeneratedRig()
    {
        if (rigRoot != null)
        {
            if (Application.isPlaying) Destroy(rigRoot.gameObject);
            else DestroyImmediate(rigRoot.gameObject);
            rigRoot = null;
        }
        runtimeBones.Clear();
        runtimeBlocks.Clear();
        evaluatedBoneLengths.Clear();
    }

    private void RestoreRootRenderer()
    {
        if (rootSpriteRenderer != null)
            rootSpriteRenderer.enabled = rootRendererWasEnabled;
    }

    private void UpdateBlockTransforms()
    {
        if (currentRigData?.blocks == null) return;

        // Group theo Bone và sắp xếp giống thuật toán segs() của Bone Animator Studio.
        Dictionary<int, List<UnityBlockData>> groups = new Dictionary<int, List<UnityBlockData>>();
        foreach (UnityBlockData block in currentRigData.blocks)
        {
            if (block == null || block.boneId == 0) continue;
            if (!groups.TryGetValue(block.boneId, out List<UnityBlockData> list))
                groups[block.boneId] = list = new List<UnityBlockData>();
            list.Add(block);
        }

        Dictionary<int, SegmentInfo> segmentByBlock = new Dictionary<int, SegmentInfo>();
        foreach (KeyValuePair<int, List<UnityBlockData>> pair in groups)
        {
            if (!runtimeBones.TryGetValue(pair.Key, out RuntimeBone bone)) continue;
            List<UnityBlockData> list = pair.Value;
            list.Sort((a, b) => a.start.CompareTo(b.start));
            float totalWeight = 0f;
            foreach (UnityBlockData block in list) totalWeight += Mathf.Max(0.1f, block.rank - 1f + 0.1f);
            if (totalWeight <= 0f) totalWeight = 1f;

            float added = 0f;
            float baseBoneLength = Mathf.Max(0f, bone.data.length);
            float currentLength = evaluatedBoneLengths.TryGetValue(pair.Key, out float value) ? value : baseBoneLength;
            float stretch = currentLength - baseBoneLength;

            foreach (UnityBlockData block in list)
            {
                float baseLength = Mathf.Max(0f, (block.end - block.start) * baseBoneLength);
                float weight = Mathf.Max(0.1f, block.rank - 1f + 0.1f);
                float extra = stretch * weight / totalWeight;
                float length = Mathf.Max(0f, baseLength + extra);
                float start = block.start * baseBoneLength + added;
                segmentByBlock[block.id] = new SegmentInfo
                {
                    baseLength = baseLength,
                    length = length,
                    start = start,
                    xScale = (baseLength > 0.0001f ? length / baseLength : 1f) * Mathf.Max(0.001f, block.scaleX)
                };
                added += extra;
            }
        }

        foreach (RuntimeBlock runtime in runtimeBlocks.Values)
        {
            UnityBlockData block = runtime.data;
            if (block.boneId == 0)
            {
                runtime.container.localPosition = new Vector3(block.x / PixelsPerUnit, -block.y / PixelsPerUnit, 0f);
                runtime.container.localRotation = Quaternion.Euler(0f, 0f, -block.angle);
                runtime.container.localScale = new Vector3(block.scaleX, block.scaleY, 1f);
                if (block.drawImage)
                {
                    runtime.visual.localPosition = new Vector3(block.imageOffsetX / PixelsPerUnit, -block.imageOffsetY / PixelsPerUnit, 0f);
                    runtime.visual.localRotation = Quaternion.Euler(0f, 0f, -block.imageRotation);
                    runtime.visual.localScale = new Vector3(block.flipX ? -block.imageScale : block.imageScale, block.imageScale, 1f);
                }
                else
                {
                    runtime.visual.localPosition = Vector3.zero;
                    runtime.visual.localRotation = Quaternion.identity;
                    runtime.visual.localScale = new Vector3(block.freeWidth / PixelsPerUnit, block.height / PixelsPerUnit, 1f);
                }
            }
            else if (segmentByBlock.TryGetValue(block.id, out SegmentInfo segment))
            {
                runtime.container.localPosition = new Vector3((segment.start + block.offsetX) / PixelsPerUnit, -block.offsetY / PixelsPerUnit, 0f);
                runtime.container.localRotation = Quaternion.identity;
                runtime.container.localScale = new Vector3(segment.xScale, block.scaleY, 1f);
                if (block.drawImage)
                {
                    runtime.visual.localPosition = new Vector3((segment.baseLength * 0.5f + block.imageOffsetX) / PixelsPerUnit, -block.imageOffsetY / PixelsPerUnit, 0f);
                    runtime.visual.localRotation = Quaternion.Euler(0f, 0f, -block.imageRotation);
                    runtime.visual.localScale = new Vector3(block.flipX ? -block.imageScale : block.imageScale, block.imageScale, 1f);
                }
                else
                {
                    runtime.visual.localPosition = new Vector3(segment.baseLength * 0.5f / PixelsPerUnit, 0f, 0f);
                    runtime.visual.localRotation = Quaternion.identity;
                    runtime.visual.localScale = new Vector3(segment.baseLength / PixelsPerUnit, block.height / PixelsPerUnit, 1f);
                }
            }

            if (runtime.renderer != null)
                runtime.renderer.sortingOrder = currentDirection == "up" ? -block.sortingOrder : block.sortingOrder;
        }
    }

    private struct SegmentInfo
    {
        public float baseLength;
        public float length;
        public float start;
        public float xScale;
    }

    #endregion

    #region Continuous bone interpolation and block visibility

    private void ApplyAnimationPose(float frame)
    {
        if (currentAnimationDirection == null) return;

        Dictionary<int, UnityBoneKeyframeData> poses = new Dictionary<int, UnityBoneKeyframeData>();
        if (currentAnimationDirection.boneTracks != null)
        {
            foreach (UnityBoneTrackData track in currentAnimationDirection.boneTracks)
            {
                if (track == null || !runtimeBones.TryGetValue(track.boneId, out RuntimeBone runtime))
                    continue;
                poses[track.boneId] = EvaluateBoneTrack(track.keyframes, frame, runtime.data);
            }
        }

        // Tất cả chiều dài được cập nhật trước, vì child Bone bắt đầu tại tip của parent Bone.
        foreach (RuntimeBone runtime in runtimeBones.Values)
        {
            UnityBoneKeyframeData pose = poses.TryGetValue(runtime.data.id, out UnityBoneKeyframeData found)
                ? found
                : new UnityBoneKeyframeData { x = runtime.data.x, y = runtime.data.y, angle = runtime.data.angle, length = runtime.data.length };
            evaluatedBoneLengths[runtime.data.id] = Mathf.Max(0f, pose.length);
        }

        foreach (RuntimeBone runtime in runtimeBones.Values)
        {
            UnityBoneKeyframeData pose = poses.TryGetValue(runtime.data.id, out UnityBoneKeyframeData found)
                ? found
                : new UnityBoneKeyframeData { x = runtime.data.x, y = runtime.data.y, angle = runtime.data.angle, length = runtime.data.length };
            float parentLength = 0f;
            if (runtime.data.parentId != 0 && evaluatedBoneLengths.TryGetValue(runtime.data.parentId, out float length))
                parentLength = length;
            runtime.transform.localPosition = new Vector3((parentLength + pose.x) / PixelsPerUnit, -pose.y / PixelsPerUnit, 0f);
            runtime.transform.localRotation = Quaternion.Euler(0f, 0f, -pose.angle);
        }

        UpdateBlockTransforms();
    }

    private static UnityBoneKeyframeData EvaluateBoneTrack(UnityBoneKeyframeData[] keys, float frame, UnityBoneData fallback)
    {
        if (keys == null || keys.Length == 0)
        {
            return new UnityBoneKeyframeData
            {
                frame = 0f,
                x = fallback.x,
                y = fallback.y,
                angle = fallback.angle,
                length = fallback.length
            };
        }

        if (keys.Length == 1 || frame <= keys[0].frame)
            return keys[0];
        int last = keys.Length - 1;
        if (frame >= keys[last].frame)
            return keys[last];

        int low = 0;
        int high = last;
        while (high - low > 1)
        {
            int mid = (low + high) >> 1;
            if (keys[mid].frame <= frame) low = mid;
            else high = mid;
        }

        UnityBoneKeyframeData a = keys[low];
        UnityBoneKeyframeData b = keys[high];
        float span = Mathf.Max(0.0001f, b.frame - a.frame);
        float t = Mathf.Clamp01((frame - a.frame) / span);
        t = t * t * (3f - 2f * t); // SmoothStep: liên tục, tránh chuyển động gãy tại keyframe.
        return new UnityBoneKeyframeData
        {
            frame = frame,
            x = Mathf.LerpUnclamped(a.x, b.x, t),
            y = Mathf.LerpUnclamped(a.y, b.y, t),
            angle = a.angle + Mathf.DeltaAngle(a.angle, b.angle) * t,
            length = Mathf.LerpUnclamped(a.length, b.length, t)
        };
    }

    private void ApplyBlockStates(float frame)
    {
        if (currentAnimationDirection == null || runtimeBlocks.Count == 0) return;

        foreach (RuntimeBlock runtime in runtimeBlocks.Values)
        {
            UnityBlockData block = runtime.data;
            bool visible = block.visible && IsBlockVisibleInDirection(block, currentDirection);
            string imageName = GetBaseImageName(block, currentDirection);

            UnityBlockKeyframeData[] keyframes = currentAnimationDirection.blockKeyframes;
            if (keyframes != null)
            {
                for (int i = 0; i < keyframes.Length; i++)
                {
                    UnityBlockKeyframeData key = keyframes[i];
                    if (key == null || key.frame > frame) continue;
                    UnityBlockStateData[] states = key.states;
                    if (states == null) continue;
                    for (int s = 0; s < states.Length; s++)
                    {
                        UnityBlockStateData state = states[s];
                        if (state == null || state.blockId != block.id) continue;
                        visible = state.visible && IsBlockVisibleInDirection(block, currentDirection);
                        imageName = string.IsNullOrEmpty(state.imageName) ? GetBaseImageName(block, currentDirection) : state.imageName;
                    }
                }
            }

            visible = visible && IsBlockVisibleInGroup(block.id);
            if (runtime.renderer == null) continue;
            if (!block.drawImage)
            {
                runtime.renderer.sprite = GetWhitePixelSprite();
                runtime.lastImageName = "__solid_color__";
            }
            else if (!string.Equals(runtime.lastImageName, imageName, StringComparison.Ordinal))
            {
                runtime.renderer.sprite = GetSprite(imageName);
                runtime.lastImageName = imageName;
                if (runtime.renderer.sprite == null)
                    Warn("block-sprite-null:" + block.id + ":" + currentDirection, $"Block '{block.name}' không có Sprite hợp lệ để render ở hướng '{currentDirection}'.");
            }
            bool enable = visible && runtime.renderer.sprite != null;
            if (runtime.lastVisible != visible || runtime.renderer.enabled != enable)
            {
                runtime.renderer.enabled = enable;
                runtime.lastVisible = visible;
            }
        }
    }

    private static bool IsBlockVisibleInDirection(UnityBlockData block, string direction)
    {
        if (block.visibility == "front" && direction == "up") return false;
        if (block.visibility == "back" && direction != "up") return false;
        return true;
    }

    private static string GetBaseImageName(UnityBlockData block, string direction)
    {
        switch (direction)
        {
            case "up": return !string.IsNullOrEmpty(block.imageUp) ? block.imageUp : block.imageDown;
            case "left": return !string.IsNullOrEmpty(block.imageLeft) ? block.imageLeft : block.imageDown;
            case "right": return !string.IsNullOrEmpty(block.imageRight) ? block.imageRight : block.imageDown;
            default: return block.imageDown;
        }
    }

    #endregion

    #region Sound phases

    private CharacterAnimationAudioBinding FindAudioBinding(string animationName)
    {
        for (int i = 0; i < audioBindings.Count; i++)
        {
            CharacterAnimationAudioBinding binding = audioBindings[i];
            if (binding != null && string.Equals(binding.animationName, animationName, StringComparison.Ordinal))
                return binding;
        }
        return null;
    }

    private void PlayAnimationStartSound(UnityAnimationData animation)
    {
        CharacterAnimationAudioBinding binding = FindAudioBinding(animation.name);
        if (binding == null)
        {
            Warn("audio-binding:" + animation.name, $"Animation '{animation.name}' chưa có cấu hình âm thanh trong Inspector.");
            return;
        }

        if (!animation.isCombat)
        {
            PlaySfx(binding.sfxId, animation.name, "SFX");
            return;
        }

        if (binding.castStartFrame < 0 || binding.castEndFrame < binding.castStartFrame
            || currentAnimationDirection == null || binding.castEndFrame >= currentAnimationDirection.frameCount)
        {
            Warn("invalid-audio-range:" + animation.name,
                $"Animation '{animation.name}' có Cast Start/End không hợp lệ ({binding.castStartFrame}–{binding.castEndFrame}); không phát SFX theo pha.");
            return;
        }

        if (binding.castStartFrame > 0)
            PlaySfx(binding.chargeSfxId, animation.name, "Charge");
        else
            PlaySfx(binding.castSfxId, animation.name, "Cast");
    }

    private void TriggerCombatPhaseCrossings(float previousFrame, float nextFrame, bool wrapped)
    {
        if (currentAnimation == null || !currentAnimation.isCombat) return;
        CharacterAnimationAudioBinding binding = FindAudioBinding(currentAnimation.name);
        if (binding == null) return;

        if (binding.castStartFrame < 0 || binding.castEndFrame < binding.castStartFrame
            || binding.castEndFrame >= currentAnimationDirection.frameCount)
        {
            Warn("invalid-audio-range:" + currentAnimation.name,
                $"Animation '{currentAnimation.name}' có Cast Start/End không hợp lệ ({binding.castStartFrame}–{binding.castEndFrame}); phạm vi hợp lệ là 0–{currentAnimationDirection.frameCount - 1}.");
            return;
        }

        float castStart = binding.castStartFrame;
        float endStart = binding.castEndFrame + 1f;
        if (wrapped)
        {
            // Mỗi vòng loop bắt đầu lại pha charge/cast. Xử lý thêm trường hợp frame nhảy qua nhiều pha trong một Update.
            if (castStart > 0f)
                PlaySfx(binding.chargeSfxId, currentAnimation.name, "Charge (loop)");
            else
                PlaySfx(binding.castSfxId, currentAnimation.name, "Cast (loop)");

            if (castStart > 0f && nextFrame >= castStart)
                PlaySfx(binding.castSfxId, currentAnimation.name, "Cast");
            if (endStart > 0f && nextFrame >= endStart)
                PlaySfx(binding.endSfxId, currentAnimation.name, "End");
            return;
        }

        if (castStart > 0f && previousFrame < castStart && nextFrame >= castStart)
            PlaySfx(binding.castSfxId, currentAnimation.name, "Cast");
        if (endStart > 0f && previousFrame < endStart && nextFrame >= endStart)
            PlaySfx(binding.endSfxId, currentAnimation.name, "End");
    }

    private void PlaySfx(string sfxId, string animationName, string phase)
    {
        if (string.IsNullOrWhiteSpace(sfxId))
        {
            Warn("empty-sfx:" + animationName + ":" + phase,
                $"Animation '{animationName}' không có SFX ID cho pha {phase}. Hãy chọn ID bằng RegistryId trong Inspector.");
            return;
        }

        if (!VolumeManager.IsReady)
        {
            Warn("volume-manager:" + animationName + ":" + phase,
                $"Không thể phát SFX '{sfxId}' cho animation '{animationName}' ({phase}) vì VolumeManager chưa sẵn sàng.");
            return;
        }

        VolumeManager.PlaySFX(sfxId);
    }

    #endregion

    #region Sprite assets and inspector audio registry

    private Sprite GetSprite(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;
        if (runtimeSprites.TryGetValue(assetName, out Sprite cached))
            return cached;

        UnityAssetData asset = FindAsset(assetName);
        if (asset == null || string.IsNullOrWhiteSpace(asset.dataUrl))
        {
            Warn("missing-asset:" + assetName, $"Không tìm thấy asset '{assetName}' trong Unity JSON.");
            runtimeSprites[assetName] = null;
            return null;
        }

        try
        {
            string encoded = asset.dataUrl;
            int comma = encoded.IndexOf(',');
            if (comma >= 0) encoded = encoded.Substring(comma + 1);
            byte[] bytes = Convert.FromBase64String(encoded);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Destroy(texture);
                Warn("decode-asset:" + assetName, $"Không giải mã được ảnh PNG '{assetName}'.");
                runtimeSprites[assetName] = null;
                return null;
            }

            texture.name = "Texture_" + assetName;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            runtimeTextures[assetName] = texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "Sprite_" + assetName;
            runtimeSprites[assetName] = sprite;
            return sprite;
        }
        catch (Exception exception)
        {
            Warn("decode-exception:" + assetName, $"Lỗi đọc asset '{assetName}': {exception.Message}");
            runtimeSprites[assetName] = null;
            return null;
        }
    }

    private Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null) return whitePixelSprite;
        Texture2D texture = Texture2D.whiteTexture;
        whitePixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        whitePixelSprite.name = "CharacterAnimation_WhitePixel";
        return whitePixelSprite;
    }

    private UnityAssetData FindAsset(string assetName)
    {
        if (data?.assets == null) return null;
        for (int i = 0; i < data.assets.Length; i++)
            if (data.assets[i] != null && string.Equals(data.assets[i].name, assetName, StringComparison.Ordinal))
                return data.assets[i];
        return null;
    }

    private static Color ParseColor(string value)
    {
        if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out Color color))
            return color;
        return Color.white;
    }

    private void SyncAudioBindingsFromJson(bool editorValidation)
    {
        if (audioBindings == null)
            audioBindings = new List<CharacterAnimationAudioBinding>();
        if (string.IsNullOrWhiteSpace(unityJson))
            return;

        // Chỉ hiện danh sách Animation/SFX khi toàn bộ JSON hợp lệ, không chỉ parse được phần header.
        if (editorValidation && !editorJsonValid)
            return;

        try
        {
            UnityAnimationHeaderRoot header = JsonUtility.FromJson<UnityAnimationHeaderRoot>(unityJson);
            if (header?.animations == null)
                return;

            Dictionary<string, CharacterAnimationAudioBinding> existing = new Dictionary<string, CharacterAnimationAudioBinding>(StringComparer.Ordinal);
            foreach (CharacterAnimationAudioBinding binding in audioBindings)
                if (binding != null && !string.IsNullOrWhiteSpace(binding.animationName))
                    existing[binding.animationName] = binding;

            List<CharacterAnimationAudioBinding> synced = new List<CharacterAnimationAudioBinding>(header.animations.Length);
            foreach (UnityAnimationHeader animation in header.animations)
            {
                if (animation == null || string.IsNullOrWhiteSpace(animation.name)) continue;
                if (!existing.TryGetValue(animation.name, out CharacterAnimationAudioBinding binding))
                {
                    binding = new CharacterAnimationAudioBinding
                    {
                        animationName = animation.name,
                        castStartFrame = animation.castStartFrame,
                        castEndFrame = animation.castEndFrame
                    };
                }
                binding.isCombat = animation.isCombat;
                synced.Add(binding);
            }

            bool changed = synced.Count != audioBindings.Count;
            if (!changed)
            {
                for (int i = 0; i < synced.Count; i++)
                {
                    if (!ReferenceEquals(synced[i], audioBindings[i])) { changed = true; break; }
                }
            }
            if (changed || audioBindings.Count == 0)
                audioBindings = synced;
        }
        catch (Exception exception)
        {
            if (editorValidation)
                Warn("inspector-json:" + exception.Message, "Chưa đọc được danh sách animation từ Unity JSON: " + exception.Message);
        }
    }

    #endregion

    #region Diagnostics

    private void Warn(string key, string message)
    {
        if (!logAnimationWarnings || !warningOnce.Add(key)) return;
        Debug.LogWarning("[CharacterAnimation] " + message, this);
    }

    #endregion

    #region JSON DTOs (JsonUtility-compatible arrays only)

    [Serializable] private sealed class UnityCharacterAnimationData
    {
        public string format;
        public int schemaVersion;
        public string modelName;
        public float pixelsPerUnit;
        public string coordinateSystem;
        public UnityDirectionData[] directions;
        public UnityAnimationData[] animations;
        public UnityAssetData[] assets;
        public UnityGroupBlockData[] groupBlocks;
    }

    [Serializable] private sealed class UnityGroupBlockHeaderRoot
    {
        public UnityGroupBlockData[] groupBlocks;
    }

    [Serializable] private sealed class UnityGroupBlockData
    {
        public string name;
        public string defaultVariant;
        public UnityGroupVariantData[] variants;
    }

    [Serializable] private sealed class UnityGroupVariantData
    {
        public string name;
        public int[] blockIds;
    }

    [Serializable] private sealed class UnityDirectionData
    {
        public string name;
        public UnityBoneData[] bones;
        public UnityBlockData[] blocks;
    }

    [Serializable] private sealed class UnityBoneData
    {
        public int id;
        public string name;
        public int parentId;
        public float x;
        public float y;
        public float angle;
        public float length;
    }

    [Serializable] private sealed class UnityBlockData
    {
        public int id;
        public string name;
        public int boneId;
        public float x;
        public float y;
        public float angle;
        public float freeWidth;
        public float start;
        public float end;
        public float height;
        public float offsetX;
        public float offsetY;
        public float rank;
        public int sortingOrder;
        public string visibility;
        public bool visible;
        public bool drawImage;
        public string imageDown;
        public string imageUp;
        public string imageLeft;
        public string imageRight;
        public float imageRotation;
        public float imageScale;
        public float imageOffsetX;
        public float imageOffsetY;
        public bool flipX;
        public float scaleX;
        public float scaleY;
        public string color;
    }

    [Serializable] private sealed class UnityAnimationData
    {
        public string name;
        public bool loop;
        public bool isCombat;
        public int castStartFrame;
        public int castEndFrame;
        public int frameCount;
        public UnityAnimationDirectionData[] directions;
    }

    [Serializable] private sealed class UnityAnimationDirectionData
    {
        public string name;
        public float frameRate;
        public int frameCount;
        public UnityBoneTrackData[] boneTracks;
        public UnityBlockKeyframeData[] blockKeyframes;
    }

    [Serializable] private sealed class UnityBoneTrackData
    {
        public int boneId;
        public UnityBoneKeyframeData[] keyframes;
    }

    [Serializable] private sealed class UnityBoneKeyframeData
    {
        public float frame;
        public float x;
        public float y;
        public float angle;
        public float length;
    }

    [Serializable] private sealed class UnityBlockKeyframeData
    {
        public float frame;
        public UnityBlockStateData[] states;
    }

    [Serializable] private sealed class UnityBlockStateData
    {
        public int blockId;
        public bool visible;
        public string imageName;
    }

    [Serializable] private sealed class UnityAssetData
    {
        public string name;
        public string dataUrl;
    }

    [Serializable] private sealed class UnityAnimationHeaderRoot
    {
        public UnityAnimationHeader[] animations;
    }

    [Serializable] private sealed class UnityAnimationHeader
    {
        public string name;
        public bool isCombat;
        public int castStartFrame;
        public int castEndFrame;
    }

    #endregion
}
