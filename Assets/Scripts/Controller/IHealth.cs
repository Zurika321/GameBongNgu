public interface IHealth
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
    bool IsInvincible { get; }

    void SetInvincible(bool invincible);
}