using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack }

    [Header("Detection")]
    public float detectionRange  = 10f;
    public float attackRange     = 2f;
    [Range(0f, 360f)]
    public float fieldOfView     = 120f;
    public LayerMask sightMask   = -1;

    [Header("Movement")]
    public float patrolSpeed     = 2f;
    public float chaseSpeed      = 4f;

    [Header("Patrol")]
    public Transform[] waypoints;
    public float waypointTolerance = 0.5f;
    public float waypointIdleTime  = 1.5f;

    [Header("Attack")]
    public float attackCooldown  = 2f;
    public float attackDamage    = 20f;
    public float attackHitDelay  = 0.4f;

    [Header("Chase")]
    public float loseTargetDelay = 3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam  = "Speed";
    [SerializeField] private string attackParam = "Attack";

    // ── Runtime ──────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Transform    player;
    private PlayerStats  playerStats;

    private State currentState;
    private int   waypointIndex;
    private float idleTimer;
    private float loseTimer;
    private float attackTimer;
    private bool  isAttacking;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            player      = pm.transform;
            playerStats = pm.GetComponent<PlayerStats>()
                       ?? pm.GetComponentInChildren<PlayerStats>();
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        SetState(waypoints.Length > 0 ? State.Patrol : State.Idle);
    }

    private void Update()
    {
        UpdateFSM();
        UpdateAnimation();
    }

    // ── FSM ───────────────────────────────────────────────────────

    private void UpdateFSM()
    {
        switch (currentState)
        {
            case State.Idle:
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f && waypoints.Length > 0)
                {
                    AdvanceWaypoint();
                    SetState(State.Patrol);
                }
                TryDetectPlayer();
                break;

            case State.Patrol:
                if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
                {
                    idleTimer = waypointIdleTime;
                    SetState(State.Idle);
                }
                TryDetectPlayer();
                break;

            case State.Chase:
                if (player == null) { SetState(State.Patrol); return; }

                if (Vector3.Distance(transform.position, player.position) <= attackRange)
                {
                    SetState(State.Attack);
                    return;
                }

                agent.SetDestination(player.position);

                loseTimer = CanSeePlayer() ? loseTargetDelay : loseTimer - Time.deltaTime;
                if (loseTimer <= 0f)
                    SetState(State.Patrol);
                break;

            case State.Attack:
                if (player == null) { SetState(State.Patrol); return; }

                FacePlayer();

                if (Vector3.Distance(transform.position, player.position) > attackRange * 1.2f)
                {
                    SetState(State.Chase);
                    return;
                }

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f && !isAttacking)
                    PerformAttack();
                break;
        }
    }

    private void TryDetectPlayer()
    {
        if (CanSeePlayer())
        {
            loseTimer = loseTargetDelay;
            SetState(State.Chase);
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        float   dist     = toPlayer.magnitude;

        if (dist > detectionRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > fieldOfView * 0.5f) return false;

        // Line of sight
        Vector3 origin = transform.position + Vector3.up;
        if (Physics.Raycast(origin, toPlayer.normalized, dist, ~sightMask))
            return false;

        return true;
    }

    // ── State Transitions ─────────────────────────────────────────

    private void SetState(State next)
    {
        currentState = next;

        switch (next)
        {
            case State.Idle:
                agent.isStopped = true;
                break;

            case State.Patrol:
                agent.isStopped = false;
                agent.speed     = patrolSpeed;
                if (waypoints.Length > 0)
                    agent.SetDestination(waypoints[waypointIndex].position);
                break;

            case State.Chase:
                agent.isStopped = false;
                agent.speed     = chaseSpeed;
                break;

            case State.Attack:
                agent.isStopped = true;
                attackTimer     = 0f;
                break;
        }
    }

    private void AdvanceWaypoint()
    {
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }

    // ── Combat ────────────────────────────────────────────────────

    private void PerformAttack()
    {
        isAttacking = true;
        attackTimer = attackCooldown;

        if (animator != null)
            animator.SetTrigger(attackParam);

        Invoke(nameof(ApplyDamage),    attackHitDelay);
        Invoke(nameof(FinishAttack),   attackCooldown * 0.5f);
    }

    private void ApplyDamage()
    {
        if (player == null) return;
        if (Vector3.Distance(transform.position, player.position) > attackRange * 1.5f) return;

        if (playerStats != null)
            playerStats.currentHp = Mathf.Max(0f, playerStats.currentHp - attackDamage);
    }

    private void FinishAttack() => isAttacking = false;

    // ── Helpers ───────────────────────────────────────────────────

    private void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        float speed = agent.velocity.magnitude / Mathf.Max(chaseSpeed, 0.01f);
        animator.SetFloat(speedParam, speed, 0.1f, Time.deltaTime);
    }

    // ── Gizmos ────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.3f);
            int next = (i + 1) % waypoints.Length;
            if (waypoints[next] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
}
