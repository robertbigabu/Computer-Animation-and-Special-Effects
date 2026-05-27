using UnityEngine;

/// <summary>
/// Spawns a small set of agents in non-symmetric positions with clear targets.
/// Designed to verify ORCA works without symmetric deadlock issues.
///
/// Scenarios (pick via Inspector):
///   - HeadOn:     2 agents walking straight at each other
///   - FourWay:    4 agents from N/S/E/W offset positions, crossing center
///   - Staggered8: 8 agents with slight angular offsets to break symmetry
/// </summary>
public class CircleCrossingSetup : MonoBehaviour
{
    public enum Scenario { HeadOn, FourWay, Staggered8 }

    [Header("Configuration")]
    public Scenario scenario = Scenario.FourWay;

    [Tooltip("How far agents spawn from center.")]
    public float spawnRadius = 6f;

    void Start()
    {
        switch (scenario)
        {
            case Scenario.HeadOn:
                SpawnAgent(new Vector3(-spawnRadius, 0, 0), new Vector3(spawnRadius, 0, 0), 0);
                SpawnAgent(new Vector3(spawnRadius, 0, 0.3f), new Vector3(-spawnRadius, 0, 0), 1);
                break;

            case Scenario.FourWay:
                // 4 agents from cardinal directions, slightly offset to break symmetry
                SpawnAgent(new Vector3(-spawnRadius, 0, 0.2f),  new Vector3(spawnRadius, 0, 0),   0);
                SpawnAgent(new Vector3(spawnRadius, 0, -0.3f),  new Vector3(-spawnRadius, 0, 0),   1);
                SpawnAgent(new Vector3(0, 0, -spawnRadius),     new Vector3(0.2f, 0, spawnRadius), 2);
                SpawnAgent(new Vector3(0.3f, 0, spawnRadius),   new Vector3(0, 0, -spawnRadius),   3);
                break;

            case Scenario.Staggered8:
                for (int i = 0; i < 8; i++)
                {
                    // Add a per-agent angular offset to break perfect symmetry
                    float angle = 2f * Mathf.PI * i / 8f + (i % 2 == 0 ? 0.1f : -0.05f);
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle) * spawnRadius, 0,
                        Mathf.Sin(angle) * spawnRadius
                    );
                    Vector3 target = new Vector3(
                        Mathf.Cos(angle + Mathf.PI) * spawnRadius, 0,
                        Mathf.Sin(angle + Mathf.PI) * spawnRadius
                    );
                    SpawnAgent(pos, target, i);
                }
                break;
        }

        Debug.Log($"[Setup] Spawned {scenario} scenario, spawnRadius={spawnRadius}m");
    }

    void SpawnAgent(Vector3 position, Vector3 target, int index)
    {
        position.y = 0.5f;
        target.y   = 0.5f;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Agent_{index:00}";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
        Destroy(go.GetComponent<Collider>());

        // Distinct color per agent
        Renderer rend = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Standard")
                       ?? Shader.Find("Unlit/Color"));
        float hue = (float)index / Mathf.Max(8, index + 1);
        mat.color = Color.HSVToRGB(hue, 0.8f, 0.9f);
        rend.material = mat;

        RVOAgent agent = go.AddComponent<RVOAgent>();
        agent.Target = target;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a1 = 2f * Mathf.PI * i / segments;
            float a2 = 2f * Mathf.PI * (i + 1) / segments;
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * spawnRadius, 0, Mathf.Sin(a1) * spawnRadius);
            Vector3 p2 = new Vector3(Mathf.Cos(a2) * spawnRadius, 0, Mathf.Sin(a2) * spawnRadius);
            Gizmos.DrawLine(p1, p2);
        }
    }
}
