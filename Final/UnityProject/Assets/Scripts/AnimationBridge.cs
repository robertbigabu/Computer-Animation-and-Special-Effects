using UnityEngine;

/// <summary>
/// Phase 1 Animation Bridge: converts the RVO-constrained world-space velocity
/// into local-space Animator parameters (MoveX, MoveY) that drive a 2D Blend Tree.
///
/// Attach to the same GameObject that has the Animator (the character model).
/// The RVOAgent can be on this object or a parent — set the reference in Inspector.
///
/// Blend Tree expected layout (2D Freeform Cartesian):
///   MoveX = local strafe  (-1 = left,  +1 = right)
///   MoveY = local forward  (-1 = back,  +1 = forward, +2 = run)
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RVOAgent driving this character. Auto-detected on this or parent if null.")]
    public RVOAgent rvoAgent;

    [Header("Speed Mapping")]
    [Tooltip("World speed (m/s) that maps to MoveY=1 (walk). Measure from your walk clip.")]
    public float walkSpeed = 1.0f;

    [Tooltip("World speed (m/s) that maps to MoveY=2 (run). Measure from your run clip.")]
    public float runSpeed = 2.0f;

    [Header("Smoothing")]
    [Tooltip("Damping time for blend tree transitions. Lower = snappier, higher = smoother.")]
    public float dampTime = 0.1f;

    // Animator parameter hashes (faster than string lookups).
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();

        if (rvoAgent == null)
            rvoAgent = GetComponentInParent<RVOAgent>();

        if (rvoAgent == null)
            Debug.LogError($"[AnimationBridge] {name}: No RVOAgent found on this object or parents!");
    }

    void Update()
    {
        if (rvoAgent == null || _animator == null) return;

        Vector3 worldVel = rvoAgent.CurrentVelocity;
        float worldSpeed = worldVel.magnitude;

        // ── Convert world velocity to character-local space ──
        // localVel.x = strafe (positive = right)
        // localVel.z = forward (positive = forward)
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        // ── Normalize to blend tree range ──
        // MoveX: strafe component / walkSpeed → [-1, +1]
        // MoveY: forward component / walkSpeed → [-1 = back, +1 = walk, +2 = run]
        float moveX = localVel.x / Mathf.Max(walkSpeed, 0.01f);
        float moveY = localVel.z / Mathf.Max(walkSpeed, 0.01f);

        // ── Feed into Animator with smoothing ──
        _animator.SetFloat(MoveXHash, moveX, dampTime, Time.deltaTime);
        _animator.SetFloat(MoveYHash, moveY, dampTime, Time.deltaTime);

        // Optional: raw speed for triggers/transitions.
        _animator.SetFloat(SpeedHash, worldSpeed);
    }
}
