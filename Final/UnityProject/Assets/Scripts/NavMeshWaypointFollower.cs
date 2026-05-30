using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives RVOAgent.Target toward the next NavMesh waypoint so that Phase 1's
/// PushToSimulator() feeds the correct preferred velocity without any changes
/// to Phase 1 code.
///
/// Arrival is detected here (not via RVOAgent.HasArrived) by measuring the
/// horizontal distance directly to FinalTarget. This keeps arrival consistent
/// with the red Gizmo circle drawn by MazeAgentSetup regardless of NavMesh
/// snapping offsets.
///
/// Execution order -50 ensures Target is updated before RVOManager.FixedUpdate
/// reads it (default order 0).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(RVOAgent))]
public class NavMeshWaypointFollower : MonoBehaviour
{
    [Header("Navigation")]
    [Tooltip("Ultimate world-space destination.")]
    public Vector3 FinalTarget;

    [Tooltip("True = NavMesh + ORCA (Phase 3). False = direct line (Phase 2 failure demo).")]
    public bool useNavMesh = true;

    [Tooltip("After arriving, respawn at the original start position and repeat indefinitely. " +
             "Leave false if MazeAgentSetup controls the group reset.")]
    public bool loop = false;

    [Tooltip("Horizontal distance to FinalTarget that counts as 'arrived'. " +
             "Should match MazeAgentSetup.arrivalThreshold so the red circle is accurate.")]
    public float arrivalRadius = 1.5f;

    [Tooltip("Distance to current waypoint corner before advancing to the next one.")]
    public float waypointReachDist = 1.2f;

    [Tooltip("Recalculate NavMesh path every N FixedUpdate ticks.")]
    public int pathRefreshInterval = 6;

    // ─── Public state ─────────────────────────────────────────────────
    /// <summary>True once the agent enters the arrivalRadius around FinalTarget.</summary>
    public bool HasArrived { get; private set; }

    // ─── Runtime ──────────────────────────────────────────────────────
    private RVOAgent _agent;
    private NavMeshPath _path;
    private int _waypointIdx;
    private int _refreshCountdown;

    private Vector3 _endpointA; // spawn position
    private Vector3 _endpointB; // initial FinalTarget

    void Start()
    {
        _agent = GetComponent<RVOAgent>();
        _path = new NavMeshPath();
        _endpointA = transform.position;
        _endpointB = FinalTarget;
        RefreshPath();
    }

    void FixedUpdate()
    {
        // ── Arrival check against FinalTarget directly ─────────────────
        // Only mark HasArrived — do NOT stop the agent here.
        // RVOAgent naturally decelerates as it approaches FinalTarget
        // (prefSpeed = Min(dist, 1)), so agents settle in the centre
        // through ORCA rather than freezing at the perimeter.
        HasArrived = HorizontalDist(transform.position, FinalTarget) < arrivalRadius;

        if (HasArrived && loop)
        {
            RestartFromSpawn();
            return;
        }

        // ── Waypoint following ─────────────────────────────────────────
        if (!useNavMesh)
        {
            _agent.Target = FinalTarget;
            return;
        }

        if (--_refreshCountdown <= 0)
            RefreshPath();

        if (_path.corners.Length == 0)
        {
            _agent.Target = FinalTarget;
            return;
        }

        // Advance past corners we have already reached.
        Vector3 pos = transform.position;
        while (_waypointIdx < _path.corners.Length - 1 &&
               HorizontalDist(pos, _path.corners[_waypointIdx]) < waypointReachDist)
        {
            _waypointIdx++;
        }

        _agent.Target = _path.corners[_waypointIdx];
    }

    /// <summary>Teleport back to the original spawn position and restart the
    /// path toward the original goal. Called by MazeAgentSetup for group resets.
    /// </summary>
    public void RestartFromSpawn()
    {
        HasArrived = false;
        FinalTarget = _endpointB;
        _agent.Respawn(_endpointA);
        RefreshPath();
    }

    void RefreshPath()
    {
        NavMesh.CalculatePath(transform.position, FinalTarget, NavMesh.AllAreas, _path);
        _waypointIdx = 0;
        _refreshCountdown = pathRefreshInterval;
    }

    static float HorizontalDist(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void OnDrawGizmosSelected()
    {
        if (_path == null || _path.corners.Length == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < _path.corners.Length - 1; i++)
            Gizmos.DrawLine(_path.corners[i], _path.corners[i + 1]);
        if (_waypointIdx < _path.corners.Length)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_path.corners[_waypointIdx], 0.2f);
        }
    }
}
