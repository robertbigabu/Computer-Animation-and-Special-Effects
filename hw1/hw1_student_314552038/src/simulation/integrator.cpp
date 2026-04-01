#include "integrator.h"
#include <iostream>
#include <cmath>
#include <vector>
#include "../util/helper.h"
namespace simulation {
// Factory
std::unique_ptr<Integrator> IntegratorFactory::CreateIntegrator(IntegratorType type) {
    switch (type) {
        case simulation::IntegratorType::ExplicitEuler:
            return std::make_unique<ExplicitEulerIntegrator>();
        case simulation::IntegratorType::ImplicitEuler:
            return std::make_unique<ImplicitEulerIntegrator>();
        case simulation::IntegratorType::MidpointEuler:
            return std::make_unique<MidpointEulerIntegrator>();
        case simulation::IntegratorType::RungeKuttaFourth:
            return std::make_unique<RungeKuttaFourthIntegrator>();
        case simulation::IntegratorType::PositionBasedDynamic:
            return std::make_unique<PositionBasedDynamicIntegrator>();
        default:
            throw std::invalid_argument("TerrainFactory::CreateTerrain : invalid TerrainType");
            break;
    }
    return nullptr;
}

//
// ExplicitEulerIntegrator
//

IntegratorType ExplicitEulerIntegrator::getType() { return IntegratorType::ExplicitEuler; }


void ExplicitEulerIntegrator::integrate(MassSpringSystem& particleSystem) {
    // TODO#4-1: Integrate position and velocity
    //   1. Integrate position using current velocity.
    //   2. Integrate velocity using current acceleration.
    //   3. Clear force
    // Note:
    //   1. You should do this first because it is very simple. Then you can check whether your collision is correct or
    //   not.
    //   2. See functions in `particle.h` if you don't know how to access data.

    //   3. Review "ODE_basics.pptx" from p.15 - p.16
    float dt = particleSystem.deltaTime;

    for (int j = 0; j < particleSystem.getJellyCount(); j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        for (int i = 0; i < jelly->getParticleNum(); i++) {
            Particle& p = jelly->getParticle(i);
            Eigen::Vector3f acc = p.getAcceleration();
            // x_{n+1} = x_n + v_n * dt
            p.addPosition(p.getVelocity() * dt);
            // v_{n+1} = v_n + a_n * dt
            p.addVelocity(acc * dt);
            // Clear force for next step
            p.setForce(Eigen::Vector3f::Zero());
        }
    }
}

//
// ImplicitEulerIntegrator
//

IntegratorType ImplicitEulerIntegrator::getType() { return IntegratorType::ImplicitEuler; }

void ImplicitEulerIntegrator::integrate(MassSpringSystem& particleSystem) {
    // TODO#4-2: Integrate position and velocity
    //   1. Backup original particles' data.
    //   2. Integrate position and velocity using explicit euler to get Xn+1.
    //   3. Compute refined Xn+1 and Vn+1 using (1.) and (2.).
    // Note:
    //   1. Use `MassSpringSystem::computeJellyForce`
    //      with modified position and velocity to get Xn+1.
    //   2. See functions in `particle.h` if you don't know how to access data.
    //   3. Review "ODE_implicit.pptx" from p.18 - p.19

    float dt = particleSystem.deltaTime;

    int jellyCount = particleSystem.getJellyCount();

    // 1. Backup original state
    std::vector<std::vector<Eigen::Vector3f>> origPos(jellyCount);
    std::vector<std::vector<Eigen::Vector3f>> origVel(jellyCount);
    std::vector<std::vector<Eigen::Vector3f>> origAcc(jellyCount);

    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        origPos[j].resize(n);
        origVel[j].resize(n);
        origAcc[j].resize(n);
        for (int i = 0; i < n; i++) {
            origPos[j][i] = jelly->getParticle(i).getPosition();
            origVel[j][i] = jelly->getParticle(i).getVelocity();
            origAcc[j][i] = jelly->getParticle(i).getAcceleration();
        }
    }

