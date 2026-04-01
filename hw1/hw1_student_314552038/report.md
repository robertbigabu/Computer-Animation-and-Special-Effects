# HW1 Jelly Simulation — Implementation Report

## Overview

This document describes the implementation of the three core simulation files: `jelly.cpp`, `terrain.cpp`, and `integrator.cpp`.

---

## 1. Spring–Damper Internal Forces (`jelly.cpp`)

### TODO#2-1 — `computeSpringForce`

The Hooke's law spring force on particle A from a spring connecting A → B:

$$\mathbf{F}_{spring} = k_s \cdot (|\mathbf{AB}| - L_0) \cdot \hat{\mathbf{AB}}$$

where $\mathbf{AB} = \mathbf{p}_B - \mathbf{p}_A$, $L_0$ is the rest length, and $k_s$ is the spring coefficient. The force is zero if the spring has zero length (degenerate case).

### TODO#2-2 — `computeDamperForce`

The damper force resists the rate of change of spring extension:

$$\mathbf{F}_{damper} = k_d \cdot [(\mathbf{v}_B - \mathbf{v}_A) \cdot \hat{\mathbf{AB}}] \cdot \hat{\mathbf{AB}}$$

Only the velocity component along the spring axis contributes. This ensures damping does not affect motion perpendicular to the spring.

### TODO#2-3 — `computeInternalForce`

Iterates over all springs (structural, shear, bending). For each spring connecting particles A and B:

1. Read positions and velocities of both endpoint particles.
2. Compute spring force and damper force.
3. Apply the combined force to A (`addForce(F)`) and the reaction to B (`addForce(-F)`) — Newton's third law.

---

## 2. Collision Handling (`terrain.cpp`)

Both terrain types follow the same two-stage collision response:

**Stage 1 — Velocity Correction** (when particle is near/below the surface and moving into it):
$$\mathbf{v}_{new} = \mathbf{v} - (1 + c_r)({\mathbf{v} \cdot \hat{\mathbf{n}}}) \hat{\mathbf{n}}$$
where $c_r = 0.8$ is the coefficient of restitution. This reflects the normal velocity component.

**Stage 2 — Contact Force** (normal resist + Coulomb friction):
- Normal resist: if the net force on the particle pushes into the surface ($\mathbf{F} \cdot \hat{\mathbf{n}} < 0$), add $\mathbf{F}_{resist} = -(\mathbf{F} \cdot \hat{\mathbf{n}}) \hat{\mathbf{n}}$ to cancel the penetrating component.
- Friction: $\mathbf{F}_{friction} = -c_f \cdot |F_n| \cdot \hat{\mathbf{v}}_t$ opposing tangential motion, where $c_f = 0.5$.

### TODO#3-1 — `PlaneTerrain::handleCollision`

- Plane surface defined by normal $\hat{\mathbf{n}} = (\sin 20°, \cos 20°, 0)$ (−20° rotation around Z-axis) passing through the origin.
- Signed distance: $d = (\mathbf{p} - \mathbf{p}_{plane}) \cdot \hat{\mathbf{n}}$.
- Collision detected when $d < \varepsilon = 0.01$.

### `ElevatorTerrain::handleCollision`

Same logic as the plane, with two additions:
- **XZ bounds check**: only particles within the $5 \times 5$ elevator footprint (±2.5 units from elevator center) are tested.
- **Relative velocity**: the collision condition uses relative velocity $(\mathbf{v}_{particle} - \mathbf{v}_{elevator}) \cdot \hat{\mathbf{n}}$ to correctly handle the moving platform.

---

## 3. Numerical Integration (`integrator.cpp`)

All integrators iterate over every particle of every jelly. Forces are cleared after each integration step.

### TODO#4-1 — Explicit Euler

The simplest first-order method:

$$\mathbf{x}_{n+1} = \mathbf{x}_n + \mathbf{v}_n \Delta t$$
$$\mathbf{v}_{n+1} = \mathbf{v}_n + \mathbf{a}_n \Delta t$$

Straightforward but conditionally stable; the time step must be small relative to the stiffness.

### TODO#4-2 — Implicit Euler

A first-order semi-implicit approximation that improves stability:

1. **Backup** current state $(x_n, v_n, a_n)$.
2. **Predict** via Explicit Euler: $x^* = x_n + v_n \Delta t$, $v^* = v_n + a_n \Delta t$.
3. **Evaluate forces** at the predicted state by calling `computeJellyForce(x^*, v^*)` to obtain $a^*$. Because collision response is embedded inside `computeJellyForce` (via `handleCollision`), the particle's velocity at the predicted state may be further corrected if $x^*$ penetrates the terrain. Reading `getVelocity()` after this call gives the physically-corrected $v^*$ at the predicted state, which is the correct slope for $x_{n+1}$.
4. **Refine** using the predicted-state derivatives:
$$\mathbf{v}_{n+1} = \mathbf{v}_n + \mathbf{a}^* \Delta t, \quad \mathbf{x}_{n+1} = \mathbf{x}_n + \mathbf{v}^* \Delta t$$

This scheme is more dissipative than Explicit Euler and tolerates larger time steps.

### Midpoint Euler

A second-order Runge-Kutta method:

1. **Backup** state and save acceleration $a_n$.
2. **Half-step**: $x_{mid} = x_n + \frac{\Delta t}{2} v_n$, $v_{mid} = v_n + \frac{\Delta t}{2} a_n$.
3. **Compute forces** at the midpoint.
4. **Full step** using midpoint derivatives:
$$\mathbf{x}_{n+1} = \mathbf{x}_n + \mathbf{v}_{mid} \Delta t, \quad \mathbf{v}_{n+1} = \mathbf{v}_n + \mathbf{a}_{mid} \Delta t$$

