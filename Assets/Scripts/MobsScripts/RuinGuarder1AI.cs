using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class RuinGuarder1AI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float loseAggroRange = 30f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float runDistanceThreshold = 5f;

    [Header("Combat")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackHitDelay = 0.4f;
    [SerializeField] private float attackHitRadius = 1.2f;

    [Header("Block")]
    [SerializeField] private float blockIntervalMin = 5f;
    [SerializeField] private float blockIntervalMax = 10f;
    [SerializeField] private float blockDuration = 2f;
    [SerializeField] [Range(0f, 1f)] private float blockDamageMultiplier = 0.3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float crossFadeTime = 0.15f;

    private enum AIState { Idle, Chase, Attack, Block, TakeDamage, Dead }

    private NavMeshAgent agent;
    private EnemyStats stats;
    private Transform player;

    private AIState currentState = AIState.Idle;
    private float stateLockedUntil;
    private float nextAttackTime;
    private float nextBlockTime;
    private bool isBlocking;
    private bool attackHitPending;
    private float attackHitTime;
    private int nextAttackIndex;

    private int hashIdle;
    private int hashWalk;
    private int hashRun;
    private int hashAttack01;
    private int hashAttack02;
    private int hashBlock;
    private int hashTakeDamage;
    private int hashDeath;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        agent.updateRotation = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        CacheHashes();
        ScheduleNextBlock();

        stats.OnDamageTaken += HandleDamageTaken;
        stats.OnDeath += HandleDeath;

        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
            player = pm.transform;
    }

    private void Update()
    {
        if (currentState == AIState.Dead)
            return;

        if (attackHitPending && Time.time >= attackHitTime)
        {
            attackHitPending = false;
            DealMeleeDamage();
        }

        UpdateState();
    }

    private void UpdateState()
    {
        if (Time.time < stateLockedUntil)
            return;

        if (currentState == AIState.Block)
        {
            isBlocking = false;
            EnterChaseOrIdle();
            return;
        }

        if (player == null)
        {
            if (currentState != AIState.Idle)
                EnterIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > loseAggroRange)
        {
            EnterIdle();
            return;
        }

        if (currentState == AIState.Idle && dist > detectionRange)
            return;

        if (Time.time >= nextBlockTime && currentState != AIState.Attack)
        {
            EnterBlock();
            return;
        }

        if (dist <= attackRange && Time.time >= nextAttackTime)
        {
            EnterAttack();
            return;
        }

        EnterChase(dist);
    }

    private void EnterIdle()
    {
        currentState = AIState.Idle;
        AgentStop();
        CrossFade(hashIdle);
    }

    private void EnterChaseOrIdle()
    {
        if (player != null)
            EnterChase(Vector3.Distance(transform.position, player.position));
        else
            EnterIdle();
    }

    private void EnterChase(float distToPlayer)
    {
        currentState = AIState.Chase;
        bool shouldRun = distToPlayer > runDistanceThreshold;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = shouldRun ? runSpeed : walkSpeed;
            agent.SetDestination(player.position);
        }
        else
        {
            float speed = shouldRun ? runSpeed : walkSpeed;
            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0f;
            transform.position += dir * speed * Time.deltaTime;
        }

        FacePlayer();
        CrossFade(shouldRun ? hashRun : hashWalk);
    }

    private void EnterAttack()
    {
        currentState = AIState.Attack;
        AgentStop();
        FacePlayer();

        int attackHash = (nextAttackIndex % 2 == 0) ? hashAttack01 : hashAttack02;
        nextAttackIndex++;

        stateLockedUntil = Time.time + attackCooldown;
        nextAttackTime   = Time.time + attackCooldown;
        attackHitTime    = Time.time + attackHitDelay;
        attackHitPending = true;

        CrossFade(attackHash);
    }

    private void EnterBlock()
    {
        currentState = AIState.Block;
        isBlocking = true;
        AgentStop();
        stateLockedUntil = Time.time + blockDuration;
        ScheduleNextBlock();
        CrossFade(hashBlock);
    }

    private void HandleDamageTaken(float amount)
    {
        if (currentState == AIState.Dead)
            return;

        attackHitPending = false;

        if (currentState != AIState.TakeDamage)
        {
            currentState = AIState.TakeDamage;
            AgentStop();
            stateLockedUntil = Time.time + 0.7f;
            CrossFade(hashTakeDamage);
        }
    }

    private void HandleDeath()
    {
        currentState = AIState.Dead;
        attackHitPending = false;
        AgentStop();
        agent.enabled = false;
        CrossFade(hashDeath);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }

    private void DealMeleeDamage()
    {
        if (player == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * (attackRange * 0.5f) + Vector3.up,
            attackHitRadius
        );

        foreach (Collider hit in hits)
        {
            PlayerStats ps = hit.GetComponentInParent<PlayerStats>() ?? hit.GetComponent<PlayerStats>();
            if (ps != null)
                ps.TakeDamage(stats.meleeDamage);
        }
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private void AgentStop()
    {
        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    private void ScheduleNextBlock()
    {
        nextBlockTime = Time.time + Random.Range(blockIntervalMin, blockIntervalMax);
    }

    private void CrossFade(int stateHash)
    {
        if (animator == null)
            return;
        animator.CrossFade(stateHash, crossFadeTime, 0);
    }

    private void CacheHashes()
    {
        hashIdle       = Animator.StringToHash("Base Layer.Idle");
        hashWalk       = Animator.StringToHash("Base Layer.Walk");
        hashRun        = Animator.StringToHash("Base Layer.Run");
        hashAttack01   = Animator.StringToHash("Base Layer.Attack01");
        hashAttack02   = Animator.StringToHash("Base Layer.Attack02");
        hashBlock      = Animator.StringToHash("Base Layer.Block");
        hashTakeDamage = Animator.StringToHash("Base Layer.TakeDamage");
        hashDeath      = Animator.StringToHash("Base Layer.Death");
    }

    public float BlockDamageMultiplier => isBlocking ? blockDamageMultiplier : 1f;

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnDamageTaken -= HandleDamageTaken;
            stats.OnDeath -= HandleDeath;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
