using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool startsArmed = true;
    [SerializeField] private float animationFadeTime = 0.15f;
    [SerializeField] private float jumpLockTime = 0.75f;
    [SerializeField] private float attackLockTime = 0.8f;
    [SerializeField] private float equipLockTime = 1f;
    [SerializeField] private float tauntLockTime = 1.4f;
    [SerializeField] private float hitLockTime = 0.8f;
    [SerializeField] private float idleVariantDelay = 4f;
    [SerializeField] private float idleVariantLockTime = 2f;

    [Header("Distance Matching")]
    [SerializeField] private bool distanceMatchLocomotion = true;
    [SerializeField] private float runCycleDistance = 2.4f;
    [SerializeField] private float walkCycleDistance = 1.4f;

    [Header("Standing States")]
    [SerializeField] private string standingIdleStateName = "standing idle";
    [SerializeField] private string standingRunStateName = "standing run forward";
    [SerializeField] private string standingWalkStateName = "standing walk forward";
    [SerializeField] private string standingJumpStateName = "standing jump";
    [SerializeField] private string standingBlockStateName = "standing block idle";
    [SerializeField] private string crouchStateName = "crouch idle";
    [SerializeField] private string crouchToStandStateName = "crouch to standing idle";

    [Header("Unarmed States")]
    [SerializeField] private string unarmedIdleStateName = "unarmed idle";
    [SerializeField] private string unarmedRunStateName = "unarmed run forward";
    [SerializeField] private string unarmedWalkStateName = "unarmed walk forward";
    [SerializeField] private string unarmedJumpStateName = "unarmed jump";

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
    [SerializeField] private string[] standingAttackStateNames =
    {
        "standing melee attack horizontal",
        "standing melee attack downward",
        "standing melee attack backhand",
        "standing melee attack 360 high",
        "standing melee attack 360 low",
        "standing melee attack kick ver. 1",
        "standing melee attack kick ver. 2",
        "standing melee combo attack ver. 1",
        "standing melee combo attack ver. 2",
        "standing melee combo attack ver. 3",
        "standing melee run jump attack"
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

    private CharacterController controller;
    private PlayerControls controls;
    private Transform movementRoot;
    private Vector2 moveInput;
    private bool isArmed;
    private bool wasCrouching;
    private float animationLockedUntil;
    private int nextAttackIndex;
    private int nextTauntIndex;
    private int nextHitIndex;
    private int nextEquipIndex;
    private int nextDisarmIndex;
    private int nextStandingIdleVariantIndex;
    private int nextUnarmedIdleVariantIndex;
    private float nextIdleVariantTime;
    private float locomotionDistance;
    private int currentDistanceMatchedStateHash;

    private AnimationState standingIdleState;
    private AnimationState standingRunState;
    private AnimationState standingWalkState;
    private AnimationState standingJumpState;
    private AnimationState standingBlockState;
    private AnimationState crouchState;
    private AnimationState crouchToStandState;
    private AnimationState unarmedIdleState;
    private AnimationState unarmedRunState;
    private AnimationState unarmedWalkState;
    private AnimationState unarmedJumpState;
    private AnimationState[] standingIdleVariantStates;
    private AnimationState[] unarmedIdleVariantStates;
    private AnimationState[] standingAttackStates;
    private AnimationState[] tauntStates;
    private AnimationState[] hitStates;
    private AnimationState[] equipStates;
    private AnimationState[] disarmStates;

    private struct AnimationState
    {
        public int Hash;
        public bool Exists;
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

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        Quaternion isoRotation = Quaternion.Euler(0f, 45f, 0f);
        move = isoRotation * move;

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        if (move != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            movementRoot.rotation = Quaternion.Slerp(
                movementRoot.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        Vector3 positionBeforeMove = movementRoot.position;

        if (controller != null)
        {
            controller.Move(move * moveSpeed * Time.deltaTime);
        }
        else
        {
            movementRoot.position += move * moveSpeed * Time.deltaTime;
        }

        float distanceMoved = Vector3.ProjectOnPlane(
            movementRoot.position - positionBeforeMove,
            Vector3.up
        ).magnitude;

        UpdateAnimation(move.sqrMagnitude > 0.0001f, distanceMoved);
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

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
        standingJumpState = MakeState(standingJumpStateName);
        standingBlockState = MakeState(standingBlockStateName);
        crouchState = MakeState(crouchStateName);
        crouchToStandState = MakeState(crouchToStandStateName);
        unarmedIdleState = MakeState(unarmedIdleStateName);
        unarmedRunState = MakeState(unarmedRunStateName);
        unarmedWalkState = MakeState(unarmedWalkStateName);
        unarmedJumpState = MakeState(unarmedJumpStateName);
        standingIdleVariantStates = MakeStates(standingIdleVariantStateNames);
        unarmedIdleVariantStates = MakeStates(unarmedIdleVariantStateNames);
        standingAttackStates = MakeStates(standingAttackStateNames);
        tauntStates = MakeStates(tauntStateNames);
        hitStates = MakeStates(hitStateNames);
        equipStates = MakeStates(equipStateNames);
        disarmStates = MakeStates(disarmStateNames);
    }

    private AnimationState MakeState(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return default;
        }

        int hash = Animator.StringToHash($"Base Layer.{SanitizeStateName(stateName)}");

        return new AnimationState
        {
            Hash = hash,
            Exists = animator.HasState(0, hash)
        };
    }

    private AnimationState[] MakeStates(string[] stateNames)
    {
        AnimationState[] states = new AnimationState[stateNames.Length];

        for (int i = 0; i < stateNames.Length; i++)
        {
            states[i] = MakeState(stateNames[i]);
        }

        return states;
    }

    private void UpdateAnimation(bool isMoving, float distanceMoved)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        bool crouchHeld = Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);
        bool blockHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame && ToggleWeaponState())
            {
                ResetIdleVariantTimer();
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame && PlayLockedState(GetJumpState(), jumpLockTime))
            {
                ResetIdleVariantTimer();
                return;
            }

            if (Keyboard.current.tKey.wasPressedThisFrame &&
                PlayNextLockedState(tauntStates, ref nextTauntIndex, tauntLockTime))
            {
                ResetIdleVariantTimer();
                return;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            PlayNextLockedState(standingAttackStates, ref nextAttackIndex, attackLockTime))
        {
            ResetIdleVariantTimer();
            return;
        }

        if (Time.time < animationLockedUntil)
        {
            return;
        }

        if (blockHeld && isArmed && PlayState(standingBlockState))
        {
            ResetIdleVariantTimer();
            return;
        }

        if (crouchHeld && PlayState(crouchState))
        {
            wasCrouching = true;
            ResetIdleVariantTimer();
            return;
        }

        if (wasCrouching)
        {
            wasCrouching = false;

            if (PlayLockedState(crouchToStandState, jumpLockTime))
            {
                return;
            }
        }

        if (isMoving)
        {
            bool isWalking = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            bool playedLocomotion = PlayLocomotionState(
                isWalking ? GetWalkState() : GetRunState(),
                distanceMoved,
                isWalking ? walkCycleDistance : runCycleDistance
            );

            if (!playedLocomotion)
            {
                PlayState(GetIdleState());
            }

            ResetIdleVariantTimer();
            return;
        }

        if (Time.time >= nextIdleVariantTime && PlayNextIdleVariant())
        {
            nextIdleVariantTime = Time.time + idleVariantDelay + idleVariantLockTime;
            return;
        }

        PlayState(GetIdleState());
    }

    private bool ToggleWeaponState()
    {
        bool wasArmed = isArmed;
        isArmed = !isArmed;

        if (wasArmed)
        {
            return PlayNextLockedState(disarmStates, ref nextDisarmIndex, equipLockTime);
        }

        return PlayNextLockedState(equipStates, ref nextEquipIndex, equipLockTime);
    }

    private AnimationState GetIdleState()
    {
        if (!isArmed && unarmedIdleState.Exists)
        {
            return unarmedIdleState;
        }

        return standingIdleState;
    }

    private AnimationState GetRunState()
    {
        if (!isArmed && unarmedRunState.Exists)
        {
            return unarmedRunState;
        }

        return standingRunState;
    }

    private AnimationState GetWalkState()
    {
        if (!isArmed && unarmedWalkState.Exists)
        {
            return unarmedWalkState;
        }

        return standingWalkState;
    }

    private AnimationState GetJumpState()
    {
        if (!isArmed && unarmedJumpState.Exists)
        {
            return unarmedJumpState;
        }

        return standingJumpState;
    }

    private bool PlayNextIdleVariant()
    {
        if (!isArmed && PlayNextLockedState(unarmedIdleVariantStates, ref nextUnarmedIdleVariantIndex, idleVariantLockTime))
        {
            return true;
        }

        return PlayNextLockedState(standingIdleVariantStates, ref nextStandingIdleVariantIndex, idleVariantLockTime);
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
            {
                continue;
            }

            nextIndex = (index + 1) % states.Length;
            return PlayLockedState(states[index], lockTime);
        }

        return false;
    }

    private bool PlayLockedState(AnimationState state, float lockTime)
    {
        if (!PlayState(state, true))
        {
            return false;
        }

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

        if (forceRestart || animator.GetCurrentAnimatorStateInfo(0).fullPathHash != state.Hash)
        {
            animator.CrossFade(state.Hash, animationFadeTime, 0);
        }

        return true;
    }

    private bool PlayLocomotionState(AnimationState state, float distanceMoved, float cycleDistance)
    {
        if (!state.Exists)
        {
            return false;
        }

        if (!distanceMatchLocomotion)
        {
            animator.speed = 1f;
            return PlayState(state);
        }

        if (currentDistanceMatchedStateHash != state.Hash)
        {
            currentDistanceMatchedStateHash = state.Hash;
        }

        locomotionDistance += distanceMoved;

        float normalizedTime = cycleDistance > 0.001f
            ? Mathf.Repeat(locomotionDistance / cycleDistance, 1f)
            : 0f;

        animator.speed = 0f;
        animator.Play(state.Hash, 0, normalizedTime);
        return true;
    }

    private void TryAssignAnimatorControllerInEditor()
    {
#if UNITY_EDITOR
        if (animator.runtimeAnimatorController != null)
        {
            return;
        }

        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerAuto.controller");

        if (controller == null)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animator);
#endif
    }

    private static string SanitizeStateName(string stateName)
    {
        return stateName.Replace('.', '_');
    }
}