### TODO#Bonus — Runge-Kutta 4th Order (RK4)

The classical fourth-order method with four derivative evaluations:

$$k_1 = f(x_n,\, v_n)$$
$$k_2 = f\!\left(x_n + \tfrac{\Delta t}{2}k_{1v},\; v_n + \tfrac{\Delta t}{2}k_{1a}\right)$$
$$k_3 = f\!\left(x_n + \tfrac{\Delta t}{2}k_{2v},\; v_n + \tfrac{\Delta t}{2}k_{2a}\right)$$
$$k_4 = f\!\left(x_n + \Delta t\, k_{3v},\; v_n + \Delta t\, k_{3a}\right)$$

$$\mathbf{x}_{n+1} = \mathbf{x}_n + \frac{\Delta t}{6}(k_{1v} + 2k_{2v} + 2k_{3v} + k_{4v})$$
$$\mathbf{v}_{n+1} = \mathbf{v}_n + \frac{\Delta t}{6}(k_{1a} + 2k_{2a} + 2k_{3a} + k_{4a})$$

RK4 offers excellent accuracy-per-step and is the most stable of the four ODE integrators implemented.

### TODO#4-3 — Position-Based Dynamics (PBD)

A constraint-projection method that operates directly on positions rather than forces:

#### Step 1 — Prediction

Identical to Explicit Euler in structure. The velocity is updated with acceleration **first**, then used to predict the position. This is essential: since Step 3 derives final velocity as `(x* - x_n) / dt`, the acceleration must be encoded into `x*` through the updated velocity.

$$\mathbf{v}^* = \mathbf{v}_n + \mathbf{a}_n \Delta t$$
$$\mathbf{x}^* = \mathbf{x}_n + \mathbf{v}^* \Delta t$$

The predicted position is stored via `setPredictedPosition` (not `setPosition`) so the original position remains available for Step 3.

#### Step 2 — Constraint Projection (`solverIterations = 4`)

Two types of constraints are enforced per iteration:

**Self Collision (spring-based distance constraints)**

For each spring connecting particles A and B, `minDistance` and `maxDistance` are defined from the jelly cell size:

$$\text{cellSize} = \frac{L_{jelly}}{N_{edge} - 1}, \quad \text{minDistance} = 0.5 \times \text{cellSize}, \quad \text{maxDistance} = L_0 \text{ (spring rest length)}$$

- If $d < \text{minDistance}$ (too close): **push** both particles apart symmetrically.
- If $d > \text{maxDistance}$ (too far): **pull** both particles together symmetrically.

Symmetric correction (equal-mass assumption):
$$\Delta \mathbf{p} = \frac{1}{2} \cdot \frac{d - d_{target}}{d} \cdot (\mathbf{p}_B^* - \mathbf{p}_A^*), \quad \mathbf{p}_A^* \mathrel{+}= \Delta\mathbf{p},\quad \mathbf{p}_B^* \mathrel{-}= \Delta\mathbf{p}$$

where $d_{target}$ is `minDistance` when too close or `maxDistance` when too far.

**Terrain Constraint**

Similar to self collision. For each particle whose predicted position penetrates the terrain surface (signed distance $< 0$):

$$\mathbf{p}_i^* \mathrel{+}= -d \cdot \hat{\mathbf{n}}_{\text{terrain}}$$

This pushes the particle back onto the terrain surface. For the elevator terrain, only particles within the 5×5 footprint (±2.5 units in XZ from elevator center) are tested.

#### Step 3 — Position and Velocity Update with XSPH Viscosity

**Velocity from constrained positions:**
$$\mathbf{v}_{n+1} = \frac{\mathbf{x}^* - \mathbf{x}_n}{\Delta t}$$

**XSPH velocity smoothing** using distance-based weights over spring-connected neighbors $q \in N_i$:

$$w_{iq} = \frac{1}{\|\mathbf{x}_i^* - \mathbf{x}_q^*\|}, \quad \mathbf{v}_i \mathrel{+}= \varepsilon \cdot \frac{\displaystyle\sum_{q \in N_i} w_{iq}\,(\mathbf{v}_q - \mathbf{v}_i)}{\displaystyle\sum_{q \in N_i} w_{iq}}$$

with $\varepsilon = 0.1$. Closer neighbors have higher influence. Finally, constrained positions are committed via `setPosition`.

PBD is unconditionally stable at the cost of some physical accuracy, making it robust for interactive simulation.

---

## Summary of Modified Functions

| File | Function | TODO |
|---|---|---|
| `jelly.cpp` | `computeSpringForce` | #2-1 |
| `jelly.cpp` | `computeDamperForce` | #2-2 |
| `jelly.cpp` | `computeInternalForce` | #2-3 |
| `terrain.cpp` | `PlaneTerrain::handleCollision` | #3-1 |
| `terrain.cpp` | `ElevatorTerrain::handleCollision` | (unlabeled) |
| `integrator.cpp` | `ExplicitEulerIntegrator::integrate` | #4-1 |
| `integrator.cpp` | `ImplicitEulerIntegrator::integrate` | #4-2 |
| `integrator.cpp` | `MidpointEulerIntegrator::integrate` | (unlabeled) |
| `integrator.cpp` | `RungeKuttaFourthIntegrator::integrate` | Bonus |
| `integrator.cpp` | `PositionBasedDynamicIntegrator::integrate` | #4-3 |
