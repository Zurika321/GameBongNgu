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
    public float frameRate = 10f;
    public bool loop = true;
}

/// <summary>
/// Component animation DÙNG CHUNG cho mọi entity (Player, Enemy, Boss...) — không còn tách riêng
/// PlayerAnimation/EnemyAnimation như trước, cũng không cần SkillAnimation riêng.
///
/// Controller gọi Play(state) khi đổi trạng thái di chuyển (Idle/Run/Die...).
/// Skill (ArcAttackSkill, FlyAttackSkill...) có thể gọi THẲNG Play(AnimState.Skill) hoặc
/// PlayCustom(sprites riêng của skill đó) khi tung chiêu — không cần đi qua Controller.
///
/// Gắn component này lên cùng GameObject có SpriteRenderer (Player gốc, Enemy gốc...).
/// </summary>
public class CharacterAnimation : MonoBehaviour
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
    private float frameRate = 10f;
    private bool loop = true;
    private int frameIndex;
    private float frameTimer;
    private bool playingCustom;

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

        frameTimer += Time.deltaTime;
        float frameDuration = frameRate > 0f ? 1f / frameRate : 0.1f;

        if (frameTimer < frameDuration) return;

        frameTimer = 0f;
        frameIndex++;

        if (frameIndex >= currentFrames.Length)
        {
            frameIndex = loop ? 0 : currentFrames.Length - 1;
        }

        spriteRenderer.sprite = currentFrames[frameIndex];
    }

    /// <summary>Phát animation theo state đã đăng ký sẵn trong mảng clips (Inspector).</summary>
    public void Play(AnimState state)
    {
        AnimClip clip = clips == null ? null : System.Array.Find(clips, c => c != null && c.state == state);
        if (clip == null)
        {
            Debug.LogWarning($"[CharacterAnimation] '{name}' chưa gán frames cho state {state}.");
            return;
        }

        Sprite[] frames = GetFrames(clip, facingDirection, out bool flipX);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning(
                $"[CharacterAnimation] '{name}' chưa gán frames cho state {state}, hướng {facingDirection}.");
            return;
        }

        if (spriteRenderer == null) return;

        if (currentState == state && !playingCustom && currentFrames == frames)
        {
            spriteRenderer.flipX = flipX;
            return;
        }

        currentState = state;
        playingCustom = false;
        spriteRenderer.flipX = flipX;
        SetFrames(frames, clip.frameRate, clip.loop);
    }

    /// <summary>
    /// Skill tự truyền thẳng mảng sprite riêng của mình (animation skill không nằm trong clips mặc định).
    /// Dùng khi skill muốn hiển thị animation riêng biệt, không thuộc state chuẩn (Idle/Run/Die...).
    /// Gọi lại Play(AnimState...) sau khi skill kết thúc để quay về animation bình thường.
    /// </summary>
    public void PlayCustom(Sprite[] frames, float customFrameRate = 10f, bool customLoop = false)
    {
        if (frames == null || frames.Length == 0) return;

        playingCustom = true;
        SetFrames(frames, customFrameRate, customLoop);
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

    private static Sprite[] GetFrames(AnimClip clip, FacingDirection direction, out bool flipX)
    {
        flipX = false;
        switch (direction)
        {
            case FacingDirection.Up:
                return FirstAvailable(clip.upFrames, clip.downFrames, clip.rightFrames, clip.leftFrames);
            case FacingDirection.Right:
                return FirstAvailable(clip.rightFrames, clip.downFrames, clip.upFrames, clip.leftFrames);
            case FacingDirection.Left:
                if (HasFrames(clip.leftFrames)) return clip.leftFrames;
                if (HasFrames(clip.rightFrames))
                {
                    flipX = true;
                    return clip.rightFrames;
                }
                return FirstAvailable(clip.downFrames, clip.upFrames);
            default:
                return FirstAvailable(clip.downFrames, clip.upFrames, clip.rightFrames, clip.leftFrames);
        }
    }

    private static Sprite[] FirstAvailable(params Sprite[][] candidates)
    {
        foreach (Sprite[] frames in candidates)
        {
            if (HasFrames(frames)) return frames;
        }

        return null;
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private void SetFrames(Sprite[] frames, float newFrameRate, bool newLoop)
    {
        currentFrames = frames;
        frameRate = newFrameRate;
        loop = newLoop;
        frameIndex = 0;
        frameTimer = 0f;
        if (spriteRenderer != null) spriteRenderer.sprite = frames[0];
    }
}
