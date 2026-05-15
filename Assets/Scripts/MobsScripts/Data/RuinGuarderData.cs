using UnityEngine;

[CreateAssetMenu(fileName = "RuinGuarderData", menuName = "Abyssfall/Enemy Data/RuinGuarder")]
public class RuinGuarderData : ScriptableObject
{
    [Header("Stats")]
    public float maxHP = 100f;
    public float defence = 10f;

    [Header("Movement")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 5f;
    public float waypointTolerance = 0.5f;
    public float waypointIdleTime = 2f;

    [Header("Detection")]
    public float detectionRange = 10f;
    public float fieldOfView = 90f;
    public float chaseRange = 15f;
    public float alertDelay = 1f;

    [Header("Combat")]
    public float attackRange = 2f;
    public float lightDamage = 10f;
    public float lightCooldown = 1.5f;
    public float lightHitDelay = 0.4f;
    public float lightHitRadius = 1.5f;
    
    public float heavyDamage = 25f;
    public float heavyCooldown = 3f;
    public float heavyHitDelay = 0.8f;
    public float heavyHitRadius = 2f;
    public float heavyAttackChance = 0.3f;

    public float attackForwardOffset = 1f;

    [Header("Block")]
    public float blockChance = 0.2f;
    public float blockDamageMultiplier = 0.5f;

    [Header("Social")]
    public float helpCallRadius = 15f;
    public float helpCallHPThreshold = 0.5f;
}
