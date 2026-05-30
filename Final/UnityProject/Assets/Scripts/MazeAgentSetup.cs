using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns one group of agents and wires up NavMeshWaypointFollower on each one.
/// Place multiple instances in the scene for multiple groups — they all share the
/// same RVOManager singleton so ORCA handles inter-group collision avoidance.
///
/// UI, arrival detection and group reset are handled by MazeSceneManager.
/// Press N to toggle NavMesh mode on all groups simultaneously (via MazeSceneManager).
/// </summary>
public class MazeAgentSetup : MonoBehaviour
{
    [Header("Agent Prefab")]
    [Tooltip("ConstrainedRVOCharacter.prefab — must have RVOAgent component.")]
    public GameObject agentPrefab;

    [Header("Spawn Grid")]
    public Vector3 spawnCenter = new Vector3(-8f, 0f, 0f);
    public int spawnRows = 3;
    public int spawnCols = 3;
    public float spawnSpacing = 1.2f;

    [Header("Goal")]
    public Vector3 goalCenter = new Vector3(8f, 0f, 0f);

    [Tooltip("Radius around each agent's individual goal that counts as arrived.")]
    public float arrivalThreshold = 1.5f;

    [Header("NavMesh Mode")]
    [Tooltip("Start in Phase 3 (NavMesh) mode. MazeSceneManager can toggle all groups together.")]
    public bool useNavMesh = true;

    // ─── Runtime ──────────────────────────────────────────────────────
    private readonly List<NavMeshWaypointFollower> _followers = new();

    /// <summary>Read-only view of all followers in this group.</summary>
    public IReadOnlyList<NavMeshWaypointFollower> Followers => _followers;

    /// <summary>Number of agents whose HasArrived == true this frame.</summary>
    public int ArrivedCount
    {
        get
        {
            int n = 0;
            foreach (var f in _followers)
                if (f != null && f.HasArrived) n++;
            return n;
        }
    }

    void Start()
    {
        if (agentPrefab == null)
        {
            Debug.LogError($"[MazeAgentSetup] {name}: agentPrefab is not assigned.");
            return;
        }

        for (int row = 0; row < spawnRows; row++)
        {
            for (int col = 0; col < spawnCols; col++)
            {
                Vector3 offset = new Vector3(
                    (col - (spawnCols - 1) * 0.5f) * spawnSpacing,
                    0f,
                    (row - (spawnRows - 1) * 0.5f) * spawnSpacing
                );

                GameObject go = Instantiate(agentPrefab, spawnCenter + offset, Quaternion.identity);
                go.name = $"{name}_Agent_{row}_{col}";

                NavMeshWaypointFollower follower = go.AddComponent<NavMeshWaypointFollower>();
                follower.FinalTarget   = goalCenter + offset;
                follower.useNavMesh    = useNavMesh;
                follower.arrivalRadius = arrivalThreshold;

                _followers.Add(follower);
            }
        }
    }

    /// <summary>Flip NavMesh mode for every agent in this group.</summary>
    public void SetNavMesh(bool on)
    {
        useNavMesh = on;
        foreach (var f in _followers)
            if (f != null) f.useNavMesh = on;
    }

    /// <summary>Teleport all agents back to their spawn positions.</summary>
    public void RestartAll()
    {
        foreach (var f in _followers)
            if (f != null) f.RestartFromSpawn();
    }

    // ─── Gizmos ───────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (_followers.Count > 0)
        {
            foreach (var f in _followers)
                if (f != null) DrawCircle(f.FinalTarget, arrivalThreshold);
            return;
        }

        for (int row = 0; row < spawnRows; row++)
        {
            for (int col = 0; col < spawnCols; col++)
            {
                Vector3 offset = new Vector3(
                    (col - (spawnCols - 1) * 0.5f) * spawnSpacing,
                    0f,
                    (row - (spawnRows - 1) * 0.5f) * spawnSpacing
                );
                DrawCircle(goalCenter + offset, arrivalThreshold);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        for (int row = 0; row < spawnRows; row++)
        {
            for (int col = 0; col < spawnCols; col++)
            {
                Vector3 offset = new Vector3(
                    (col - (spawnCols - 1) * 0.5f) * spawnSpacing,
                    0f,
                    (row - (spawnRows - 1) * 0.5f) * spawnSpacing
                );
                Gizmos.DrawWireSphere(spawnCenter + offset, 0.3f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(spawnCenter + offset, goalCenter + offset);
                Gizmos.color = Color.blue;
            }
        }
    }

    static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        Gizmos.color = Color.red;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(
                Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        float c = radius * 0.15f;
        Gizmos.DrawLine(center - Vector3.right * c,   center + Vector3.right * c);
        Gizmos.DrawLine(center - Vector3.forward * c, center + Vector3.forward * c);
    }
}
