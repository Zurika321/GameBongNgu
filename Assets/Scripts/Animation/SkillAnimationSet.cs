using UnityEngine;

[System.Serializable]
public class SkillAnimationSet
{
    [Header("Charge anim")]
    public Sprite[] chargeDown;
    public Sprite[] chargeUp;
    public Sprite[] chargeLeft;
    public Sprite[] chargeRight;

    [Header("Attack anim")]
    public Sprite[] attackDown;
    public Sprite[] attackUp;
    public Sprite[] attackLeft;
    public Sprite[] attackRight;

    [Header("End anim")]
    public Sprite[] endDown;
    public Sprite[] endUp;
    public Sprite[] endLeft;
    public Sprite[] endRight;

    public Sprite[] GetChargeFrames(Vector2 direction)
    {
        return GetDirectionalFrames(direction, chargeDown, chargeUp, chargeLeft, chargeRight);
    }

    public Sprite[] GetAttackFrames(Vector2 direction)
    {
        return GetDirectionalFrames(direction, attackDown, attackUp, attackLeft, attackRight);
    }

    public Sprite[] GetEndFrames(Vector2 direction)
    {
        return GetDirectionalFrames(direction, endDown, endUp, endLeft, endRight);
    }

    private static Sprite[] GetDirectionalFrames(
        Vector2 direction,
        Sprite[] down,
        Sprite[] up,
        Sprite[] left,
        Sprite[] right)
    {
        if (direction.sqrMagnitude < 0.001f)
            return down != null && down.Length > 0 ? down : up != null && up.Length > 0 ? up : left != null && left.Length > 0 ? left : right != null && right.Length > 0 ? right : System.Array.Empty<Sprite>();

        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
        {
            if (direction.y >= 0f)
                return up != null && up.Length > 0 ? up : down != null && down.Length > 0 ? down : right != null && right.Length > 0 ? right : left != null && left.Length > 0 ? left : System.Array.Empty<Sprite>();

            return down != null && down.Length > 0 ? down : up != null && up.Length > 0 ? up : right != null && right.Length > 0 ? right : left != null && left.Length > 0 ? left : System.Array.Empty<Sprite>();
        }

        if (direction.x >= 0f)
            return right != null && right.Length > 0 ? right : left != null && left.Length > 0 ? left : up != null && up.Length > 0 ? up : down != null && down.Length > 0 ? down : System.Array.Empty<Sprite>();

        return left != null && left.Length > 0 ? left : right != null && right.Length > 0 ? right : up != null && up.Length > 0 ? up : down != null && down.Length > 0 ? down : System.Array.Empty<Sprite>();
    }
}