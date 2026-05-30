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
    private readonly List<MazeAgentSetup> _groups = new();
    private GUIStyle _labelStyle;
    private float _resetTimer = -1f;

    void Start()
    {
        // Auto-discover all groups in the scene.
        _groups.AddRange(FindObjectsByType<MazeAgentSetup>(FindObjectsSortMode.None));

        if (_groups.Count == 0)
            Debug.LogWarning("[MazeSceneManager] No MazeAgentSetup found in scene.");
        else
            Debug.Log($"[MazeSceneManager] Managing {_groups.Count} group(s). Press N to toggle NavMesh mode.");
    }

    void Update()
    {
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            useNavMesh = !useNavMesh;
            foreach (var g in _groups)
                g.SetNavMesh(useNavMesh);
            Debug.Log($"[MazeSceneManager] NavMesh: {(useNavMesh ? "ON (Phase 3)" : "OFF (Phase 2 failure)")}");
        }

        int total   = TotalAgents();
        int arrived = TotalArrived();

        // Start countdown once every agent in every group has arrived.
        if (_resetTimer < 0f && total > 0 && arrived == total)
            _resetTimer = resetDelay;

        if (_resetTimer >= 0f)
        {
            _resetTimer -= Time.deltaTime;
            if (_resetTimer < 0f)
            {
                foreach (var g in _groups)
                    g.RestartAll();
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
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                richText  = true,
                fontStyle = FontStyle.Bold,
            };
            _labelStyle.normal.textColor = Color.white;
        }

        string modeStr = useNavMesh
            ? "<color=cyan>NavMesh + ORCA</color>"
            : "<color=yellow>Pure ORCA (may get stuck)</color>";

        int total   = TotalAgents();
        int arrived = TotalArrived();

        GUILayout.Label($"Mode   : {modeStr}   <b>[N]</b> to toggle", _labelStyle);
        GUILayout.Label($"Groups : {_groups.Count}",                   _labelStyle);
        GUILayout.Label($"Agents : {total}",                           _labelStyle);
        GUILayout.Label($"FPS    : {1f / Time.deltaTime:F1}",          _labelStyle);
        GUILayout.Label($"Arrived: {arrived} / {total}",               _labelStyle);

        if (_resetTimer >= 0f)
            GUILayout.Label($"<color=orange>Resetting in {_resetTimer:F1}s…</color>", _labelStyle);
    }
}
