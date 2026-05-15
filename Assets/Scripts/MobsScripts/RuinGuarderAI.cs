using UnityEngine;
using UnityEngine.AI;

/// Tier-1 guard AI. Guards Safe Ruins entrances, first enemy the player meets.
/// States: Patrol → Alert → Chase → Attack → Return → Dead
/// Block: reactive 20% chance (IBlockable) evaluated once per player swing.
/// CallForHelp: alerts nearby allies when HP drops below 50%.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class RuinGuarderAI : EnemyBase, IBlockable
{
    private enum GuarderState { Patrol, Alert, Chase, Attack, Return, Dead }

    // ─── Inspector ────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private RuinGuarderData data;

    [Header("Patrol Waypoints")]
    [SerializeField] private Transform[] waypoints;

    // ─── Animator hashes ──────────────────────────────────────────────

    private int hashIdle, hashWalk, hashRun, hashAlert;
    private int hashLightAttack, hashHeavyAttack;
    private int hashBlock, hashTakeDamage, hashDeath;

    // ─── Runtime state ────────────────────────────────────────────────

    private GuarderState currentState   = GuarderState.Patrol;
    private float       stateLockUntil;

    // Patrol
    private int   waypointIndex;
    private bool  waitingAtWaypoint;
    private float waypointIdleTimer;

    // Alert
    private float alertTimer;

    // Attack
    private float nextAttackTime;
    private bool  attackPending;
    private float attackHitTime;
    private bool  isHeavyAttack;

    // CallForHelp
    private bool calledForHelp;

    // ─── Unity ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        CacheHashes();
    }

    private void Start()
    {
        // Apply SO stats after all Awakes; ensures EnemyStats.Awake ran first.
        if (data != null)
            stats.Initialize(data.maxHP, data.defence);

        waypointIdleTimer = data != null ? data.waypointIdleTime : 2f;

        if (waypoints != null && waypoints.Length > 0)
            AgentMove(data != null ? data.patrolSpeed : 2.5f, waypoints[waypointIndex].position);
    }

    private void Update()
    {
        if (currentState == GuarderState.Dead) return;

        // Resolve a pending melee hit at the right moment.
        if (attackPending && Time.time >= attackHitTime)
        {
            attackPending = false;
            DealMeleeDamage(isHeavyAttack);
        }

        if (Time.time < stateLockUntil) return;
        UpdateFSM();
    }

    // ─── FSM dispatch ────────────────────────────────────────────────

    private void UpdateFSM()
    {
        switch (currentState)
        {
            case GuarderState.Patrol:  UpdatePatrol();  break;
            case GuarderState.Alert:   UpdateAlert();   break;
            case GuarderState.Chase:   UpdateChase();   break;
            case GuarderState.Attack:  UpdateAttack();  break;
            case GuarderState.Return:  UpdateReturn();  break;
        }
    }

    // ─── Patrol ───────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        TryDetectPlayer();

        if (waypoints == null || waypoints.Length == 0)
        {
            CrossFade(hashIdle);
            return;
        }

        if (!waitingAtWaypoint)
        {
            CrossFade(hashWalk);

            if (!agent.pathPending && agent.remainingDistance <= data.waypointTolerance)
            {
                waitingAtWaypoint = true;
                waypointIdleTimer = data.waypointIdleTime;
                AgentStop();
                CrossFade(hashIdle);
            }
        }
        else
        {
            waypointIdleTimer -= Time.deltaTime;
            if (waypointIdleTimer <= 0f)
            {
                waitingAtWaypoint = false;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                AgentMove(data.patrolSpeed, waypoints[waypointIndex].position);
                CrossFade(hashWalk);
            }
        }
    }

    // ─── Alert ────────────────────────────────────────────────────────

    private void UpdateAlert()
    {
        alertTimer -= Time.deltaTime;
        if (alertTimer <= 0f)
            EnterChase();
    }

    // ─── Chase ────────────────────────────────────────────────────────

    private void UpdateChase()
    {
        if (player == null) { EnterPatrol(); return; }

        float dist = DistToPlayer();
        if (dist > data.chaseRange)    { EnterReturn(); return; }
        if (dist <= data.attackRange)  { EnterAttack(); return; }

        AgentMove(data.chaseSpeed, player.position);
        FacePlayer();
        CrossFade(hashRun);
    }

    // ─── Attack ───────────────────────────────────────────────────────

    private void UpdateAttack()
    {
        if (player == null) { EnterPatrol(); return; }

        if (DistToPlayer() > data.attackRange * 1.3f)
        {
            EnterChase();
            return;
        }

        FacePlayer();

        if (!attackPending && Time.time >= nextAttackTime)
            PerformAttack();
    }

    // ─── Return ───────────────────────────────────────────────────────

    private void UpdateReturn()
    {
        TryDetectPlayer();

        if (!agent.pathPending && agent.remainingDistance <= data.waypointTolerance)
            EnterPatrol();
    }

    // ─── State entries ────────────────────────────────────────────────

    private void EnterAlert()
    {
        currentState = GuarderState.Alert;
        alertTimer   = data.alertDelay;
        AgentStop();
        CrossFade(hashAlert);
    }

    private void EnterChase()
    {
        currentState = GuarderState.Chase;
        if (player != null)
            AgentMove(data.chaseSpeed, player.position);
        CrossFade(hashRun);
    }

    private void EnterAttack()
    {
        currentState   = GuarderState.Attack;
        nextAttackTime = 0f;   // attack immediately on entry
        AgentStop();
        FacePlayer();
    }

    private void EnterPatrol()
    {
        currentState      = GuarderState.Patrol;
        waitingAtWaypoint = false;

        if (waypoints != null && waypoints.Length > 0)
            AgentMove(data.patrolSpeed, waypoints[waypointIndex].position);

        CrossFade(hashWalk);
    }

    private void EnterReturn()
    {
        currentState = GuarderState.Return;

        Vector3 target = (waypoints != null && waypoints.Length > 0)
            ? waypoints[waypointIndex].position
            : transform.position;

        AgentMove(data.patrolSpeed, target);
        CrossFade(hashWalk);
    }

    // ─── Detection ───────────────────────────────────────────────────

    private void TryDetectPlayer()
    {
        if (CanSeePlayer(data.detectionRange, data.fieldOfView))
            EnterAlert();
    }

    // ─── Combat ──────────────────────────────────────────────────────

    private void PerformAttack()
    {
        isHeavyAttack = Random.value < data.heavyAttackChance;

        float cooldown = isHeavyAttack ? data.heavyCooldown  : data.lightCooldown;
        float hitDelay = isHeavyAttack ? data.heavyHitDelay  : data.lightHitDelay;

        nextAttackTime = Time.time + cooldown;
        attackHitTime  = Time.time + hitDelay;
        attackPending  = true;

        CrossFade(isHeavyAttack ? hashHeavyAttack : hashLightAttack);
    }

    private void DealMeleeDamage(bool heavy)
    {
        float hitRadius = heavy ? data.heavyHitRadius : data.lightHitRadius;
        float damage    = heavy ? data.heavyDamage    : data.lightDamage;

        Vector3 origin = transform.position
            + transform.forward * data.attackForwardOffset
            + Vector3.up;

        Collider[] hits = Physics.OverlapSphere(origin, hitRadius);
        foreach (Collider hit in hits)
        {
            PlayerStats ps = hit.GetComponentInParent<PlayerStats>()
                          ?? hit.GetComponent<PlayerStats>();
            if (ps == null) continue;

            ps.TakeDamage(damage);

            if (heavy && playerMovement != null)
                playerMovement.PlayHitReaction();
        }
    }

    // ─── IBlockable ──────────────────────────────────────────────────

    // Called once per player swing from PlayerMovement.CheckPendingMeleeHit.
    public float BlockDamageMultiplier
    {
        get
        {
            if (data == null) return 1f;
            if (Random.value < data.blockChance)
            {
                CrossFade(hashBlock);
                return data.blockDamageMultiplier;
            }
            return 1f;
        }
    }

    // ─── CallForHelp ─────────────────────────────────────────────────

    private void TryCallForHelp()
    {
        if (calledForHelp) return;
        calledForHelp = true;

        Collider[] nearby = Physics.OverlapSphere(transform.position, data.helpCallRadius);
        foreach (Collider col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            RuinGuarderAI ally = col.GetComponentInParent<RuinGuarderAI>()
                              ?? col.GetComponent<RuinGuarderAI>();
            ally?.AlertToPlayer(player);
        }
    }

    /// Called by a nearby ally's TryCallForHelp to aggro this guard.
    public void AlertToPlayer(Transform target)
    {
        if (currentState == GuarderState.Dead) return;
        if (target != null) player = target;
        if (currentState != GuarderState.Chase && currentState != GuarderState.Attack)
            EnterAlert();
    }

    // ─── EnemyBase overrides ─────────────────────────────────────────

    protected override void HandleDamageTaken(float amount)
    {
        if (currentState == GuarderState.Dead) return;

        attackPending = false;

        if (currentState != GuarderState.Attack)
        {
            AgentStop();
            stateLockUntil = Time.time + 0.5f;
            ResetAnimHash();
            CrossFade(hashTakeDamage);
        }

        // Being hit interrupts patrol/return → start chasing immediately.
        if (currentState == GuarderState.Patrol
         || currentState == GuarderState.Alert
         || currentState == GuarderState.Return)
            currentState = GuarderState.Chase;

        if (data != null && stats.CurrentHP / data.maxHP < data.helpCallHPThreshold)
            TryCallForHelp();
    }

    protected override void HandleDeath()
    {
        currentState  = GuarderState.Dead;
        attackPending = false;
        agent.enabled = false;

        ResetAnimHash();
        CrossFade(hashDeath);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private void CacheHashes()
    {
        hashIdle        = Animator.StringToHash("Base Layer.Idle");
        hashWalk        = Animator.StringToHash("Base Layer.Walk");
        hashRun         = Animator.StringToHash("Base Layer.Run");
        hashAlert       = Animator.StringToHash("Base Layer.Idle");
        hashLightAttack = Animator.StringToHash("Base Layer.Attack01");
        hashHeavyAttack = Animator.StringToHash("Base Layer.Attack02");
        hashBlock       = Animator.StringToHash("Base Layer.Block");
        hashTakeDamage  = Animator.StringToHash("Base Layer.TakeDamage");
        hashDeath       = Animator.StringToHash("Base Layer.Death");
    }

    // ─── Gizmos ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, data.chaseRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.helpCallRadius);

        if (waypoints == null) return;
        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.2f);
            int next = (i + 1) % waypoints.Length;
            if (waypoints[next] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
}