    // 2. Explicit Euler to get predicted x*, v*
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        for (int i = 0; i < n; i++) {
            Particle& p = jelly->getParticle(i);
            p.setPosition(origPos[j][i] + origVel[j][i] * dt);
            p.setVelocity(origVel[j][i] + origAcc[j][i] * dt);
            p.setForce(Eigen::Vector3f::Zero());
        }
    }

    // Compute forces at predicted state (x*, v*).
    // Note: handleCollision inside computeJellyForce may reflect the velocity of particles
    // whose predicted position penetrates the terrain. Reading getVelocity() after this call
    // gives the physically-corrected velocity at the predicted state, which is the correct
    // slope to use for x_{n+1}. This is consistent with how the framework embeds collision
    // response inside force computation.
    for (int j = 0; j < jellyCount; j++) {
        particleSystem.computeJellyForce(*particleSystem.getJellyPointer(j));
    }

    // 3. Refined update: x_{n+1} = x_n + v* * dt,  v_{n+1} = v_n + a* * dt
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        for (int i = 0; i < n; i++) {
            Particle& p = jelly->getParticle(i);
            Eigen::Vector3f v_star = p.getVelocity();
            Eigen::Vector3f a_star = p.getAcceleration();
            p.setPosition(origPos[j][i] + v_star * dt);
            p.setVelocity(origVel[j][i] + a_star * dt);
            p.setForce(Eigen::Vector3f::Zero());
        }
    }
}

//
// MidpointEulerIntegrator
//

IntegratorType MidpointEulerIntegrator::getType() { return IntegratorType::MidpointEuler; }

void MidpointEulerIntegrator::integrate(MassSpringSystem& particleSystem) {
    float dt = particleSystem.deltaTime;
    int jellyCount = particleSystem.getJellyCount();

    // Backup original state
    std::vector<std::vector<Eigen::Vector3f>> origPos(jellyCount);
    std::vector<std::vector<Eigen::Vector3f>> origVel(jellyCount);
    std::vector<std::vector<Eigen::Vector3f>> origAcc(jellyCount);

    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        origPos[j].resize(n);
        origVel[j].resize(n);
        origAcc[j].resize(n);
        for (int i = 0; i < n; i++) {
            origPos[j][i] = jelly->getParticle(i).getPosition();
            origVel[j][i] = jelly->getParticle(i).getVelocity();
            origAcc[j][i] = jelly->getParticle(i).getAcceleration();
        }
    }

    // Half-step to midpoint state
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        for (int i = 0; i < n; i++) {
            Particle& p = jelly->getParticle(i);
            p.setPosition(origPos[j][i] + origVel[j][i] * (dt * 0.5f));
            p.setVelocity(origVel[j][i] + origAcc[j][i] * (dt * 0.5f));
            p.setForce(Eigen::Vector3f::Zero());
        }
    }

    // Compute forces at midpoint
    for (int j = 0; j < jellyCount; j++) {
        particleSystem.computeJellyForce(*particleSystem.getJellyPointer(j));
    }

    // Full step using midpoint derivatives
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        for (int i = 0; i < n; i++) {
            Particle& p = jelly->getParticle(i);
            Eigen::Vector3f v_mid = p.getVelocity();
            Eigen::Vector3f a_mid = p.getAcceleration();
            p.setPosition(origPos[j][i] + v_mid * dt);
            p.setVelocity(origVel[j][i] + a_mid * dt);
            p.setForce(Eigen::Vector3f::Zero());
        }
    }
}

//
// RungeKuttaFourthIntegrator
//

IntegratorType RungeKuttaFourthIntegrator::getType() { return IntegratorType::RungeKuttaFourth; }

