using System;
using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHP = 100f;
    public float defence = 10f;
    public float meleeDamage = 15f;

    public float CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    public event Action<float> OnDamageTaken;
    public event Action OnDeath;

    private void Awake()
    {
        CurrentHP = maxHP;
    }

    /// Lets an AI script apply ScriptableObject values after all Awakes have run.
    public void Initialize(float newMaxHP, float newDefence)
    {
        maxHP     = newMaxHP;
        defence   = newDefence;
        CurrentHP = maxHP;
    }

    public void TakeDamage(float rawAmount, float damageReduction = 0f)
    {
        if (IsDead)
            return;

        float effective = Mathf.Max(0f, rawAmount - defence - damageReduction);
        CurrentHP = Mathf.Max(0f, CurrentHP - effective);
        OnDamageTaken?.Invoke(effective);

        if (CurrentHP <= 0f)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
    }
}
