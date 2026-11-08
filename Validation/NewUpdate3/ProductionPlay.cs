using UnityAnimationData = AnimationHarness.Animation;
public partial class AnimationHarness {
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

}
