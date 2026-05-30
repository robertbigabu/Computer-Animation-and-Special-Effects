using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-level manager for maze demonstrations.
/// Aggregates multiple MazeAgentSetup groups, handles the shared UI,
/// monitors all-arrived condition, and triggers the group reset.
///
/// Keyboard shortcuts:
///   N     – toggle NavMesh mode (Phase 2 ↔ Phase 3) across all groups
///   R     – force-end current round immediately, record arrival rate, and respawn
/// </summary>
public class MazeSceneManager : MonoBehaviour
{
    [Header("Group Reset")]
    [Tooltip("Seconds to wait after all agents arrive (or R is pressed) before respawning.")]
    public float resetDelay = 1.5f;

    [Header("NavMesh Mode (toggle with N key)")]
    public bool useNavMesh = true;

    // ─── Round record ──────────────────────────────────────────────────
    private struct RoundRecord
    {
        public float Time;      // elapsed seconds
        public int   Arrived;   // agents that reached goal
        public int   Total;     // total agents
        public bool  Forced;    // true = ended by R key, false = natural completion
        public bool  NavMesh;   // mode active during this round

        public float ArrivalRate => Total > 0 ? (float)Arrived / Total : 0f;
    }

    // ─── Runtime ──────────────────────────────────────────────────────
    private readonly List<MazeAgentSetup> _groups  = new();
    private readonly List<RoundRecord>    _history = new();

    private GUIStyle  _labelStyle;
    private GUIStyle  _historyStyle;
    private GUIStyle  _forcedStyle;
    private GUIStyle  _panelStyle;
    private GUIStyle  _toggleBtnStyle;
    private GUIStyle  _forceEndBtnStyle;
    private Texture2D _bgTexture;

    private bool _historyVisible = true;

    private float _roundTimer  = 0f;   // elapsed seconds this round
    private float _resetTimer  = -1f;  // countdown before respawn (-1 = idle)
    private bool  _roundActive = false;

    void Start()
    {
        _groups.AddRange(FindObjectsByType<MazeAgentSetup>(FindObjectsSortMode.None));

        if (_groups.Count == 0)
            Debug.LogWarning("[MazeSceneManager] No MazeAgentSetup found in scene.");
        else
            Debug.Log($"[MazeSceneManager] Managing {_groups.Count} group(s). " +
                      $"[N] toggle NavMesh  [R] force-end round.");

        _roundActive = true;
    }

    void Update()
    {
        // ── N: toggle NavMesh mode ─────────────────────────────────────
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            useNavMesh = !useNavMesh;
            foreach (var g in _groups)
                g.SetNavMesh(useNavMesh);
            Debug.Log($"[MazeSceneManager] NavMesh: {(useNavMesh ? "ON (Phase 3)" : "OFF (Phase 2)")}");
        }

        // ── R: force-end current round ─────────────────────────────────
        // Only fires when a round is active and no reset is already pending.
        if (Keyboard.current.rKey.wasPressedThisFrame && _roundActive && _resetTimer < 0f)
        {
            int total   = TotalAgents();
            int arrived = TotalArrived();

            _roundActive = false;
            RecordRound(arrived, total, forced: true);
            _resetTimer = resetDelay;

            Debug.Log($"[MazeSceneManager] R pressed — Round {_history.Count} force-ended at " +
                      $"{_roundTimer:F2}s  ({arrived}/{total} arrived).");
        }

        // ── Round timer ────────────────────────────────────────────────
        if (_roundActive)
            _roundTimer += Time.deltaTime;

        // ── Natural completion ─────────────────────────────────────────
        {
            int total   = TotalAgents();
            int arrived = TotalArrived();

            if (_resetTimer < 0f && total > 0 && arrived == total)
            {
                _roundActive = false;
                RecordRound(arrived, total, forced: false);
                _resetTimer = resetDelay;
                Debug.Log($"[MazeSceneManager] Round {_history.Count} complete in {_roundTimer:F2}s.");
            }
        }

