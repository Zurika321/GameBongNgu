// Archived sprite animation implementation. Disabled while testing the JSON rig.
#if false
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// State animation chuẩn — dùng chung cho mọi entity, không phân biệt Player/Enemy.
/// </summary>
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
/// 1 clip animation = 1 mảng sprite cho 1 state cụ thể. Kéo thả sprite trực tiếp trong Inspector.
/// </summary>
[System.Serializable]
public class AnimClip
{
    public AnimState state;
    [FormerlySerializedAs("frames")]
    public Sprite[] downFrames;
    public Sprite[] upFrames;
    public Sprite[] rightFrames;
    public Sprite[] leftFrames;
    public float[] downFrameDurations;
    public float[] upFrameDurations;
    public float[] rightFrameDurations;
    public float[] leftFrameDurations;
    public float frameRate = 10f;
    public bool loop = true;
}

/// <summary>
/// Component animation DÙNG CHUNG cho mọi entity (Player, Enemy, Boss...) — không còn tách riêng
/// PlayerAnimation/EnemyAnimation như trước, cũng không cần SkillAnimation riêng.
///
/// Controller gọi Play(state) khi đổi trạng thái di chuyển (Idle/Run/Die...).
/// Skill (StaticDamageZone, MovingHitbox...) có thể gọi THẲNG Play(AnimState.Skill) hoặc
/// PlayCustom(sprites riêng của skill đó) khi tung chiêu — không cần đi qua Controller.
///
/// Gắn component này lên cùng GameObject có SpriteRenderer (Player gốc, Enemy gốc...).
/// </summary>
public class CharacterAnimation_old : MonoBehaviour
{
    private enum FacingDirection
    {
        Down,
        Up,
        Right,
        Left
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AnimClip[] clips;

    private AnimState currentState = AnimState.Idle;
    private FacingDirection facingDirection = FacingDirection.Down;
    private Sprite[] currentFrames;
    private float[] currentFrameDurations;
    private float frameRate = 10f;
    private bool loop = true;
    private int frameIndex;
    private float frameTimer;
    private bool playingCustom;
    private FacingDirection appliedFacingDirection;
    private bool appliedFlipX;
    private bool hasAppliedPlayback;

    public AnimState CurrentState => currentState;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            Debug.LogWarning($"[CharacterAnimation] '{name}' không có SpriteRenderer.", this);
    }

    private void Update()
    {
        if (currentFrames == null || currentFrames.Length == 0 || spriteRenderer == null) return;

        if (!loop && frameIndex >= currentFrames.Length - 1)
        {
            spriteRenderer.sprite = currentFrames[currentFrames.Length - 1];
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = GetFrameDuration(frameIndex);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;

            if (frameIndex >= currentFrames.Length)
            {
                if (loop)
                {
                    frameIndex = 0;
                }
                else
                {
                    frameIndex = currentFrames.Length - 1;
                    spriteRenderer.sprite = currentFrames[frameIndex];
                    frameTimer = 0f;
                    return;
                }
            }

            spriteRenderer.sprite = currentFrames[frameIndex];
            frameDuration = GetFrameDuration(frameIndex);
        }
    }

    /// <summary>Phát animation theo state đã đăng ký sẵn trong mảng clips (Inspector).</summary>
    public void Play(AnimState state)
    {
        if (playingCustom) return;

        AnimClip clip = clips == null ? null : System.Array.Find(clips, c => c != null && c.state == state);
        if (clip == null)
        {
            Debug.LogWarning($"[CharacterAnimation] '{name}' chưa gán frames cho state {state}.");
            return;
        }

        Sprite[] frames = GetFrames(clip, facingDirection, out float[] durations, out bool flipX);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning(
                $"[CharacterAnimation] '{name}' chưa gán frames cho state {state}, hướng {facingDirection}.");
            return;
        }

        bool sameStateAndFacing = currentState == state
            && !playingCustom
            && hasAppliedPlayback
            && appliedFacingDirection == facingDirection
            && appliedFlipX == flipX
            && ReferenceEquals(currentFrames, frames);
        if (spriteRenderer != null && sameStateAndFacing) return;
        if (spriteRenderer == null) return;

