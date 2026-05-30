using UnityEngine;
using RVO;

/// <summary>
/// MonoBehaviour wrapper for a single RVO agent.
///
/// Position is owned by the RVO simulator (matching the official Circle.cs
/// example).  Each frame we just set preferred velocity and read back the
/// resulting position — no two-way sync issues.
/// </summary>
public class RVOAgent : MonoBehaviour
{
    // ─── Public / Inspector ────────────────────────────────────────
    [Header("Navigation")]
    [Tooltip("World-space target the agent walks toward.")]
    public UnityEngine.Vector3 Target;

    [Tooltip("Agent considers itself 'arrived' within this distance of the target.")]
    public float arrivalThreshold = 0.5f;

    [Header("RVO Overrides (leave 0 to use RVOManager defaults)")]
    public float maxSpeedOverride = 0f;
    public float radiusOverride   = 0f;

    [Header("Kinematic Overrides (-1 = use RVOManager defaults, 0 = disabled)")]
    public float maxAccelOverride      = -1f;
    public float maxAngularVelOverride = -1f;

    // ─── Runtime state ─────────────────────────────────────────────
    [HideInInspector] public int AgentId { get; private set; } = -1;

    /// <summary>Last velocity computed by the LP solver (world space).</summary>
    public UnityEngine.Vector3 CurrentVelocity { get; private set; }

    /// <summary>True once the agent is within arrivalThreshold of its target.</summary>
    public bool HasArrived { get; private set; }

    // ─── Lifecycle ─────────────────────────────────────────────────
    void Start()
    {
        RVO.Vector2 pos2D = RVOManager.ToRVO(transform.position);

        float speed  = maxSpeedOverride > 0f ? maxSpeedOverride : RVOManager.Instance.maxSpeed;
        float radius = radiusOverride   > 0f ? radiusOverride   : RVOManager.Instance.agentRadius;

        AgentId = Simulator.Instance.AddAgent(
            pos2D,
            RVOManager.Instance.neighborDist,
            RVOManager.Instance.maxNeighbors,
            RVOManager.Instance.timeHorizon,
            RVOManager.Instance.timeHorizonObst,
            radius,
            speed,
            new RVO.Vector2(0f, 0f)
        );

        // Apply kinematic constraints.
        // -1 = use manager default, 0 = explicitly disabled, >0 = custom value.
        float accel  = maxAccelOverride >= 0f ? maxAccelOverride : RVOManager.Instance.maxAccel;
        float angVel = maxAngularVelOverride >= 0f ? maxAngularVelOverride : RVOManager.Instance.maxAngularVel;
        Simulator.Instance.SetAgentMaxAccel(AgentId, accel);
        Simulator.Instance.SetAgentMaxAngularVel(AgentId, angVel);

        Debug.Log($"[RVOAgent] {name} registered: id={AgentId}, accel={accel}, angVel={angVel}");

        if (RVOManager.Instance != null)
            RVOManager.Instance.Register(this);
        else
            Debug.LogError($"[RVOAgent] {name}: RVOManager.Instance is null!");
    }

    // ─── Called by RVOManager each FixedUpdate ─────────────────────

    /// <summary>Set preferred velocity BEFORE DoStep.</summary>
    public void PushToSimulator()
    {
        if (AgentId < 0) return;

        // Read current position FROM RVO (RVO owns position).
        RVO.Vector2 rvoPos = Simulator.Instance.GetAgentPosition(AgentId);
        UnityEngine.Vector3 currentPos = RVOManager.ToUnity(rvoPos);
        currentPos.y = transform.position.y; // preserve Y height

        UnityEngine.Vector3 toTarget = Target - currentPos;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;

        if (dist < arrivalThreshold)
        {
            Simulator.Instance.SetAgentPrefVelocity(AgentId, new RVO.Vector2(0f, 0f));
            HasArrived = true;
        }
        else
        {
            // Preferred speed = maxSpeed, decelerate smoothly within the last
            // maxSpeed metres so the agent doesn't overshoot the target.
            float maxSpd   = maxSpeedOverride > 0f
                ? maxSpeedOverride
                : RVOManager.Instance.maxSpeed;
            float prefSpeed = Mathf.Min(dist, maxSpd);
            UnityEngine.Vector3 prefDir = toTarget / dist;
            RVO.Vector2 prefVel = new RVO.Vector2(
                prefDir.x * prefSpeed,
                prefDir.z * prefSpeed
            );

            // Small perturbation to break symmetric deadlocks.
            float angle = Random.Range(0f, 2f * Mathf.PI);
            prefVel += new RVO.Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.01f;

            Simulator.Instance.SetAgentPrefVelocity(AgentId, prefVel);
            HasArrived = false;
        }
    }

    /// <summary>Read back position + velocity AFTER DoStep.</summary>
    public void ReadFromSimulator()
    {
        if (AgentId < 0) return;

        // Read position from RVO (RVO owns it — we just mirror to Unity).
        RVO.Vector2 rvoPos = Simulator.Instance.GetAgentPosition(AgentId);
        UnityEngine.Vector3 newPos = RVOManager.ToUnity(rvoPos);
        newPos.y = transform.position.y; // keep the capsule's Y height
        transform.position = newPos;

        // Read velocity for animation bridge (Phase 1 later use).
        RVO.Vector2 rvoVel = Simulator.Instance.GetAgentVelocity(AgentId);
        CurrentVelocity = RVOManager.ToUnity(rvoVel);

        // Face movement direction.
        if (CurrentVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = UnityEngine.Quaternion.Slerp(
                transform.rotation,
                UnityEngine.Quaternion.LookRotation(CurrentVelocity),
                10f * Time.fixedDeltaTime
            );
        }
    }

    /// <summary>Teleport this agent to a new world position and zero its velocity.
    /// Updates both the Unity Transform and RVO's internal state so the agent
    /// starts fresh from the new location on the very next simulation step.</summary>
    public void Respawn(UnityEngine.Vector3 worldPos)
    {
        transform.position = worldPos;
        Simulator.Instance.SetAgentPosition(AgentId, RVOManager.ToRVO(worldPos));
        CurrentVelocity = UnityEngine.Vector3.zero;
        HasArrived = false;
    }

    void OnDestroy()
    {
        if (RVOManager.Instance != null)
            RVOManager.Instance.Unregister(this);
        AgentId = -1;
    }

    // ─── Gizmos ────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, Target);
        Gizmos.DrawWireSphere(Target, 0.2f);
    }
}