void RungeKuttaFourthIntegrator::integrate(MassSpringSystem& particleSystem) {
    // TODO#Bonus: Integrate velocity and acceleration
    //   1. Backup original particles' data.
    //   2. Compute k1, k2, k3, k4
    //   3. Compute refined Xn+1 using (1.) and (2.).
    // Note:
    //   1. Use `MassSpringSystem::computeJellyForce`
    //      with modified position and velocity to get Xn+1.
    //   2. See functions in `particle.h` if you don't know how to access data.
    //   3. StateStep struct is just a hint, you can use whatever you want.
    //   4. Review "ODE_basics.pptx" from p.21

    const float dt = particleSystem.deltaTime;
    int jellyCount = particleSystem.getJellyCount();

    // 1. Backup original state
    std::vector<std::vector<Eigen::Vector3f>> origPos(jellyCount);
    std::vector<std::vector<Eigen::Vector3f>> origVel(jellyCount);

    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        origPos[j].resize(n);
        origVel[j].resize(n);
        for (int i = 0; i < n; i++) {
            origPos[j][i] = jelly->getParticle(i).getPosition();
            origVel[j][i] = jelly->getParticle(i).getVelocity();
        }
    }

    // Helper: apply state offset and recompute forces, then collect derivatives
    using StateVec = std::vector<std::vector<Eigen::Vector3f>>;
    auto collectDerivatives = [&](float alpha, const StateVec& kVel, const StateVec& kAcc,
                                   StateVec& outVel, StateVec& outAcc) {
        // Set evaluation state and capture the velocity slope BEFORE computeJellyForce.
        // handleCollision (called inside computeJellyForce) directly calls setVelocity()
        // on particles whose temporary position penetrates the terrain. Reading getVelocity()
        // after that call would return a collision-flipped value, corrupting the k slope.
        outVel.resize(jellyCount);
        outAcc.resize(jellyCount);
        for (int j = 0; j < jellyCount; j++) {
            Jelly* jelly = particleSystem.getJellyPointer(j);
            int n = jelly->getParticleNum();
            outVel[j].resize(n);
            outAcc[j].resize(n);
            for (int i = 0; i < n; i++) {
                Particle& p = jelly->getParticle(i);
                p.setPosition(origPos[j][i] + alpha * kVel[j][i] * dt);
                // Velocity at this evaluation point is the slope for the position ODE (dx/dt = v).
                // Save it now, before computeJellyForce can overwrite it via collision response.
                outVel[j][i] = origVel[j][i] + alpha * kAcc[j][i] * dt;
                p.setVelocity(outVel[j][i]);
                p.setForce(Eigen::Vector3f::Zero());
            }
        }
        // Compute forces at this state to obtain accelerations (slopes for the velocity ODE).
        for (int j = 0; j < jellyCount; j++) {
            particleSystem.computeJellyForce(*particleSystem.getJellyPointer(j));
        }
        for (int j = 0; j < jellyCount; j++) {
            Jelly* jelly = particleSystem.getJellyPointer(j);
            int n = jelly->getParticleNum();
            for (int i = 0; i < n; i++) {
                outAcc[j][i] = jelly->getParticle(i).getAcceleration();
            }
        }
    };

    // 2. Compute k1, k2, k3, k4
    // k1: derivatives at (x_n, v_n) — forces already computed before integrate() is called
    StateVec k1v(jellyCount), k1a(jellyCount);
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        k1v[j].resize(n);
        k1a[j].resize(n);
        for (int i = 0; i < n; i++) {
            k1v[j][i] = origVel[j][i];
            k1a[j][i] = jelly->getParticle(i).getAcceleration();
        }
    }

    StateVec k2v, k2a, k3v, k3a, k4v, k4a;
    collectDerivatives(0.5f, k1v, k1a, k2v, k2a);
    collectDerivatives(0.5f, k2v, k2a, k3v, k3a);
    collectDerivatives(1.0f, k3v, k3a, k4v, k4a);

    // 3. Final update: x_{n+1} = x_n + dt/6*(k1+2k2+2k3+k4)
    for (int j = 0; j < jellyCount; j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int n = jelly->getParticleNum();
        for (int i = 0; i < n; i++) {
            Particle& p = jelly->getParticle(i);
            p.setPosition(origPos[j][i] + (dt / 6.0f) *
                (k1v[j][i] + 2.0f * k2v[j][i] + 2.0f * k3v[j][i] + k4v[j][i]));
            p.setVelocity(origVel[j][i] + (dt / 6.0f) *
                (k1a[j][i] + 2.0f * k2a[j][i] + 2.0f * k3a[j][i] + k4a[j][i]));
            p.setForce(Eigen::Vector3f::Zero());
        }
    }
}

