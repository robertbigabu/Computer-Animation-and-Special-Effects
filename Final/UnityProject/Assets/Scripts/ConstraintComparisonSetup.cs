using UnityEngine;
using RVO;

/// <summary>
/// Side-by-side kinematic constraint demo.
///
/// Two agents start moving RIGHT at full speed. After a few seconds,
/// their target flips 180° to the LEFT.
///
///   Red  (top lane):  Vanilla ORCA — snaps direction instantly.
///   Blue (bottom lane): Constrained — arcs smoothly through the U-turn.
///
/// This is the clearest possible demonstration of Phase 1's contribution.
/// </summary>
public class ConstraintComparisonSetup : MonoBehaviour
{
    [Header("Layout")]
    public float laneSeparation = 3f;
    public float startX  = -8f;
    public float targetX =  8f;

    [Header("Timing")]
    [Tooltip("Seconds before the target flips 180°.")]
    public float flipTime = 3f;

    [Header("Constrained Agent (blue)")]
    public float constrainedMaxAccel      = 1f;   // m/s² — very visible ramp
    public float constrainedMaxAngularVel = 1f;   // rad/s — ~57°/s, takes ~3s for 180°

    private RVOAgent _vanillaAgent;
    private RVOAgent _constrainedAgent;
    private bool _flipped = false;
    private float _timer = 0f;

    // Labels
    private GameObject _vanillaLabel;
    private GameObject _constrainedLabel;

    void Start()
    {
        float topZ    =  laneSeparation * 0.5f;
        float bottomZ = -laneSeparation * 0.5f;

        // ── Vanilla agent (red, top lane) ──
        _vanillaAgent = SpawnAgent(
            new Vector3(startX, 0.5f, topZ),
            new Vector3(targetX, 0.5f, topZ),
            Color.red, 0f, 0f, "Vanilla"
        );

        // ── Constrained agent (blue, bottom lane) ──
        _constrainedAgent = SpawnAgent(
            new Vector3(startX, 0.5f, bottomZ),
            new Vector3(targetX, 0.5f, bottomZ),
            new Color(0.2f, 0.5f, 1f),
            constrainedMaxAccel, constrainedMaxAngularVel, "Constrained"
        );

        // ── Lane labels (3D text) ──
        _vanillaLabel     = CreateLabel("VANILLA (no constraints)", new Vector3(0, 0.1f, topZ + 1.5f), Color.red);
        _constrainedLabel = CreateLabel("CONSTRAINED (Phase 1)",    new Vector3(0, 0.1f, bottomZ - 1.5f), new Color(0.2f, 0.5f, 1f));

        Debug.Log($"[Demo] Agents moving RIGHT. Target flips LEFT in {flipTime}s. Watch the U-turn difference.");
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_flipped && _timer >= flipTime)
        {
            // Flip targets 180°
            float topZ    =  laneSeparation * 0.5f;
            float bottomZ = -laneSeparation * 0.5f;

            _vanillaAgent.Target     = new Vector3(-targetX, 0.5f, topZ);
            _constrainedAgent.Target = new Vector3(-targetX, 0.5f, bottomZ);

            _flipped = true;
            Debug.Log("[Demo] TARGET FLIPPED! Watch the red agent snap vs blue agent arc.");
        }
    }

    RVOAgent SpawnAgent(Vector3 pos, Vector3 target, Color color,
                        float maxAccel, float maxAngularVel, string label)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = label;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
        Destroy(go.GetComponent<Collider>());

        Renderer rend = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Standard")
                       ?? Shader.Find("Unlit/Color"));
        mat.color = color;
        rend.material = mat;

        RVOAgent agent = go.AddComponent<RVOAgent>();
        agent.Target = target;
        agent.maxAccelOverride      = maxAccel;
        agent.maxAngularVelOverride = maxAngularVel;

        return agent;
    }

    GameObject CreateLabel(string text, Vector3 position, Color color)
    {
        GameObject go = new GameObject($"Label_{text}");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(90, 0, 0); // face up for top-down camera

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 32;
        tm.characterSize = 0.15f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        return go;
    }

    void OnDrawGizmos()
    {
        // Lane divider
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-12, 0, 0), new Vector3(12, 0, 0));

        // Start/end markers
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(new Vector3(startX, 0, 0), 0.3f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(targetX, 0, 0), 0.3f);
    }
}
