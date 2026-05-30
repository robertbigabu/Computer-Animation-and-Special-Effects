# Phase 2 & Phase 3 實驗規劃與結果記錄

## 一、實驗架構

### 系統架構回顧

```
[NavMesh]  ─── FinalTarget → next waypoint →  preferred velocity
                                                        ↓
[RVO2 Agent.cs] ─── ORCA half-planes + accel disk + angular wedge ─── LP solve
                                                        ↓
                                              constrained velocity
                                                        ↓
                              [AnimationBridge] → Blend Tree (MoveX, MoveY)
```

**N 鍵切換模式**（MazeSceneManager）

| 模式 | useNavMesh | preferred velocity 來源 |
|------|------------|------------------------|
| Phase 2（對比組） | `false` | 直線指向 FinalTarget |
| Phase 3（混合控制器） | `true` | NavMesh 路徑點方向 |

---

## 二、實驗場景清單

| Scene 名稱 | Unity 場景檔 | 障礙物類型 | Pure ORCA 預期失效原因 |
|-----------|------------|-----------|----------------------|
| L 型轉角 | `L Corner.unity` | 單一 90° 轉彎牆 | agent 撞牆後沿牆爬行或卡角落 |
| U 型死胡同 | `UDeadend.unity` | U 形凹槽 | agent 衝入凹槽後 local minima，完全無法退出 |
| 簡單迷宮 | `Simple Maze.unity` | 多段折返通道 | 多 agent 互堵＋多重 local minima |

---

## 三、實驗參數設定

### 固定參數（RVOManager Inspector）

| 參數 | 值 |
|------|----|
| maxSpeed | 20.0 m/s |
| agentRadius | 2 m |
| neighborDist | 15 m |
| maxNeighbors | 10 |
| timeHorizon | 5 s |
| timeHorizonObst | 5 s |
| **maxAccel** | **4.0 m/s²** |
| **maxAngularVel** | **3.0 rad/s** |

### MazeAgentSetup — 各場景實際設定

| 場景 | 組數 | spawnRows × spawnCols | 總 agent 數 | spawnSpacing | spawnCenter | goalCenter | arrivalThreshold |
|------|------|----------------------|------------|-------------|------------|-----------|-----------------|
| L 型轉角 | 1 | 8 × 8 | **64** | 10 | (80, 0, 90) | (−80, 0, −90) | 4 |
| U 型死胡同 | 1 | 8 × 8 | **64** | 10 | (0, 0, 80) | (0, 0, −90) | 4 |
| 簡單迷宮 | 2（對向） | 5 × 8 / 5 × 8 | **40 + 40 = 80** | 10 | (40,0,±120) | (40,0,∓120) | 4 |

> 簡單迷宮場景為**雙向對沖**設計（Group A 與 Group B 反向行進），用以製造最嚴苛的互堵壓力。

### 逾時定義

> **Stuck 判定**：單局 **180 秒**內 `ArrivedCount / TotalAgents < 100%` 視為有 agent 卡死。

---

## 四、實驗執行步驟

### Phase 2 — 複雜地形失效分析（對比組）

1. 開啟目標 Scene（如 `UDeadend.unity`）
2. 確認 MazeSceneManager → `useNavMesh = false`（或進入 Play Mode 後按 **N** 切換至 `Pure ORCA`）
3. 讓模擬自動跑 **3 round**（MazeSceneManager 每局結束自動重生）
4. 記錄每局的 `Arrived / Total`（GUI 右上角）與 `Round Time`（History 面板）
5. 截圖/錄製 agent 卡住的典型瞬間

### Phase 3 — NavMesh + Constrained ORCA

1. 同一 Scene，**N 鍵**切換至 `NavMesh + ORCA`（或直接 `useNavMesh = true`）
2. 同樣跑 **3 round**
3. 記錄相同指標

---

## 五、結果記錄表格

### 5.1 主結果表：到達率 × 完成時間

> 每格填入：`到達率%（完成時間 s）`，未完成填 `X% (>180s)`