        currentState = state;
        playingCustom = false;
        spriteRenderer.flipX = flipX;
        SetFrames(frames, durations, clip.frameRate, clip.loop);
        appliedFacingDirection = facingDirection;
        appliedFlipX = flipX;
        hasAppliedPlayback = true;
    }

    /// <summary>
    /// Skill tự truyền thẳng mảng sprite riêng của mình (animation skill không nằm trong clips mặc định).
    /// Dùng khi skill muốn hiển thị animation riêng biệt, không thuộc state chuẩn (Idle/Run/Die...).
    /// Gọi lại Play(AnimState...) sau khi skill kết thúc để quay về animation bình thường.
    /// </summary>
    public void PlayCustom(Sprite[] frames, float customFrameRate = 10f, bool customLoop = false)
    {
        PlayCustom(frames, null, customFrameRate, customLoop);
    }

    public void StopCustomPlayback()
    {
        playingCustom = false;
    }

    /// <summary>Phát clip custom với thời lượng tùy chọn cho từng frame.</summary>
    public void PlayCustom(
        Sprite[] frames,
        float[] frameDurations,
        float customFrameRate = 10f,
        bool customLoop = false)
    {
        if (frames == null || frames.Length == 0) return;

        playingCustom = true;
        SetFrames(frames, frameDurations, customFrameRate, customLoop);
    }

    /// <summary>Chọn một trong bốn hướng top-down dựa trên trục di chuyển trội.</summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            facingDirection = direction.x < 0f ? FacingDirection.Left : FacingDirection.Right;
        else
            facingDirection = direction.y < 0f ? FacingDirection.Down : FacingDirection.Up;
    }

    private static Sprite[] GetFrames(
        AnimClip clip,
        FacingDirection direction,
        out float[] durations,
        out bool flipX)
    {
        durations = null;
        flipX = false;

        switch (direction)
        {
            case FacingDirection.Up:
                return FirstAvailable(clip, out durations, FacingDirection.Up, FacingDirection.Down,
                    FacingDirection.Right, FacingDirection.Left);
            case FacingDirection.Right:
                return FirstAvailable(clip, out durations, FacingDirection.Right, FacingDirection.Down,
                    FacingDirection.Up, FacingDirection.Left);
            case FacingDirection.Left:
                Sprite[] leftFrames = GetFramesForDirection(clip, FacingDirection.Left, out durations);
                if (HasFrames(leftFrames)) return leftFrames;

                Sprite[] rightFrames = GetFramesForDirection(clip, FacingDirection.Right, out durations);
                if (HasFrames(rightFrames))
                {
                    flipX = true;
                    return rightFrames;
                }

                return FirstAvailable(clip, out durations, FacingDirection.Down, FacingDirection.Up);
            default:
                return FirstAvailable(clip, out durations, FacingDirection.Down, FacingDirection.Up,
                    FacingDirection.Right, FacingDirection.Left);
        }
    }

    private static Sprite[] FirstAvailable(
        AnimClip clip,
        out float[] durations,
        params FacingDirection[] candidates)
    {
        durations = null;
        foreach (FacingDirection candidate in candidates)
        {
            Sprite[] frames = GetFramesForDirection(clip, candidate, out float[] candidateDurations);
            if (!HasFrames(frames)) continue;

            durations = candidateDurations;
            return frames;
        }

        return null;
    }

    private static Sprite[] GetFramesForDirection(AnimClip clip, FacingDirection direction, out float[] durations)
    {
        switch (direction)
        {
            case FacingDirection.Up:
                durations = clip.upFrameDurations;
                return clip.upFrames;
            case FacingDirection.Right:
                durations = clip.rightFrameDurations;
                return clip.rightFrames;
            case FacingDirection.Left:
                durations = clip.leftFrameDurations;
                return clip.leftFrames;
            default:
                durations = clip.downFrameDurations;
                return clip.downFrames;
        }
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private float GetFrameDuration(int index)
    {
        if (currentFrameDurations != null
            && index >= 0
            && index < currentFrameDurations.Length)
        {
            float duration = currentFrameDurations[index];
            if (IsFinite(duration) && duration > 0f) return duration;
        }

        return IsFinite(frameRate) && frameRate > 0f ? 1f / frameRate : 0.1f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void SetFrames(Sprite[] frames, float[] durations, float newFrameRate, bool newLoop)
    {
        currentFrames = frames;
        currentFrameDurations = durations;
        frameRate = newFrameRate;
        loop = newLoop;
        frameIndex = 0;
        frameTimer = 0f;
        if (spriteRenderer != null) spriteRenderer.sprite = frames[0];
    }
}

#endif
