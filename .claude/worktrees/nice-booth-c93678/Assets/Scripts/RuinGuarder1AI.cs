using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class RuinGuarder1AI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float loseAggroRange = 18f;
    [SerializeField] private LayerMask playerLayer;

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

    private enum State { Idle, Chase, Attack, Block, TakeDamage, Dead }

    private NavMeshAgent agent;
    private EnemyStats stats;
    private Transform player;

    private State currentState = State.Idle;
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

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheHashes();
        ScheduleNextBlock();

        stats.OnDamageTaken += HandleDamageTaken;
        stats.OnDeath += HandleDeath;
    }

    private void Update()
    {
        if (currentState == State.Dead)
            return;

        if (attackHitPending && Time.time >= attackHitTime)
        {
            attackHitPending = false;
            DealMeleeDamage();
        }

        FindPlayer();
        UpdateState();
    }

    private void FindPlayer()
    {
        if (player != null)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);
        if (hits.Length > 0)
            player = hits[0].transform;
    }

    private void UpdateState()
    {
        if (Time.time < stateLockedUntil)
            return;

        if (currentState == State.Block)
        {
            isBlocking = false;
            EnterChaseOrIdle();
            return;
        }

        if (player == null)
        {
            if (currentState != State.Idle)
                EnterIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > loseAggroRange)
        {
            player = null;
            EnterIdle();
            return;
        }

        if (Time.time >= nextBlockTime && currentState != State.Attack)
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
        currentState = State.Idle;
        agent.isStopped = true;
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
        currentState = State.Chase;
        agent.isStopped = false;
        agent.SetDestination(player.position);

        bool shouldRun = distToPlayer > runDistanceThreshold;
        agent.speed = shouldRun ? runSpeed : walkSpeed;
        CrossFade(shouldRun ? hashRun : hashWalk);
    }

    private void EnterAttack()
    {
        currentState = State.Attack;
        agent.isStopped = true;

        FacePlayer();

        int attackHash = (nextAttackIndex % 2 == 0) ? hashAttack01 : hashAttack02;
        nextAttackIndex++;

        float lockTime = attackCooldown;
        stateLockedUntil = Time.time + lockTime;
        nextAttackTime = Time.time + lockTime;

        attackHitTime = Time.time + attackHitDelay;
        attackHitPending = true;

        CrossFade(attackHash);
    }

    private void EnterBlock()
    {
        currentState = State.Block;
        isBlocking = true;
        agent.isStopped = true;
        stateLockedUntil = Time.time + blockDuration;
        ScheduleNextBlock();
        CrossFade(hashBlock);
    }

    private void HandleDamageTaken(float amount)
    {
        if (currentState == State.Dead)
            return;

        attackHitPending = false;

        // Interrupt current state unless already in TakeDamage
        if (currentState != State.TakeDamage)
        {
            currentState = State.TakeDamage;
            agent.isStopped = true;
            stateLockedUntil = Time.time + 0.7f;
            CrossFade(hashTakeDamage);
        }
    }

    private void HandleDeath()
    {
        currentState = State.Dead;
        attackHitPending = false;
        agent.isStopped = true;
        agent.enabled = false;
        CrossFade(hashDeath);

        GetComponent<Collider>()?.gameObject.SetActive(false);
    }

    private void DealMeleeDamage()
    {
        if (player == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * (attackRange * 0.5f),
            attackHitRadius,
            playerLayer
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
        hashIdle        = Animator.StringToHash("Base Layer.Idle");
        hashWalk        = Animator.StringToHash("Base Layer.Walk");
        hashRun         = Animator.StringToHash("Base Layer.Run");
        hashAttack01    = Animator.StringToHash("Base Layer.Attack01");
        hashAttack02    = Animator.StringToHash("Base Layer.Attack02");
        hashBlock       = Animator.StringToHash("Base Layer.Block");
        hashTakeDamage  = Animator.StringToHash("Base Layer.TakeDamage");
        hashDeath       = Animator.StringToHash("Base Layer.Death");
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
