# Phase 2 & 3 整合進度摘要

> 專案路徑：`Final/UnityProject/`
> Unity 版本：6000.4.7f1 LTS

---

## 新增腳本（4 個）

### `Assets/Scripts/NavMeshWaypointFollower.cs`
掛在每個 agent 的 GameObject 上，負責 NavMesh 路徑跟隨。
- `FinalTarget`：最終目的地
- `useNavMesh`：`true` = Phase 3（NavMesh 引導）；`false` = Phase 2（直線，會卡死）
- `arrivalRadius`：到達判定距離（直接量 agent 到 FinalTarget，與 Gizmo 圓圈完全一致）
- `loop = false`：由 `MazeSceneManager` 統一控制重置
- `RestartFromSpawn()`：傳送回起點並重新計算路徑

### `Assets/Scripts/MazeAgentSetup.cs`
每個場景可放多個，各自代表一組 agent。
- 依 `spawnRows × spawnCols` 格子生成 agent，個別 goal = `goalCenter + 同格 offset`（仿 RVO2-Unity antipodal 模式）
- 暴露 `Followers`、`ArrivedCount`、`SetNavMesh()`、`RestartAll()` 供 `MazeSceneManager` 呼叫
- Gizmo：Edit 模式預覽目標圓圈；Play 模式即時顯示每個 agent 的紅色到達圓圈

### `Assets/Scripts/MazeSceneManager.cs`
場景唯一，自動 `FindObjectsByType<MazeAgentSetup>()` 發現所有組。
- N 鍵同步切換所有組的 NavMesh 模式
- 全員抵達後倒數 `resetDelay` 秒，統一 `RestartAll()`
- **左上 UI**：Mode（青/黃色）、Groups、Agents、Round、Time（本輪計時）、Arrived
- **右側 UI**：Round History 面板，深色背景，按鈕可展開/收起，記錄每輪完成秒數

### `Assets/Scripts/RVOObstacleSetup.cs`
掛在 RVOManager GameObject 上，將場景中 tag = `RVOObstacle` 的 BoxCollider 自動登記為 RVO 靜態障礙。
- `[DefaultExecutionOrder(50)]`：在 `RVOManager.Awake()`（order 0）之後執行，避免被 `Simulator.Clear()` 清掉

---

## 修改腳本（2 個）

### `Assets/Scripts/RVOAgent.cs`
| 修改 | 說明 |
|---|---|
| `prefSpeed = Mathf.Min(dist, maxSpeed)` | 修正原本 hardcode `1f` 導致 agent 永遠只跑 1 m/s |
| `Respawn(Vector3 worldPos)` | 新增方法：同步更新 Unity transform + RVO 內部位置，速度歸零 |

### `Assets/Scripts/Editor/CharacterPrefabSetup.cs`
- 修正「Assemble Character Prefab」工具誤抓動畫 FBX 的問題
- 改為篩選有 `SkinnedMeshRenderer` 的 FBX，確保抓到 `jery.fbx`（有 Skin 的角色模型）

### `Assets/Plugins/RVO2/Simulator.cs`（Phase 1 的 RVO2 函式庫）
- 確認已有 `SetAgentPosition()`（Phase 1 隊友已加），移除重複新增的版本

---

## 場景狀態

| 場景 | 狀態 | 用途 |
|---|---|---|
| `SampleScene.unity` | 已有（Phase 1） | `ConstraintComparisonSetup`：紅藍對比，Phase 1 展示 |
| `UDeadend.unity` | 已有（本次建立） | Phase 2+3 U 型死胡同示範 |
| `L Corner.unity` | 已有（本次建立）| Phase 2+3 L 型轉角示範 |
| `Simple Obstacle.unity` | 已有（本次建立）| Phase 2+3 簡單障礙物雙向穿越 |
| `Simple Maze.unity` | 已有（本次建立）| Phase 2+3 簡單迷宮雙向穿越 |

---

## 場景必備 GameObject 清單

每個迷宮場景需要：

```
Scene
├── Plane                    ← 地板（掛 NavMeshSurface，Bake NavMesh）
├── Cube (× N)           ← 障礙物（Static + Tag: RVOObstacle）
├── BoundaryWall (× 4)       ← Plane 四周邊界牆（Static + Tag: RVOObstacle）
├── RVOManager               ← 掛 RVOManager.cs + RVOObstacleSetup.cs + MazeSceneManager.cs
└── MazeSetup       ← 掛 MazeAgentSetup.cs（可多個）
```

---

## N 鍵行為

| 狀態 | UI 顯示 | Agent 行為 |
|---|---|---|
| OFF（預設可設為 false） | 黃色「Pure ORCA」 | 直線衝向目標，在障礙前卡死（Phase 2） |
| ON | 青色「NavMesh + ORCA」 | 沿 NavMesh 路徑繞行，順利抵達（Phase 3） |