| 場景 | 模式 | Round 1 | Round 2 | Round 3 | 平均完成時間 | 卡死 agent 數（平均） |
|------|------|---------|---------|---------|------------|---------------------|
| L 型轉角 | **Phase 2 Pure ORCA** |5%(>180s) |5%(>180s) | 5%(>180s)|NULL | 61|
| L 型轉角 | **Phase 3 NavMesh+ORCA** | 100%(47.1s)|100%(44.5s) | 100%(40.5s) | 40.03s| 0|
| U 型死胡同 | **Phase 2 Pure ORCA** | 0%(>180s)| 0%(>180s)|0%(>180s) | NULL | 64|
| U 型死胡同 | **Phase 3 NavMesh+ORCA** |100%(53.2s) |100%(48.7s) |100%(49.0s) |50.3s |0 |
| 簡單迷宮 | **Phase 2 Pure ORCA** | 0%(>180s)| 0%(>180s)|0%(>180s) | NULL | 80|
| 簡單迷宮 | **Phase 3 NavMesh+ORCA** | 100%(103.8s)|100%(108.9s) |100%(110.9s) | 107.87s|0 |

---

### 5.2 Phase 2 失效細節表

| 場景  | 卡死位置描述 | 失效原因分類 |
|-----------------------|-----------------|------------|
| L 型轉角 |  轉角內側 |  沿牆震盪 |
| U 型死胡同 |  凹槽底部 | Local Minima 無退出速度 |
| 簡單迷宮 |  起點牆前 | Local Minima 無退出速度 |

---

### 5.3 Phase 3 導航品質表

| 場景 | 平均完成時間（s） | 最長單 agent 時間（s） | NavMesh 重算間隔（tick） |
|------|-----------------|---------------------|----------------------|
| L 型轉角 |40.03 |47.1 | 6 |
| U 型死胡同 | 50.3| 53.2| 6 |
| 簡單迷宮 |107.87 |110.9 | 6 |

> 在到達終點時，因為我們有對 agent 進行動畫的約束，因此會花非常多的時間穿越人群已排出目標位置
---

### 5.4 Phase 2 vs Phase 3 改善幅度

| 場景 | P2 到達率 | P3 到達率 | P3 完成時間（s） | 改善倍率（P2 到達率為 0 時填 ∞） |
|------|----------|----------|----------------|-------------------------------|
| L 型轉角 | 5% | 100% | 40.03| 20|
| U 型死胡同 | 0% | 100% |50.3 | ∞|
| 簡單迷宮 | 0% | 100% |107.87 |∞ |

---

### 5.5 Phase 1 動畫約束效果（從 ConstraintComparisonSetup 場景量測）

| 指標 | Vanilla ORCA | Constrained ORCA（maxAccel=4, maxAngVel=3） |
|------|--------------|--------------------------------------------|
| 180° U-turn 完成時間（s） | ≈ 0（瞬間） | ≈ π / maxAngularVel = 1.05 s |
| 最大加速量（m/s²，觀測值） | 無限制 | ≤ 4.0 |
| 動畫抖動（主觀 0-5） | | |
| 滑步現象（主觀 Y/N） | | |

---

## 六、分析要點

### Phase 2 失效分析（凸顯全域尋路必要性）

```
Pure ORCA local minima 成因：
  preferred_velocity 指向 FinalTarget（直線）
  → 牆壁產生 ORCA half-plane 推離直線
  → LP 解落在 0 velocity（feasible region 退化）
  → agent 停止在凹槽內
```

用截圖標示：`preferred_velocity`（紅色）vs `ORCA output velocity`（藍色）

### Phase 3 成功原因

```
NavMesh 路徑點 → preferred_velocity 指向「下一個轉彎後的 waypoint」
  → ORCA half-plane 仍處理 agent-agent 避障
  → 即使牆前有其他 agent，也能繞行
```

### 關鍵差異總結

| 維度 | Pure ORCA | NavMesh + Constrained ORCA |
|------|-----------|--------------------------|
| 全域感知 | 無（local only） | 有（NavMesh A*） |
| 動態避障 | 是 | 是（kinematic constrained） |
| 靜態障礙 | ORCA lines | NavMesh 繞道 + ORCA lines |
| 動作連續性 | 速度可瞬間改變 | maxAccel + maxAngularVel 約束 |
| Local Minima | 無法自行解決 | NavMesh waypoint 引導繞開 |

---

## 七、實驗完整性 Checklist

- [ ] L Corner：Phase 2 跑完 3 round，有截圖紀錄卡死
- [ ] L Corner：Phase 3 跑完 3 round，時間記錄於 5.1
- [ ] UDeadend：Phase 2 跑完 3 round，有截圖紀錄卡死
- [ ] UDeadend：Phase 3 跑完 3 round，時間記錄於 5.1
- [ ] Simple Maze：Phase 2 跑完 3 round
- [ ] Simple Maze：Phase 3 跑完 3 round
- [ ] ConstraintComparison：U-turn 對比截圖完成
- [ ] 表格 5.1 ~ 5.5 全部填寫完畢