IntegratorType PositionBasedDynamicIntegrator::getType() { return IntegratorType::PositionBasedDynamic; }

void PositionBasedDynamicIntegrator::integrate(MassSpringSystem& particleSystem) {
    // TODO#4-3: Integrate position and velocity
    //   1. Prediction Step
    //   2. Constraint Projection
    //   3. Position and Velocity Update with XSPH Viscosity

    const float dt = particleSystem.deltaTime;
    const int solverIterations = 4;
    const float xsphFactor = 0.1f;

    for (int j = 0; j < particleSystem.getJellyCount(); j++) {
        Jelly* jelly = particleSystem.getJellyPointer(j);
        int numParticles = jelly->getParticleNum();

        // Cell size defines min/max constraint distances
        float cellSize = jelly->getLength() / static_cast<float>(jelly->getNumAtEdge() - 1);
        float minDistance = cellSize * 0.75f;

        // Save original positions
        std::vector<Eigen::Vector3f> oldPos(numParticles);
        for (int i = 0; i < numParticles; i++) {
            oldPos[i] = jelly->getParticle(i).getPosition();
        }

        // 1. Prediction Step: identical to explicit Euler
        //    v* = v_n + a_n * dt  (velocity updated with acceleration first)
        //    x* = x_n + v* * dt  (position uses updated velocity to encode acceleration)
        //    This is required so Step 3 can recover v* = (x* - x_n) / dt correctly.
        for (int i = 0; i < numParticles; i++) {
            Particle& p = jelly->getParticle(i);
            Eigen::Vector3f vel = p.getVelocity();    // v_n
            Eigen::Vector3f acc = p.getAcceleration(); // a_n
            Eigen::Vector3f predVel = vel + acc * dt;  // v* = v_n + a_n * dt
            p.setVelocity(predVel);
            p.setPredictedPosition(oldPos[i] + predVel * dt); // x* = x_n + v* * dt
            p.setForce(Eigen::Vector3f::Zero());
        }

        // Determine active terrain normal and position for constraint step
        Eigen::Vector3f terrainNormal = Eigen::Vector3f::Zero();
        Eigen::Vector3f terrainPos = Eigen::Vector3f::Zero();
        bool hasElevator = false;
        if (particleSystem.sceneIdx == 0 && particleSystem.planeTerrain) {
            // Plane rotated -20 degrees around Z-axis
            terrainNormal = Eigen::Vector3f(std::sin(util::PI<float>() / 9.0f),
                                            std::cos(util::PI<float>() / 9.0f), 0.0f);
            terrainPos = particleSystem.planeTerrain->getPosition();
        } else if (particleSystem.sceneIdx == 1 && particleSystem.elevatorTerrain) {
            terrainNormal = Eigen::Vector3f(0.0f, 1.0f, 0.0f);
            terrainPos = particleSystem.elevatorTerrain->getPosition();
            hasElevator = true;
        }

        // 2. Constraint Projection
        for (int iter = 0; iter < solverIterations; iter++) {
            // Self Collision: spring-based distance constraints
            //   minDistance depends on cell size; maxDistance is the spring rest length
            //   If too close (dist < minDistance) -> push apart symmetrically
            //   If too far  (dist > maxDistance) -> pull together symmetrically
            for (int s = 0; s < jelly->getSpringNum(); s++) {
                Spring& spring = jelly->getSpring(s);
                int a = spring.getSpringStartID();
                int b = spring.getSpringEndID();

                Eigen::Vector3f pa = jelly->getParticle(a).getPredictedPosition();
                Eigen::Vector3f pb = jelly->getParticle(b).getPredictedPosition();
                Eigen::Vector3f delta = pb - pa;
                float d = delta.norm();
                // maxDistance is based on cell size (not spring rest length) so the constraint
                // acts as a safety net against extreme deformation, not as a redundant spring.
                // Using rest length would fire on any stretching and double-count spring forces.
                float maxDistance = cellSize * 2.0f;

                if (d < 1e-8f) continue;

                if (d < minDistance) {
                    // Too close: push apart to minDistance
                    Eigen::Vector3f correction = 0.5f * (d - minDistance) / d * delta;
                    jelly->getParticle(a).setPredictedPosition(pa + correction);
                    jelly->getParticle(b).setPredictedPosition(pb - correction);
                } else if (d > maxDistance) {
                    // Too far: pull together to maxDistance
                    Eigen::Vector3f correction = 0.5f * (d - maxDistance) / d * delta;
                    jelly->getParticle(a).setPredictedPosition(pa + correction);
                    jelly->getParticle(b).setPredictedPosition(pb - correction);
                }
            }

            // Terrain Constraint: similar to self collision, push particle back above surface
            if (terrainNormal.norm() > 0.5f) {
                for (int i = 0; i < numParticles; i++) {
                    Particle& p = jelly->getParticle(i);
                    Eigen::Vector3f predPos = p.getPredictedPosition();

                    // Elevator has a finite 5x5 footprint
                    if (hasElevator) {
                        constexpr float halfWidth = 2.5f;
                        if (std::abs(predPos[0] - terrainPos[0]) > halfWidth ||
                            std::abs(predPos[2] - terrainPos[2]) > halfWidth)
                            continue;
                    }

                    float dist = (predPos - terrainPos).dot(terrainNormal);
                    if (dist < 0.0f) {
                        // Push particle back onto surface
                        p.setPredictedPosition(predPos - dist * terrainNormal);
                    }
                }
            }
        }

        // 3. Position and Velocity Update
        // Derive velocity from constrained position change, then commit positions
        std::vector<Eigen::Vector3f> newVel(numParticles);
        for (int i = 0; i < numParticles; i++) {
            newVel[i] = (jelly->getParticle(i).getPredictedPosition() - oldPos[i]) / dt;
        }

        // Build spring-connected neighbor list for XSPH
        std::vector<std::vector<int>> neighbors(numParticles);
        for (int s = 0; s < jelly->getSpringNum(); s++) {
            int a = jelly->getSpring(s).getSpringStartID();
            int b = jelly->getSpring(s).getSpringEndID();
            neighbors[a].push_back(b);
            neighbors[b].push_back(a);
        }

        // XSPH velocity smoothing with distance-based weights:
        //   w = 1 / distance(p, q)
        //   velocity_correction = sum(w * (v_q - v_p)) / weight_sum
        std::vector<Eigen::Vector3f> finalVel(numParticles);
        for (int i = 0; i < numParticles; i++) {
            Eigen::Vector3f pi = jelly->getParticle(i).getPredictedPosition();
            float weightSum = 0.0f;
            Eigen::Vector3f velocityCorrection = Eigen::Vector3f::Zero();

            for (int q : neighbors[i]) {
                Eigen::Vector3f pq = jelly->getParticle(q).getPredictedPosition();
                float dist = (pi - pq).norm();
                if (dist < 1e-8f) continue;
                float w = 1.0f / dist;
                weightSum += w;
                velocityCorrection += w * (newVel[q] - newVel[i]);
            }

            finalVel[i] = newVel[i];
            if (weightSum > 1e-8f) {
                finalVel[i] += xsphFactor * velocityCorrection / weightSum;
            }
        }

        // Apply final constrained positions and XSPH-smoothed velocities
        for (int i = 0; i < numParticles; i++) {
            Particle& p = jelly->getParticle(i);
            p.setPosition(p.getPredictedPosition());
            p.setVelocity(finalVel[i]);
            p.setForce(Eigen::Vector3f::Zero());
        }
    }
}

}  // namespace simulation
