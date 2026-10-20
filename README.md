# Task 08-Fireworks

This is my implementation of simulation of fireworks, using a 3D particle system.
Individual particles obey the laws of physics (gravity and resistance), but don't interact with each other.
The entire 3D scene is rendered using the OpenGL library and Silk.NET library.

## Run

Install the .NET 8 SDK (or a newer SDK with the .NET 8 runtime) and use a machine with OpenGL support.
From this repository's root, run:

```sh
dotnet run --project 08-Fireworks.csproj
```

Press **F** to launch a rocket, **Space** to toggle automatic launching, and **Escape** to quit.
Use the mouse to rotate the view and zoom.

The shared rendering utilities are included in `Silk/`, so no sibling coursework projects are required.
This project originated in the NPGR003 coursework repository; the original MIT license is included in `LICENSE`.

## Simulation
The simulation is advanced once per frame from OnRender() by calling Simulation.SimulateTo(nowSeconds), where nowSeconds is current time in seconds.

### Overview

The system contains a dynamic list of particles of three kinds:

- Rocket: launched from one of several fixed launchers on the ground.

- Spark: produced by rocket explosions (and optionally by secondary “mini” explosions).

- Trace: very short-lived particles used to visualize trajectories (placed at the previous position of rockets/sparks).

The simulation is time-based (seconds as double). Each particle stores its own last simulated time, so Particle.SimulateTo(time) integrates motion forward by dt = time - lastTime.

### Particle lifecycle

Each particle has:

- Position
- Velocity
- Age (counts down; particle is removed when Age <= 0)
- Color
- Size (fade/shrink near the end of life)
- Mass
- Linear and quadratic drag coefficients

Motion is updated using simple Euler integration under:

- Gravity ((0, -9.81, 0))
- Drag (-k1 * v - k2 * |v| * v)

Sparks are also removed early if they hit the ground (Position.Y <= 0).

### Launchers and rocket spawning

Simulation owns a list of Launcher objects, each with a world-space Position and an emission Rate (rockets per second).
When Simulation.firing is enabled, each launcher uses an internal accumulator to emit the correct number of rockets for the elapsed dt (fractional rockets accumulate over time).

Pressing Space toggles firing on/off, so you can pause continuous launching.
You can also press F to fire a single rocket.

### Rocket explosions and primary sparks

Rockets fly upward and are removed when either:

- Their Age expires, or
- They reach a randomized ExplodeHeight (and WillExplode is true)

When a rocket explodes, it is retired and Simulation spawns a burst of primary sparks at the rocket’s position. The number of sparks depends on RocketType (different rocket types spawn different burst sizes), and is capped so the system does not exceed MaxParticles.

#### Secondary explosions (grouped per rocket)

Each rocket decides at creation time whether its burst will have secondary explosions(about 15% of rockets). If not enabled, none of those primary sparks can trigger a secondary burst. A spark that is allowed to secondary-explode will do so once near the end of its life. Simulation then spawns a small mini-burst of sparks at that spark’s position. These mini-burst sparks have AllowSecondary = false, so chaining is prevented.

#### Trace particles (trajectory visualization)

During simulation, for every alive rocket or spark, a short-lived Trace particle is spawned at its PrevPosition. These trace particles:

- have very small Age (e.g., 0.1)
- use a dimmed version of the parent particle’s color
- have a small point size

This produces visible trails behind moving rockets and sparks.

### Particle limit handling

Simulation.SimulateTo() collects newly created particles into a temporary toAdd list while iterating (to avoid modifying the main list during traversal). After simulation, dead particles are removed, new particles from toAdd are appended only up to the available free capacity (MaxParticles - currentCount). This prevents overflowing the vertex buffer while still allowing bursts and trails to appear consistently.


# Bonus features implemented:
* Launch ramps visualization
* Multiple rocket/particle types
* Multi-stage explosions
* Visualization of rocket trajectories
* Interactive fireworks control


# RESULT

At the begging of the video the automatic launching is turned off, and I am firing rockets only by pressing the F key.
Then I press the Space key and all the launchers start launching rockets periodically.
I can also use the mouse to turn around and zoom in/out of the simulation.
We can also see that rockets leave traces, and some can exlode into sparkles, sometimes even twice, while others don't explode at all.
In the bottom we can see the launchers.

[Watch the fireworks recording](./recording.mp4)
