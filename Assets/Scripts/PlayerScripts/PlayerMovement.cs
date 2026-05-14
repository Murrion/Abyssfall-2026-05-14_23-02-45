using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed     = 4f;
    public float armedRunSpeed = 3f;
    public float walkSpeed     = 2f;
    public float rotationSpeed = 10f;

    [Header("Physics")]
    [SerializeField] private float gravity = -15f;

    [Header("Foot IK")]
    [SerializeField] private bool  enableFootIK      = true;
    [SerializeField] [Range(0f,1f)] private float footIKWeight   = 1f;
    [SerializeField] private float footRayDistance   = 0.5f;
    [SerializeField] private float footVerticalOffset  = 0.09f;
    [SerializeField] private float footPlantThreshold = 0.12f;
    [SerializeField] private LayerMask groundMask     = -1;

    [Header("Model Rotation")]
    [SerializeField] private float armedModelRotationY = -45f;
    [SerializeField] private float modelRotationSpeed  = 10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool startsArmed = false;
    [SerializeField] private float animationFadeTime          = 0.15f;
    [SerializeField] private float blockFadeTime              = 0.3f;
    [SerializeField] private float blockSpeedMultiplier       = 0.5f;
    [SerializeField] private float attackLockTime             = 0.8f;
    [SerializeField] private float attackWalkSpeedMultiplier  = 0.8f;
    public             float attackSpeed                      = 1f;
    [SerializeField] private float strongAttackSpeedMultiplier = 0.5f;
    [SerializeField] private float strongAttackLockTime        = 1.2f;
    [SerializeField] private float strongAttackStaminaCost     = 50f;
    [SerializeField] private float equipLockTime          = 1f;
    [SerializeField] private float equipSpeedMultiplier   = 0.5f;
    [SerializeField] private float equipWalkCycleDistance = 1.4f;
    [SerializeField] private float tauntLockTime = 1.4f;
    [SerializeField] private float hitLockTime = 0.8f;
    [SerializeField] private float idleVariantDelay = 4f;
    [SerializeField] private float idleVariantLockTime = 2f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    [SerializeField] private float staminaCostPerAttack  = 25f;
    [SerializeField] private float staminaRegenRate      = 20f;
    [SerializeField] private float staminaRegenDelay     = 1.5f;
    [SerializeField] private float staminaBlockDrainRate = 5f;
    [SerializeField] private float staminaRunDrainRate   = 8f;

    [Header("Melee Hit Detection")]
    [SerializeField] private float meleeHitRadius         = 1.2f;
    [SerializeField] private float meleeHitForwardOffset  = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Distance Matching")]
    [SerializeField] private bool distanceMatchLocomotion = true;
    [SerializeField] private float runCycleDistance        = 2.4f;
    [SerializeField] private float walkCycleDistance       = 1.4f;
    [SerializeField] private float attackWalkCycleDistance = 1.4f;
    [SerializeField] private float blockWalkCycleDistance  = 1.4f;

    [Header("Standing States")]
    [SerializeField] private string standingIdleStateName = "standing idle";
    [SerializeField] private string standingRunStateName = "standing run forward";
    [SerializeField] private string standingWalkStateName = "standing walk forward";
    [SerializeField] private string standingBlockStateName = "standing block idle";

    [Header("Unarmed States")]
    [SerializeField] private string unarmedIdleStateName = "unarmed idle";
    [SerializeField] private string unarmedRunStateName = "unarmed run forward";
    [SerializeField] private string unarmedWalkStateName = "unarmed walk forward";

    [Header("Action States")]
    [SerializeField] private string[] standingIdleVariantStateNames =
    {
        "standing idle looking ver. 1",
        "standing idle looking ver. 2"
    };
    [SerializeField] private string[] unarmedIdleVariantStateNames =
    {
        "unarmed idle looking ver. 1",
        "unarmed idle looking ver. 2"
    };
    [SerializeField] private string[] regularAttackStateNames =
    {
        "standing melee attack horizontal",
        "standing melee attack backhand",
        "standing melee attack kick ver. 1",
        "standing melee attack kick ver. 2",
    };
    [SerializeField] private string[] combo2StateNames =
    {
        "standing melee combo attack ver. 1",
        "standing melee combo attack ver. 2",
    };
    [SerializeField] private string[] combo3StateNames =
    {
        "standing melee combo attack ver. 3",
    };

    [Header("Combo")]
    [SerializeField] private float comboBufferWindowNT = 0.65f;
    [SerializeField] private float comboTimeout        = 0.8f;
    [SerializeField] private string[] strongAttackStateNames =
    {
        "standing melee attack 360 high",
        "standing melee attack 360 low",
        "standing melee attack downward",
    };
    [SerializeField] private string[] tauntStateNames =
    {
        "standing taunt battlecry",
        "standing taunt chest thump"
    };
    [SerializeField] private string[] hitStateNames =
    {
        "standing react large gut",
        "standing react large from left",
        "standing react large from right",
        "standing block react large"
    };
    [SerializeField] private string[] equipStateNames =
    {
        "unarmed equip over shoulder",
        "unarmed equip underarm"
    };
    [SerializeField] private string[] disarmStateNames =
    {
        "standing disarm over shoulder",
        "standing disarm underarm"
    };

    public float MaxStamina     => maxStamina;
    public float CurrentStamina => currentStamina;
    public float runSpeed       => moveSpeed;
    public int   ComboStep      => comboStep;
    public int   ComboMaxSteps  => 3;
    public float ComboWindowEnd => comboTimeoutAt;

    private const int UpperBodyLayer = 1;

    private CharacterController controller;
    private PlayerControls controls;
    private Transform movementRoot;
    private Vector2 moveInput;
    private bool isArmed;
    private bool previousIsArmed;
    private bool lockIsEquipOrDisarm;
    private float animationLockedUntil;
    private float attackLockedUntil;
    private bool isPlayingUpperBodyAttack;
    private int comboStep;
    private bool comboWindowOpen;
    private bool comboBuffered;
    private float comboTimeoutAt;
    private int nextRegularAttackIndex;
    private int nextCombo2Index;
    private int nextCombo3Index;
    private int nextStrongAttackIndex;
    private int nextTauntIndex;
    private int nextHitIndex;
    private int nextEquipIndex;
    private int nextDisarmIndex;
    private int nextStandingIdleVariantIndex;
    private int nextUnarmedIdleVariantIndex;
    private float nextIdleVariantTime;
    private float staminaRegenDelayTimer;
    private bool isBlocking;
    private bool isRunning;
    private float idleVariantLockedUntil;
    private int currentAttackStateHash;
    private float verticalVelocity;
    private float currentBodyOffset;
    private float bodyOffsetVelocity;
    private float attackStartTime;
    private float currentAttackDuration;
    private float currentAttackSpeedMultiplier = 1f;
    private bool  meleeHitPending;
    private float meleeHitTime;
    private bool  isEquipping;
    private bool  isEquippingReverse;
    private int   currentEquipHash;
    private float currentEquipDuration;
    private float currentEquipNT; // 0=start, 1=end; always valid direction
    private float locomotionDistance;
    private int currentDistanceMatchedStateHash;

    private AnimationState standingIdleState;
    private AnimationState standingRunState;
    private AnimationState standingWalkState;
    private AnimationState standingBlockState;
    private AnimationState unarmedIdleState;
    private AnimationState unarmedRunState;
    private AnimationState unarmedWalkState;
    private AnimationState[] standingIdleVariantStates;
    private AnimationState[] unarmedIdleVariantStates;
    private AnimationState[] regularAttackStates;
    private AnimationState[] combo2States;
    private AnimationState[] combo3States;
    private AnimationState[] strongAttackStates;
    private AnimationState[] tauntStates;
    private AnimationState[] hitStates;
    private AnimationState[] equipStates;
    private AnimationState[] disarmStates;

    private struct AnimationState
    {
        public int Hash;
        public bool Exists;
        public int Layer;
        public float Duration;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();
        movementRoot = controller != null ? controller.transform : transform;
        animator = animator != null
            ? animator
            : movementRoot.GetComponentInChildren<Animator>() ?? GetComponentInChildren<Animator>() ?? GetComponentInParent<Animator>();
        isArmed = startsArmed;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            TryAssignAnimatorControllerInEditor();
        }

        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        CacheAnimatorStates();
    }

    private void Start()
    {
        PlayState(GetIdleState(), true);
        ResetIdleVariantTimer();
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();

    private void LateUpdate()
    {
        if (animator == null)
            return;

        // Smooth upper body layer weight for block/equip вЂ” runs every frame regardless of other logic
        if (!isPlayingUpperBodyAttack)
        {
            float targetWeight = (isBlocking || isEquipping) ? 1f : 0f;
            float speed        = blockFadeTime > 0.001f ? 1f / blockFadeTime : 1000f;
            animator.SetLayerWeight(UpperBodyLayer,
                Mathf.MoveTowards(animator.GetLayerWeight(UpperBodyLayer), targetWeight, speed * Time.deltaTime));
        }

        // Guard: never touch movementRoot rotation вЂ” that is controlled by Update
        if (animator.transform == movementRoot)
            return;

        // Local model rotation: only equip interpolation вЂ” armed rotation is handled in Update()
        Quaternion armedRot = Quaternion.Euler(0f, armedModelRotationY, 0f);
        Quaternion targetRotation = isEquipping
            ? Quaternion.Slerp(Quaternion.identity, armedRot, currentEquipNT)
            : Quaternion.identity;

        animator.transform.localRotation = Quaternion.Slerp(
            animator.transform.localRotation,
            targetRotation,
            modelRotationSpeed * Time.deltaTime
        );
    }

    private void Update()
    {
        Vector3 move = comboStep == 3 && isPlayingUpperBodyAttack
            ? Vector3.zero
            : Quaternion.Euler(0f, 45f, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        if (move != Vector3.zero)
        {
            float rotOff = isEquipping
                ? armedModelRotationY * currentEquipNT
                : (isArmed ? armedModelRotationY : 0f);

            Quaternion targetFacing = Quaternion.LookRotation(move);
            if (rotOff != 0f)
                targetFacing *= Quaternion.Euler(0f, rotOff, 0f);

            movementRoot.rotation = Quaternion.Slerp(
                movementRoot.rotation,
                targetFacing,
                rotationSpeed * Time.deltaTime
            );
        }

        Vector3 positionBefore = movementRoot.position;

        bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool actionActive = isPlayingUpperBodyAttack || isBlocking || Time.time < animationLockedUntil;
        isRunning = shiftHeld && !actionActive && move.sqrMagnitude > 0.001f && currentStamina > 0f;

        float speed;
        if (isPlayingUpperBodyAttack)
            speed = walkSpeed * attackWalkSpeedMultiplier;
        else if (isEquipping)
            speed = walkSpeed * equipSpeedMultiplier;
        else if (isRunning)
            speed = isArmed ? armedRunSpeed : moveSpeed;
        else
            speed = walkSpeed;
        if (isBlocking)
            speed *= blockSpeedMultiplier;

        if (controller != null)
        {
            bool grounded = CheckGrounded(out RaycastHit groundHit);

            if (grounded)
            {
                float gap = movementRoot.position.y - groundHit.point.y;
                // Proportional snap: larger gap в†’ stronger pull
                verticalVelocity = gap > 0.01f ? -Mathf.Max(gap * 20f, 2f) : -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
        else
        {
            movementRoot.position += move * speed * Time.deltaTime;
        }

        float distanceMoved = Vector3.ProjectOnPlane(
            movementRoot.position - positionBefore,
            Vector3.up
        ).magnitude;

        UpdateAnimation(move.sqrMagnitude > 0.0001f, distanceMoved);

        // Drive equip/disarm animation manually вЂ” prevents freeze from animator.speed=0
        if (isEquipping && animator != null && currentEquipDuration > 0.01f)
        {
            float step = Time.deltaTime / currentEquipDuration;
            currentEquipNT = isEquippingReverse
                ? Mathf.Clamp01(currentEquipNT - step)
                : Mathf.Clamp01(currentEquipNT + step);
            animator.Play(currentEquipHash, UpperBodyLayer, currentEquipNT);
        }

        // Always drive attack animation by real time вЂ” same speed standing or walking
        if (isPlayingUpperBodyAttack && animator != null && currentAttackDuration > 0.01f)
        {
            float effectiveSpeed    = attackSpeed * currentAttackSpeedMultiplier;
            float effectiveDuration = currentAttackDuration / Mathf.Max(effectiveSpeed, 0.01f);
            float attackNT          = Mathf.Clamp01((Time.time - attackStartTime) / effectiveDuration);
            animator.Play(currentAttackStateHash, UpperBodyLayer, attackNT);
        }

        UpdateStamina();
        CheckPendingMeleeHit();
    }

    private void UpdateStamina()
    {
        if (isBlocking)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaBlockDrainRate * Time.deltaTime);
            staminaRegenDelayTimer = staminaRegenDelay;
            return;
        }

        if (isRunning)
        {
            currentStamina = Mathf.Max(0f, currentStamina - staminaRunDrainRate * Time.deltaTime);
            staminaRegenDelayTimer = staminaRegenDelay;
            return;
        }

        if (staminaRegenDelayTimer > 0f)
        {
            staminaRegenDelayTimer -= Time.deltaTime;
            return;
        }

        if (currentStamina < maxStamina)
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
    }

    [ContextMenu("Reset Animation Defaults")]
    private void ResetAnimationDefaults()
    {
        animationFadeTime   = 0.15f;
        attackLockTime      = 0.8f;
        strongAttackLockTime = 1.2f;
        equipLockTime       = 1f;
        tauntLockTime       = 1.4f;
        hitLockTime         = 0.8f;
        idleVariantDelay    = 4f;
        idleVariantLockTime = 2f;
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        if (animator != null)
            animator.applyRootMotion = false;
        CacheAnimatorStates();
    }

    public void PlayHitReaction()
    {
        PlayNextLockedState(hitStates, ref nextHitIndex, hitLockTime);
    }

    private void CacheAnimatorStates()
    {
        standingIdleState = MakeState(standingIdleStateName);
        standingRunState = MakeState(standingRunStateName);
        standingWalkState = MakeState(standingWalkStateName);
        int blockLayer = animator != null && animator.layerCount > UpperBodyLayer ? UpperBodyLayer : 0;
        standingBlockState = MakeState(standingBlockStateName, blockLayer);
        unarmedIdleState = MakeState(unarmedIdleStateName);
        unarmedRunState = MakeState(unarmedRunStateName);
        unarmedWalkState = MakeState(unarmedWalkStateName);
        standingIdleVariantStates = MakeStates(standingIdleVariantStateNames);
        unarmedIdleVariantStates = MakeStates(unarmedIdleVariantStateNames);
        int attackLayer = animator != null && animator.layerCount > UpperBodyLayer ? UpperBodyLayer : 0;
        regularAttackStates = MakeStates(regularAttackStateNames, attackLayer);
        combo2States        = MakeStates(combo2StateNames,        attackLayer);
        combo3States        = MakeStates(combo3StateNames,        attackLayer);
        strongAttackStates  = MakeStates(strongAttackStateNames,  attackLayer);
        tauntStates  = MakeStates(tauntStateNames);
        hitStates    = MakeStates(hitStateNames);
        equipStates  = MakeStates(equipStateNames,  attackLayer);
        disarmStates = MakeStates(disarmStateNames, attackLayer);
    }

    private AnimationState MakeState(string stateName, int layer = 0)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return default;
        if (animator.layerCount <= layer)
            return default;

        string layerName = animator.GetLayerName(layer);
        int hash = Animator.StringToHash($"{layerName}.{SanitizeStateName(stateName)}");
        float duration = GetClipLength(stateName);
        return new AnimationState { Hash = hash, Exists = animator.HasState(layer, hash), Layer = layer, Duration = duration };
    }

    private float GetClipLength(string stateName)
    {
        if (animator?.runtimeAnimatorController == null)
            return 1f;

        string sanitized = SanitizeStateName(stateName);
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (SanitizeStateName(clip.name) == sanitized)
                return clip.length;
        }
        return 1f;
    }

    private AnimationState[] MakeStates(string[] stateNames, int layer = 0)
    {
        AnimationState[] states = new AnimationState[stateNames.Length];
        for (int i = 0; i < stateNames.Length; i++)
            states[i] = MakeState(stateNames[i], layer);
        return states;
    }

    private void UpdateAnimation(bool isMoving, float distanceMoved)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (isPlayingUpperBodyAttack)
        {
            bool transitioning = animator.IsInTransition(UpperBodyLayer);
            float nt = transitioning ? 0f : animator.GetCurrentAnimatorStateInfo(UpperBodyLayer).normalizedTime;

            // Open combo buffer window at comboBufferWindowNT %
            if (!comboWindowOpen && !transitioning && nt >= comboBufferWindowNT && comboStep < 3)
                comboWindowOpen = true;

            // Animation finished
            if (!transitioning && nt >= 0.92f)
            {
                animator.SetLayerWeight(UpperBodyLayer, 0f);
                isPlayingUpperBodyAttack = false;
                attackLockedUntil = Time.time + animationFadeTime;
                comboWindowOpen = false;

                if (comboBuffered && comboStep < 3)
                {
                    comboBuffered = false;
                    PlayComboStep();
                }
                else
                {
                    comboTimeoutAt = Time.time + comboTimeout;
                }
            }
        }
        else if (comboStep > 0 && Time.time >= comboTimeoutAt)
        {
            comboStep = 0;
            comboBuffered = false;
        }

        // Equip end detection via currentEquipNT (works for both directions)
        if (isEquipping && !isPlayingUpperBodyAttack)
        {
            bool done = isEquippingReverse ? (currentEquipNT <= 0.05f) : (currentEquipNT >= 0.95f);
            if (done)
            {
                isEquipping         = false;
                lockIsEquipOrDisarm = false;
            }
        }


        bool blockHeld        = Mouse.current != null && Mouse.current.rightButton.isPressed && currentStamina > 0f;
        bool blockJustPressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        // Cancel DISARM (previousIsArmed=true) with attack or block вЂ” EQUIP (previousIsArmed=false) is not cancellable
        if (isEquipping && previousIsArmed)
        {
            bool attackPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if ((attackPressed || blockHeld) && Time.time >= animationLockedUntil)
                CancelEquip(); // reverts isArmed = true so attack/block checks pass below
        }

        // Attack: only when not equipping (unarmed equip must finish first)
        if (isArmed && !isEquipping && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            Time.time >= animationLockedUntil)
        {
            bool strongAttack = Keyboard.current != null && Keyboard.current.altKey.isPressed;
            if (strongAttack && Time.time >= attackLockedUntil)
            {
                comboStep = 0;
                comboWindowOpen = false;
                comboBuffered = false;
                currentAttackSpeedMultiplier = strongAttackSpeedMultiplier;
                PlayNextAttack(strongAttackStates, ref nextStrongAttackIndex, strongAttackStaminaCost, strongAttackLockTime);
            }
            else if (!strongAttack)
            {
                if (!isPlayingUpperBodyAttack && Time.time >= attackLockedUntil)
                {
                    comboStep = 0;
                    currentAttackSpeedMultiplier = 1f;
                    PlayComboStep();
                }
                else if (comboWindowOpen && comboStep < 3)
                {
                    comboBuffered = true;
                }
            }
            idleVariantLockedUntil = 0f;
            ResetIdleVariantTimer();
        }

        if (Keyboard.current != null)
        {
            // E key: blocked during attack/block; mid-equip в†’ flip direction without restart
            if (Keyboard.current.eKey.wasPressedThisFrame && Time.time >= animationLockedUntil
                && !isPlayingUpperBodyAttack && !isBlocking)
            {
                if (isEquipping)
                {
                    // Flip direction from current position вЂ” rotation follows automatically
                    isEquippingReverse = !isEquippingReverse;
                    isArmed            = !isArmed;
                    previousIsArmed    = !previousIsArmed;
                    // currentEquipNT stays в†’ animation continues from same point
                }
                else
                {
                    ToggleWeaponState();
                }
                idleVariantLockedUntil = 0f;
                ResetIdleVariantTimer();
            }

            if (Keyboard.current.tKey.wasPressedThisFrame &&
                PlayNextLockedState(tauntStates, ref nextTauntIndex, tauntLockTime))
            {
                idleVariantLockedUntil = 0f;
                ResetIdleVariantTimer();
                return;
            }
        }

        if (Time.time < animationLockedUntil)
            return;

        // Block: not available during attack or equip (equip must finish first)
        if (blockHeld && isArmed && !isEquipping && currentStamina > 0f && standingBlockState.Exists
            && !isPlayingUpperBodyAttack)
        {
            if (!isBlocking || blockJustPressed)
                animator.Play(standingBlockState.Hash, UpperBodyLayer, 0f);

            isBlocking = true;
            idleVariantLockedUntil = 0f;
            ResetIdleVariantTimer();
        }
        else if (isBlocking && (!blockHeld || isPlayingUpperBodyAttack))
        {
            isBlocking = false;
        }

        if (isMoving)
        {
            idleVariantLockedUntil = 0f;
            bool useRunAnim  = isRunning && !isPlayingUpperBodyAttack && !isBlocking && !isEquipping;
            AnimationState locoState = useRunAnim  ? GetRunState()
                                     : isEquipping ? GetEquipWalkState()
                                                   : GetWalkState();

            float cycleDist = isBlocking              ? blockWalkCycleDistance
                            : isEquipping              ? equipWalkCycleDistance
                            : isPlayingUpperBodyAttack ? attackWalkCycleDistance
                            : useRunAnim               ? runCycleDistance
                                                       : walkCycleDistance;

            if (!locoState.Exists && !useRunAnim)
            {
                locoState = GetRunState();
                cycleDist = runCycleDistance;
            }

            if (!PlayLocomotionState(locoState, distanceMoved, cycleDist))
                PlayState(GetIdleState());

            ResetIdleVariantTimer();
            return;
        }

        if (Time.time >= nextIdleVariantTime && PlayNextIdleVariant())
        {
            nextIdleVariantTime = Time.time + idleVariantDelay + idleVariantLockTime;
            return;
        }

        if (Time.time >= idleVariantLockedUntil)
            PlayState(GetIdleState());
    }

    private bool ToggleWeaponState()
    {
        previousIsArmed     = isArmed;
        isArmed             = !isArmed;
        lockIsEquipOrDisarm = true;

        // Disarm = equip animation played backwards; equip = forward
        return PlayEquipAnimation(equipStates, ref nextEquipIndex, reverse: previousIsArmed);
    }

    private bool PlayEquipAnimation(AnimationState[] states, ref int nextIndex, bool reverse = false)
    {
        for (int i = 0; i < states.Length; i++)
        {
            int idx = (nextIndex + i) % states.Length;
            if (!states[idx].Exists) continue;

            nextIndex = (idx + 1) % states.Length;
            animator.SetLayerWeight(UpperBodyLayer, 1f);
            animator.CrossFade(states[idx].Hash, animationFadeTime, UpperBodyLayer, reverse ? 1f : 0f);
            isEquipping          = true;
            isEquippingReverse   = reverse;
            currentEquipHash     = states[idx].Hash;
            currentEquipDuration = states[idx].Duration > 0.1f ? states[idx].Duration : equipLockTime;
            currentEquipNT       = reverse ? 1f : 0f;
            return true;
        }
        return false;
    }

    private void CancelEquip()
    {
        if (!isEquipping) return;
        isArmed             = previousIsArmed;
        isEquipping         = false;
        lockIsEquipOrDisarm = false;
    }

    private AnimationState GetEquipWalkState() =>
        previousIsArmed
            ? standingWalkState
            : (unarmedWalkState.Exists ? unarmedWalkState : standingWalkState);

    private bool UseWalkSpeed() => !isRunning;

    private void PlayComboStep()
    {
        comboStep++;
        comboWindowOpen = false;
        comboBuffered = false;

        switch (comboStep)
        {
            case 1:
                PlayNextAttack(regularAttackStates, ref nextRegularAttackIndex, staminaCostPerAttack, attackLockTime);
                break;
            case 2:
                PlayNextAttack(combo2States, ref nextCombo2Index, staminaCostPerAttack, attackLockTime);
                break;
            case 3:
                PlayNextAttack(combo3States, ref nextCombo3Index, staminaCostPerAttack, attackLockTime);
                break;
            default:
                comboStep = 0;
                break;
        }
    }

    private void PlayNextAttack(AnimationState[] states, ref int nextIndex, float staminaCost, float lockTime)
    {
        if (currentStamina < staminaCost)
            return;

        for (int i = 0; i < states.Length; i++)
        {
            int index = (nextIndex + i) % states.Length;
            if (!states[index].Exists)
                continue;

            nextIndex = (index + 1) % states.Length;
            currentStamina -= staminaCost;
            staminaRegenDelayTimer = staminaRegenDelay;

            int layer = states[index].Layer;
            if (layer > 0)
            {
                animator.SetLayerWeight(layer, 1f);
                isPlayingUpperBodyAttack = true;
            }

            currentAttackStateHash = states[index].Hash;
            currentAttackDuration  = states[index].Duration > 0.1f ? states[index].Duration : 1f;
            attackStartTime        = Time.time;
            ScheduleMeleeHit(currentAttackDuration);
            animator.CrossFade(states[index].Hash, animationFadeTime, layer, 0f);
            attackLockedUntil = Time.time + 60f;
            return;
        }
    }

    private AnimationState GetIdleState() =>
        !isArmed && unarmedIdleState.Exists ? unarmedIdleState : standingIdleState;

    private AnimationState GetRunState() =>
        !isArmed && unarmedRunState.Exists ? unarmedRunState : standingRunState;

    private AnimationState GetWalkState() =>
        !isArmed && unarmedWalkState.Exists ? unarmedWalkState : standingWalkState;

    private bool PlayNextIdleVariant()
    {
        if (!isArmed && PlayIdleVariantState(unarmedIdleVariantStates, ref nextUnarmedIdleVariantIndex))
            return true;
        return PlayIdleVariantState(standingIdleVariantStates, ref nextStandingIdleVariantIndex);
    }

    private bool PlayIdleVariantState(AnimationState[] states, ref int nextIndex)
    {
        for (int i = 0; i < states.Length; i++)
        {
            int index = (nextIndex + i) % states.Length;
            if (!states[index].Exists)
                continue;

            nextIndex = (index + 1) % states.Length;
            float duration = states[index].Duration > 0.1f ? states[index].Duration : idleVariantLockTime;
            idleVariantLockedUntil = Time.time + duration - animationFadeTime;
            animator.CrossFade(states[index].Hash, animationFadeTime, 0, 0f);
            return true;
        }
        return false;
    }

    private void ResetIdleVariantTimer()
    {
        nextIdleVariantTime = Time.time + idleVariantDelay;
    }

    private bool PlayNextLockedState(AnimationState[] states, ref int nextIndex, float lockTime)
    {
        for (int i = 0; i < states.Length; i++)
        {
            int index = (nextIndex + i) % states.Length;
            if (!states[index].Exists)
                continue;

            nextIndex = (index + 1) % states.Length;
            return PlayLockedState(states[index], lockTime);
        }
        return false;
    }

    private bool PlayLockedState(AnimationState state, float lockTime)
    {
        if (!PlayState(state, true))
            return false;

        animationLockedUntil = Time.time + lockTime;
        return true;
    }

    private bool PlayState(AnimationState state, bool forceRestart = false)
    {
        if (!state.Exists)
        {
            animator.speed = 1f;
            currentDistanceMatchedStateHash = 0;
            return false;
        }

        animator.speed = 1f;
        currentDistanceMatchedStateHash = 0;

        if (!forceRestart)
        {
            int layer = state.Layer;
            bool isCurrent = animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == state.Hash;
            bool isNext    = animator.IsInTransition(layer) &&
                             animator.GetNextAnimatorStateInfo(layer).fullPathHash == state.Hash;
            if (isCurrent || isNext)
                return true;
        }

        animator.CrossFade(state.Hash, animationFadeTime, state.Layer, 0f);
        return true;
    }

    private bool PlayLocomotionState(AnimationState state, float distanceMoved, float cycleDistance)
    {
        if (!state.Exists)
            return false;

        // During block animator.speed must stay at 1 so the block animation isn't paused
        if (!distanceMatchLocomotion || isBlocking)
        {
            animator.speed = 1f;
            return PlayState(state);
        }

        if (currentDistanceMatchedStateHash != state.Hash)
            currentDistanceMatchedStateHash = state.Hash;

        locomotionDistance += distanceMoved;

        float normalizedTime = cycleDistance > 0.001f
            ? Mathf.Repeat(locomotionDistance / cycleDistance, 1f)
            : 0f;

        animator.speed = 0f;
        animator.Play(state.Hash, 0, normalizedTime);
        return true;
    }

    private bool CheckGrounded(out RaycastHit hit)
    {
        float radius = controller.radius * 0.9f;
        Vector3 origin = movementRoot.position + Vector3.up * (radius + 0.05f);
        return Physics.SphereCast(origin, radius, Vector3.down, out hit, 0.3f, groundMask);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!enableFootIK || animator == null)
            return;

        // Animated foot positions in world space (before any IK)
        Vector3 leftPos  = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 rightPos = animator.GetIKPosition(AvatarIKGoal.RightFoot);

        float leftGroundY  = SampleGroundY(leftPos);
        float rightGroundY = SampleGroundY(rightPos);

        // How high each foot is above ground (positive = above, negative = below)
        float leftAbove  = leftPos.y  - leftGroundY;
        float rightAbove = rightPos.y - rightGroundY;

        // Lower body so the foot that is least above ground can reach the ground.
        // Never raise body above its animated position (targetBodyOffset <= 0).
        float targetBodyOffset = -Mathf.Max(Mathf.Min(leftAbove, rightAbove), 0f);
        currentBodyOffset = Mathf.SmoothDamp(
            currentBodyOffset, targetBodyOffset, ref bodyOffsetVelocity, 0.08f);

        animator.bodyPosition += Vector3.up * currentBodyOffset;

        // After body shift, recalculate each foot's remaining distance to ground
        PlantFoot(AvatarIKGoal.LeftFoot,
            leftPos  + Vector3.up * currentBodyOffset,
            leftAbove  + currentBodyOffset,
            leftGroundY);

        PlantFoot(AvatarIKGoal.RightFoot,
            rightPos + Vector3.up * currentBodyOffset,
            rightAbove + currentBodyOffset,
            rightGroundY);
    }

    private float SampleGroundY(Vector3 fromPos)
    {
        Ray ray = new Ray(fromPos + Vector3.up * 0.5f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 0.5f + footRayDistance, groundMask))
            return hit.point.y + footVerticalOffset;
        return fromPos.y;
    }

    private void PlantFoot(AvatarIKGoal goal, Vector3 footPos, float aboveGround, float groundY)
    {
        animator.SetIKRotationWeight(goal, 0f);

        if (aboveGround > footPlantThreshold)
        {
            // Swing phase вЂ” foot is high, disable IK so knees move naturally
            animator.SetIKPositionWeight(goal, 0f);
            return;
        }

        float weight = 1f - Mathf.Clamp01(aboveGround / Mathf.Max(footPlantThreshold, 0.001f));
        animator.SetIKPositionWeight(goal, footIKWeight * weight);
        animator.SetIKPosition(goal, new Vector3(footPos.x, Mathf.Max(footPos.y, groundY), footPos.z));
    }


    private void ScheduleMeleeHit(float animationDuration)
    {
        meleeHitTime = Time.time + animationDuration * 0.45f;
        meleeHitPending = true;
    }

    private void CheckPendingMeleeHit()
    {
        if (!meleeHitPending || Time.time < meleeHitTime)
            return;

        meleeHitPending = false;

        PlayerStats ps = GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>();
        float damage = ps != null ? ps.damage : 10f;

        Vector3 origin = movementRoot.position + movementRoot.forward * meleeHitForwardOffset + Vector3.up;
        Collider[] hits = Physics.OverlapSphere(origin, meleeHitRadius, enemyLayer);

        foreach (Collider hit in hits)
        {
            EnemyStats enemy = hit.GetComponentInParent<EnemyStats>() ?? hit.GetComponent<EnemyStats>();
            if (enemy == null || enemy.IsDead)
                continue;

            RuinGuarder1AI ai = hit.GetComponentInParent<RuinGuarder1AI>() ?? hit.GetComponent<RuinGuarder1AI>();
            float reduction = ai != null ? damage * (1f - ai.BlockDamageMultiplier) : 0f;
            enemy.TakeDamage(damage, reduction);
        }
    }
    private void TryAssignAnimatorControllerInEditor()
    {
#if UNITY_EDITOR
        if (animator.runtimeAnimatorController != null)
            return;

        RuntimeAnimatorController ctrl =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerAuto.controller");

        if (ctrl == null)
            return;

        animator.runtimeAnimatorController = ctrl;
        EditorUtility.SetDirty(animator);
#endif
    }

    private static string SanitizeStateName(string stateName) => stateName.Replace('.', '_');
}
