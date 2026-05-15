using UnityEngine;
using UnityEngine.AI;

/// Shared foundation for all enemies in Abyssfall.
/// Handles: component refs, player lookup, CrossFade, NavMesh helpers,
/// FacePlayer, CanSeePlayer, DealDamageToPlayer, and EnemyStats event wiring.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public abstract class EnemyBase : MonoBehaviour
{
    // ─── Inspector (visible in every subclass) ────────────────────────

    [Header("Animation")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected float crossFadeTime = 0.15f;

    [Header("Line of Sight")]
    [Tooltip("Layers that physically block the enemy's line of sight. Leave empty to skip LOS check.")]
    [SerializeField] protected LayerMask sightBlockingMask;

    // ─── Shared component refs ────────────────────────────────────────

    protected NavMeshAgent   agent;
    protected EnemyStats     stats;
    protected Transform      player;
    protected PlayerStats    playerStats;
    protected PlayerMovement playerMovement;

    // ─── Animation ────────────────────────────────────────────────────

    private int currentAnimHash = -1;

    // ─── Unity ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        agent.updateRotation = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            player         = pm.transform;
            playerStats    = pm.GetComponent<PlayerStats>()
                          ?? pm.GetComponentInChildren<PlayerStats>();
            playerMovement = pm;
        }

        stats.OnDamageTaken += HandleDamageTaken;
        stats.OnDeath       += HandleDeath;
    }

    protected virtual void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnDamageTaken -= HandleDamageTaken;
            stats.OnDeath       -= HandleDeath;
        }
    }

    // ─── Abstract contract ────────────────────────────────────────────

    protected abstract void HandleDamageTaken(float amount);
    protected abstract void HandleDeath();

    // ─── Animation helpers ────────────────────────────────────────────

    /// Cross-fades to an animator state only when the state changes,
    /// preventing per-frame restarts.
    protected void CrossFade(int hash)
    {
        if (animator == null || hash == currentAnimHash) return;
        currentAnimHash = hash;
        animator.CrossFade(hash, crossFadeTime, 0);
    }

    /// Call after a forced state change (e.g. TakeDamage) so the next
    /// CrossFade always fires even if the hash hasn't changed.
    protected void ResetAnimHash() => currentAnimHash = -1;

    // ─── NavMesh helpers ──────────────────────────────────────────────

    protected void AgentStop()
    {
        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    protected void AgentMove(float speed, Vector3 destination)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.speed     = speed;
        agent.SetDestination(destination);
    }

    // ─── Spatial helpers ──────────────────────────────────────────────

    protected void FacePlayer(float rotSpeed = 10f)
    {
        if (player == null) return;
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), rotSpeed * Time.deltaTime);
    }

    protected float DistToPlayer() =>
        player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

    /// Returns true when player is within range, inside the FOV cone,
    /// and not blocked by sightBlockingMask geometry (if mask is non-zero).
    protected bool CanSeePlayer(float range, float fov)
    {
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        float   dist     = toPlayer.magnitude;

        if (dist > range) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > fov * 0.5f) return false;

        if (sightBlockingMask != 0)
        {
            Vector3 origin = transform.position + Vector3.up;
            if (Physics.Raycast(origin, toPlayer.normalized, dist, sightBlockingMask))
                return false;
        }

        return true;
    }

    // ─── Combat helper ────────────────────────────────────────────────

    protected void DealDamageToPlayer(float amount)
    {
        if (playerStats != null)
            playerStats.TakeDamage(amount);
    }
}
