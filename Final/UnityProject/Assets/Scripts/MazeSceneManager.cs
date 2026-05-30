using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-level manager for maze demonstrations.
/// Aggregates multiple MazeAgentSetup groups, handles the shared UI,
/// monitors all-arrived condition, and triggers the group reset.
///
/// Setup: place one MazeSceneManager in the scene alongside one or more
/// MazeAgentSetup GameObjects. It auto-discovers all groups at Start().
/// Press N to toggle NavMesh mode across all groups simultaneously.
/// </summary>
public class MazeSceneManager : MonoBehaviour
{
    [Header("Group Reset")]
    [Tooltip("Seconds to wait after all agents arrive before respawning everyone.")]
    public float resetDelay = 1.5f;

    [Header("NavMesh Mode (toggle with N key)")]
    public bool useNavMesh = true;

    // ─── Runtime ──────────────────────────────────────────────────────
    private readonly List<MazeAgentSetup> _groups  = new();
    private readonly List<float>          _history = new(); // seconds per round

    private GUIStyle _labelStyle;
    private GUIStyle _historyTitleStyle;
    private GUIStyle _historyStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _toggleBtnStyle;
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
            Debug.Log($"[MazeSceneManager] Managing {_groups.Count} group(s). Press N to toggle NavMesh mode.");

        _roundActive = true;
    }

    void Update()
    {
        // N key: toggle NavMesh mode across all groups.
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            useNavMesh = !useNavMesh;
            foreach (var g in _groups)
                g.SetNavMesh(useNavMesh);
            Debug.Log($"[MazeSceneManager] NavMesh: {(useNavMesh ? "ON (Phase 3)" : "OFF (Phase 2 failure)")}");
        }

        // Advance round timer while agents are still moving.
        if (_roundActive)
            _roundTimer += Time.deltaTime;

        int total   = TotalAgents();
        int arrived = TotalArrived();

        // All arrived → record time and start reset countdown.
        if (_resetTimer < 0f && total > 0 && arrived == total)
        {
            _roundActive = false;
            _history.Add(_roundTimer);
            _resetTimer = resetDelay;
            Debug.Log($"[MazeSceneManager] Round {_history.Count} finished in {_roundTimer:F2}s.");
        }

        if (_resetTimer >= 0f)
        {
            _resetTimer -= Time.deltaTime;
            if (_resetTimer < 0f)
            {
                foreach (var g in _groups)
                    g.RestartAll();

                _roundTimer  = 0f;
                _roundActive = true;
                Debug.Log("[MazeSceneManager] All agents arrived — respawning all groups.");
            }
        }
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

        // ── Left panel: live stats ────────────────────────────────────
        string modeStr = useNavMesh
            ? "<color=cyan>NavMesh + ORCA</color>"
            : "<color=yellow>Pure ORCA (may get stuck)</color>";

        int total   = TotalAgents();
        int arrived = TotalArrived();
        int round   = _history.Count + 1;

        GUILayout.BeginVertical();
        GUILayout.Label($"Mode   : {modeStr}   <b>[N]</b> to toggle", _labelStyle);
        GUILayout.Label($"Groups : {_groups.Count}",                   _labelStyle);
        GUILayout.Label($"Agents : {total}",                           _labelStyle);
        GUILayout.Label($"Round  : {round}",                           _labelStyle);
        GUILayout.Label($"Time   : {_roundTimer:F1}s",                 _labelStyle);
        GUILayout.Label($"Arrived: {arrived} / {total}",               _labelStyle);

        if (_resetTimer >= 0f)
            GUILayout.Label($"<color=orange>Resetting in {_resetTimer:F1}s…</color>", _labelStyle);
        GUILayout.EndVertical();

        // ── Right panel: round history ────────────────────────────────
        const float panelW   = 220f;
        const float rowH     = 24f;
        const float titleH   = 32f;
        const float btnH     = 28f;
        const float padding  = 10f;
        float x = Screen.width - panelW - 12f;
        float y = 12f;

        // Height: always show toggle button; expand for rows when visible.
        float panelH = btnH + padding * 2f
                     + (_historyVisible && _history.Count > 0
                            ? titleH + _history.Count * rowH + padding
                            : 0f);

        // Solid dark background box.
        GUI.Box(new Rect(x - padding, y - padding, panelW + padding * 2f, panelH + padding * 2f),
                GUIContent.none, _panelStyle);

        GUILayout.BeginArea(new Rect(x, y, panelW, panelH));

        // Toggle button.
        string btnLabel = _historyVisible ? "Round History  ▲" : "Round History  ▼";
        if (GUILayout.Button(btnLabel, _toggleBtnStyle, GUILayout.Height(btnH)))
            _historyVisible = !_historyVisible;

        // History rows (only when expanded and there is data).
        if (_historyVisible && _history.Count > 0)
        {
            GUILayout.Space(padding);
            for (int i = 0; i < _history.Count; i++)
                GUILayout.Label($"Round {i + 1} : {_history[i]:F2}s", _historyStyle);
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

        _historyTitleStyle = new GUIStyle(_labelStyle) { fontSize = 15 };
        _historyTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _historyStyle = new GUIStyle(_labelStyle)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Normal,
        };
        _historyStyle.normal.textColor = Color.white;

        // Solid dark background for the right panel.
        _bgTexture = new Texture2D(1, 1);
        _bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.85f));
        _bgTexture.Apply();

        _panelStyle = new GUIStyle();
        _panelStyle.normal.background = _bgTexture;

        // Toggle button: inherits default button look but with bigger font.
        _toggleBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        _toggleBtnStyle.normal.textColor  = new Color(1f, 0.85f, 0.3f);
        _toggleBtnStyle.hover.textColor   = Color.white;
        _toggleBtnStyle.active.textColor  = Color.white;
    }

    void OnDestroy()
    {
        if (_bgTexture != null) Destroy(_bgTexture);
    }
}
