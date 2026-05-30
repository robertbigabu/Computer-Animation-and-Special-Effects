using System.Collections.Generic;
using UnityEngine;
using RVO;

/// <summary>
/// Registers Unity BoxColliders tagged "RVOObstacle" as static ORCA obstacles
/// so agents are actively pushed away from walls (and get stuck at local minima
/// in Phase 2 rather than walking through them).
///
/// Execution order +50 ensures this runs AFTER RVOManager.Awake() (order 0),
/// which calls Simulator.Instance.Clear(). Obstacles must be registered after
/// Clear() or they get wiped. Start() on all scripts runs after all Awake()
/// calls, so RVOAgent.Start() (which adds agents) still runs after this.
///
/// Usage:
///   1. Tag each wall Cube as "RVOObstacle" (create the tag first in
///      Edit > Project Settings > Tags and Layers).
///   2. Add this component to the RVOManager GameObject (or any scene object).
/// </summary>
[DefaultExecutionOrder(50)]
public class RVOObstacleSetup : MonoBehaviour
{
    [Tooltip("Tag used to identify wall GameObjects. Create this tag in Project Settings > Tags and Layers.")]
    public string obstacleTag = "RVOObstacle";

    void Awake()
    {
        int count = 0;
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag(obstacleTag))
        {
            BoxCollider box = obj.GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning($"[RVOObstacleSetup] {obj.name} is tagged RVOObstacle but has no BoxCollider — skipped.");
                continue;
            }
            RegisterBox(box);
            count++;
        }

        // Must be called after all AddObstacle() calls and before DoStep().
        Simulator.Instance.ProcessObstacles();
        Debug.Log($"[RVOObstacleSetup] Registered {count} box obstacles.");
    }

    void RegisterBox(BoxCollider box)
    {
        Bounds b = box.bounds; // world-space AABB

        // Counter-clockwise winding when viewed from above (+Y),
        // so ORCA half-planes push agents to the outside of the box.
        var verts = new List<RVO.Vector2>
        {
            new RVO.Vector2(b.min.x, b.min.z),
            new RVO.Vector2(b.max.x, b.min.z),
            new RVO.Vector2(b.max.x, b.max.z),
            new RVO.Vector2(b.min.x, b.max.z),
        };

        Simulator.Instance.AddObstacle(verts);
    }
}
