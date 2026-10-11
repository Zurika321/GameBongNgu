using UnityEngine;

public interface IDamage
{
    void TakeDamage(float damage, GameObject source);
    void TakeDamage(float damage, GameObject source, float stunTime);
}