        // ── Respawn countdown ──────────────────────────────────────────
        if (_resetTimer >= 0f)
        {
            _resetTimer -= Time.deltaTime;
            if (_resetTimer < 0f)
            {
                foreach (var g in _groups)
                    g.RestartAll();

                _roundTimer  = 0f;
                _roundActive = true;
                Debug.Log("[MazeSceneManager] Respawning all groups — next round started.");
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────
    void RecordRound(int arrived, int total, bool forced)
    {
        _history.Add(new RoundRecord
        {
            Time    = _roundTimer,
            Arrived = arrived,
            Total   = total,
            Forced  = forced,
            NavMesh = useNavMesh,
        });
    }

    int TotalAgents()
    {
        int n = 0;
        foreach (var g in _groups) n += g.Followers.Count;
        return n;
    }

    int TotalArrived()
    {
        int n = 0;
        foreach (var g in _groups) n += g.ArrivedCount;
        return n;
    }

    // ─── UI ───────────────────────────────────────────────────────────
    void OnGUI()
    {
        InitStyles();

        int total   = TotalAgents();
        int arrived = TotalArrived();
        int round   = _history.Count + 1;
        float rate  = total > 0 ? (float)arrived / total * 100f : 0f;

        string modeStr = useNavMesh
            ? "<color=cyan>NavMesh + ORCA  [Phase 3]</color>"
            : "<color=yellow>Pure ORCA  [Phase 2]</color>";

        // ── Left panel: live stats ─────────────────────────────────────
        GUILayout.BeginVertical();
        GUILayout.Label($"Mode    : {modeStr}",                        _labelStyle);
        GUILayout.Label($"Groups  : {_groups.Count}",                  _labelStyle);
        GUILayout.Label($"Agents  : {total}",                          _labelStyle);
        GUILayout.Label($"Round   : {round}",                          _labelStyle);
        GUILayout.Label($"Time    : {_roundTimer:F1}s",                _labelStyle);
        GUILayout.Label($"Arrived : {arrived} / {total}  ({rate:F0}%)",_labelStyle);
        GUILayout.Space(6f);
        GUILayout.Label("<b>[N]</b> NavMesh mode   <b>[R]</b> Force end round", _labelStyle);

        if (_resetTimer >= 0f)
            GUILayout.Label($"<color=orange>Respawning in {_resetTimer:F1}s…</color>", _labelStyle);

        GUILayout.EndVertical();

        // ── Right panel: round history ─────────────────────────────────
        const float panelW  = 290f;
        const float rowH    = 26f;
        const float btnH    = 28f;
        const float padding = 10f;
        float x = Screen.width - panelW - 12f;
        float y = 12f;

        float panelH = btnH + padding * 2f
                     + (_historyVisible && _history.Count > 0
                            ? _history.Count * rowH + padding
                            : 0f);

        GUI.Box(new Rect(x - padding, y - padding, panelW + padding * 2f, panelH + padding * 2f),
                GUIContent.none, _panelStyle);

        GUILayout.BeginArea(new Rect(x, y, panelW, panelH));

        string btnLabel = _historyVisible ? "Round History  ▲" : "Round History  ▼";
        if (GUILayout.Button(btnLabel, _toggleBtnStyle, GUILayout.Height(btnH)))
            _historyVisible = !_historyVisible;

        if (_historyVisible && _history.Count > 0)
        {
            GUILayout.Space(padding * 0.5f);
            for (int i = 0; i < _history.Count; i++)
            {
                RoundRecord r    = _history[i];
                string modeTag   = r.NavMesh ? "[P3]" : "[P2]";
                string forcedTag = r.Forced  ? " ✗"   : " ✓";
                string pct       = $"{r.ArrivalRate * 100f:F0}%";
                string line      = $"R{i + 1} {modeTag}{forcedTag}  {r.Arrived}/{r.Total} ({pct})  {r.Time:F1}s";

                // Green = natural 100 %, orange = forced or incomplete.
                GUIStyle style = (!r.Forced && r.Arrived == r.Total) ? _historyStyle : _forcedStyle;
                GUILayout.Label(line, style);
            }
        }

        GUILayout.EndArea();
    }

    void InitStyles()
    {
        if (_labelStyle != null) return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            richText  = true,
            fontStyle = FontStyle.Bold,
        };
        _labelStyle.normal.textColor = Color.white;

        // History row — completed round (green).
        _historyStyle = new GUIStyle(_labelStyle)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Normal,
        };
        _historyStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);   // green

        // History row — forced / incomplete round (orange).
        _forcedStyle = new GUIStyle(_historyStyle);
        _forcedStyle.normal.textColor = new Color(1f, 0.65f, 0.1f);   // orange

        // Solid dark background.
        _bgTexture = new Texture2D(1, 1);
        _bgTexture.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.08f, 0.88f));
        _bgTexture.Apply();

        _panelStyle = new GUIStyle();
        _panelStyle.normal.background = _bgTexture;

        _toggleBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        _toggleBtnStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        _toggleBtnStyle.hover.textColor  = Color.white;
        _toggleBtnStyle.active.textColor = Color.white;
    }

    void OnDestroy()
    {
        if (_bgTexture != null) Destroy(_bgTexture);
    }
}
