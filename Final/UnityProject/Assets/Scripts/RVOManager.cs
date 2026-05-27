using System.Collections.Generic;
using UnityEngine;
using RVO;

/// <summary>
/// Singleton that owns the RVO Simulator instance and drives the full
/// simulation loop in a single FixedUpdate — no execution-order ambiguity.
///
/// Loop (every FixedUpdate):
///   1. For each registered agent: push Unity position + preferred velocity → RVO.
///   2. Simulator.DoStep() (LP solver runs).
///   3. For each registered agent: read back computed velocity.
///
/// Agents register themselves via <see cref="Register"/> in their Start().
/// Movement (applying velocity to Transform) happens in each agent's Update().
///
/// 2D ↔ 3D mapping:  RVO (x,y) == Unity (x,z).  Unity Y is up (ignored by RVO).
/// </summary>
public class RVOManager : MonoBehaviour
{
    public static RVOManager Instance { get; private set; }

    [Header("Simulation Defaults")]
    [Tooltip("How far each agent scans for neighbors (center-to-center).")]
    public float neighborDist = 15f;

    [Tooltip("Max neighbors considered per agent. Higher = safer but slower.")]
    public int maxNeighbors = 10;

    [Tooltip("Time horizon for agent-agent avoidance (seconds). Bigger = earlier reaction. Use 2-3 for tight scenes, 10+ for wide open spaces.")]
    public float timeHorizon = 2.5f;

    [Tooltip("Time horizon for obstacle avoidance (seconds).")]
    public float timeHorizonObst = 2.5f;

    [Tooltip("Default agent collision radius.")]
    public float agentRadius = 0.4f;

    [Tooltip("Default max speed.")]
    public float maxSpeed = 2f;

    [Header("Phase 1: Kinematic Constraints")]
    [Tooltip("Max acceleration (m/s²). 0 = disabled (original ORCA behavior). Try 3-5 for noticeable smoothing.")]
    public float maxAccel = 4f;

    [Tooltip("Max angular velocity (rad/s). 0 = disabled. π ≈ 3.14 = half turn per second. Try 2-4.")]
    public float maxAngularVel = 3f;

    // ─── Agent registry ────────────────────────────────────────────
    private readonly List<RVOAgent> _agents = new();

    /// <summary>Called by RVOAgent.Start() to join the simulation loop.</summary>
    public void Register(RVOAgent agent)
    {
        if (!_agents.Contains(agent))
            _agents.Add(agent);
    }

    /// <summary>Called by RVOAgent.OnDestroy() to leave the loop.</summary>
    public void Unregister(RVOAgent agent)
    {
        _agents.Remove(agent);
    }

    // ─── Lifecycle ─────────────────────────────────────────────────
    void Awake()
    {
        // Singleton guard
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Clear any leftover state from a previous Play-mode session.
        Simulator.Instance.Clear();

        // Tell RVO what "default" agents look like.
        Simulator.Instance.SetAgentDefaults(
            neighborDist,
            maxNeighbors,
            timeHorizon,
            timeHorizonObst,
            agentRadius,
            maxSpeed,
            new RVO.Vector2(0f, 0f)   // initial velocity = zero
        );

        // Match RVO timestep to Unity's FixedUpdate rate.
        Simulator.Instance.TimeStep = Time.fixedDeltaTime;

        // Single-threaded (safe; 8–16 agents don't need parallelism).
        Simulator.Instance.NumWorkers = 1;
    }

    void FixedUpdate()
    {
        // ── Phase 1: all agents set preferred velocity ──
        for (int i = 0; i < _agents.Count; i++)
        {
            _agents[i].PushToSimulator();
        }

        // ── Phase 2: run the LP solver (RVO moves positions internally) ──
        Simulator.Instance.DoStep();

        // ── Phase 3: all agents read back position + velocity ──
        for (int i = 0; i < _agents.Count; i++)
        {
            _agents[i].ReadFromSimulator();
        }
    }

    void OnDestroy()
    {
        Simulator.Instance.Clear();
        _agents.Clear();
        if (Instance == this) Instance = null;
    }

    // ─── Helpers for 2D ↔ 3D conversion ───────────────────────────
    /// <summary>Unity Vector3 (x,_,z) → RVO Vector2 (x,y).</summary>
    public static RVO.Vector2 ToRVO(UnityEngine.Vector3 v)
    {
        return new RVO.Vector2(v.x, v.z);
    }

    /// <summary>RVO Vector2 (x,y) → Unity Vector3 (x,0,z).</summary>
    public static UnityEngine.Vector3 ToUnity(RVO.Vector2 v)
    {
        return new UnityEngine.Vector3(v.X, 0f, v.Y);
    }
}
