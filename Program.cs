using System;
using System.Diagnostics;
using CommandLine;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Globalization;
using System.Text;
using Util;
// ReSharper disable InconsistentNaming
// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace _08_Fireworks;

using Vector3 = Vector3D<float>;
using Matrix4 = Matrix4X4<float>;

public class Options
{
  [Option('w', "width", Required = false, Default = 800, HelpText = "Window width in pixels.")]
  public int WindowWidth { get; set; } = 800;

  [Option('h', "height", Required = false, Default = 600, HelpText = "Window height in pixels.")]
  public int WindowHeight { get; set; } = 600;

  [Option('p', "particles", Required = false, Default = 10000, HelpText = "Maximum number of particles.")]
  public int Particles { get; set; } = 10000;

  [Option('r', "rate", Required = false, Default = 1000.0, HelpText = "Particle generation rate per second.")]
  public double ParticleRate { get; set; } = 1000.0;

  [Option('t', "texture", Required = false, Default = ":check:", HelpText = "User-defined texture.")]
  public string TextureFile { get; set; } = ":check:";
}

enum ParticleKind { Rocket, Spark, Trace }

/// <summary>
/// Single particle.
/// </summary>
class Particle
{
  public static Random rnd = new((int)DateTime.Now.Ticks);

  public void SetPosition (Vector3 p) => Position = p;

  private static readonly Vector3 Gravity = new(0.0f, -9.81f, 0.0f);

  public Vector3 PrevPosition { get; private set; }

  public int RocketType { get; private set; } = 0;

  public bool DidSecondary { get; private set; } = false;

  public bool AllowSecondary { get; internal set; } = false;

  public bool BurstHasSecondary { get; private set; } = false;

  private static float Length (Vector3 v)
    => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

  /// <summary>
  /// Create a new particle.
  /// </summary>
  public Particle (double now, ParticleKind kind = ParticleKind.Rocket)
  => Reset(now, kind);

  public bool WillExplode { get; private set; } = true;

  /// <summary>
  /// World space coordinates of the object's center.
  /// </summary>
  public Vector3 Position { get; internal set; }

  /// <summary>
  /// Current RGB color.
  /// </summary>
  public Vector3 Color { get; internal set; }

  /// <summary>
  /// Current point size in pixels.
  /// </summary>
  public float Size { get; internal set; }

  /// <summary>
  /// Current age (time-to-death).
  /// </summary>
  public double Age { get; set; }

  public ParticleKind Kind { get; private set; }

  public Vector3 Velocity { get; internal set; }

  public float Mass { get; private set; } = 1.0f;

  // Drag coefficients
  public float DragLinear { get; private set; } = 0.4f;
  public float DragQuadratic { get; private set; } = 0.05f;

  public bool Exploded { get; private set; } = false;

  public float ExplodeHeight { get; private set; } = 4.0f;

  /// <summary>
  /// Last simulated time in seconds.
  /// </summary>
  private double SimulatedTime;

  public void Reset (double now, ParticleKind kind)
  {
    Kind = kind;

    // small random horizontal offset
    float x = (float)(0.2 * (rnd.NextDouble() - 0.5));
    float z = (float)(0.2 * (rnd.NextDouble() - 0.5));
    float y = 0.0f;

    Position = new Vector3(x, y, z);

    Color = new Vector3(
      (float)(0.3 + 0.7 * rnd.NextDouble()),
      (float)(0.3 + 0.7 * rnd.NextDouble()),
      (float)(0.3 + 0.7 * rnd.NextDouble()));

    if (Kind == ParticleKind.Rocket)
    {
      float vx = (float)(2.5 * (rnd.NextDouble() - 0.5));
      float vz = (float)(2.5 * (rnd.NextDouble() - 0.5));
      float vy = (float)(20.0 + 10.0 * rnd.NextDouble()); // upward speed

      Velocity = new Vector3(vx, vy, vz);

      Size = 4.0f;
      Age = 2.0 + 1.0 * rnd.NextDouble(); // 2–3 seconds

      Mass = 1.0f;
      DragLinear = 0.3f;
      DragQuadratic = 0.03f;

      Exploded = false;
      ExplodeHeight = (float)(12.0 + 6.0 * rnd.NextDouble());

      WillExplode = rnd.NextDouble() >= 0.20;

      RocketType = rnd.Next(3);

      BurstHasSecondary = (rnd.NextDouble() < 0.15);
    }
    else
    {
      // Sparks
      float vx = (float)(6.0 * (rnd.NextDouble() - 0.5));
      float vy = (float)(6.0 * (rnd.NextDouble() - 0.5));
      float vz = (float)(6.0 * (rnd.NextDouble() - 0.5));

      Velocity = new Vector3(vx, vy, vz);

      Size = 2.0f;
      Age = 0.5 + 0.8 * rnd.NextDouble();

      Mass = 1.0f;
      DragLinear = 0.8f;
      DragQuadratic = 0.08f;

      DidSecondary = false;
    }

    SimulatedTime = now;
  }

  public void SpawnSparks (List<Particle> toAdd, double nowSeconds, int count)
  {
    for (int i = 0; i < count; i++)
    {
      var s = new Particle(nowSeconds, ParticleKind.Spark);

      // start at rocket position
      s.Position = this.Position;

      // give sparks velocity in random directions, plus some of the rocket velocity
      double u = rnd.NextDouble();
      double v = rnd.NextDouble();
      double theta = 2.0 * Math.PI * u;
      double phi = Math.Acos(2.0 * v - 1.0);

      float dx = (float)(Math.Sin(phi) * Math.Cos(theta));
      float dy = (float)(Math.Cos(phi));
      float dz = (float)(Math.Sin(phi) * Math.Sin(theta));

      float speedS = (float)(10.0 + 15.0 * rnd.NextDouble());
      s.Velocity = new Vector3(dx, dy, dz) * speedS + 0.2f * this.Velocity;

      // sparks inherit rocket color
      s.Color = this.Color;

      s.AllowSecondary = this.BurstHasSecondary; // 5% of sparks

      toAdd.Add(s);
    }
  }

  /// <summary>
  /// Simulate one step in time.
  /// </summary>
  /// <param name="time">Target time to simulate to (in seconds).</param>
  /// <returns></returns>
  public bool SimulateTo (double time)
  {
    if (time <= SimulatedTime)
      return true;

    double dtD = time - SimulatedTime;
    SimulatedTime = time;

    Age -= dtD;
    if (Age <= 0.0)
      return false;

    float dt = (float)dtD;

    // gravity: Fg = m * g
    Vector3 force = Mass * Gravity;

    // drag: Fd = -k1*v - k2*|v|*v
    float speed = Length(Velocity);
    if (speed > 1e-6f)
    {
      force += (-DragLinear) * Velocity;
      force += (-DragQuadratic * speed) * Velocity;
    }

    // Acceleration
    Vector3 acc = (1.0f / Mass) * force;

    PrevPosition = Position;

    // Euler integration
    Velocity = Velocity + acc * dt;
    Position = Position + Velocity * dt;

    // Mark rocket for explosion when it is high enough
    if (Kind == ParticleKind.Rocket && WillExplode && !Exploded && Position.Y >= ExplodeHeight)
    {
      Exploded = true;
      return false; // retire rocket
    }

    if (Kind == ParticleKind.Spark && AllowSecondary && !DidSecondary && Age < 0.15)
    {
      DidSecondary = true;
      Exploded = true;
      return false;
    }


    // Kill particle if it hits the ground
    if (Kind == ParticleKind.Spark && Position.Y <= 0f)
      return false;

    if (Age < 1.5)
      Color *= (float)Math.Pow(0.6, dtD);

    if (Age < 1.0)
      Size *= (float)Math.Pow(0.7, dtD);

    return true;
  }

  public void FillBuffer (float[] buffer, ref int i)
  {
    // offset  0: Position
    buffer[i++] = Position.X;
    buffer[i++] = Position.Y;
    buffer[i++] = Position.Z;

    // offset  3: Color
    buffer[i++] = Color.X;
    buffer[i++] = Color.Y;
    buffer[i++] = Color.Z;

    // offset  6: Normal
    buffer[i++] = 0.0f;
    buffer[i++] = 1.0f;
    buffer[i++] = 0.0f;

    // offset  9: Txt coordinates
    buffer[i++] = 0.5f;
    buffer[i++] = 0.5f;

    // offset 11: Point size
    buffer[i++] = Size;
  }
}

class Launcher
{
  private static readonly Random rnd = new((int)DateTime.Now.Ticks);

  public Vector3 Position { get; set; } = new Vector3(0, 0, 0);

  // rockets per second
  public double Rate { get; set; } = 3.0;

  // internal accumulator for fractional emission
  private double accumulator = 0.0;

  public void SimulateStep (double dt, List<Particle> toAdd, double nowSeconds)
  {
    accumulator += dt * Rate;

    int n = (int)Math.Floor(accumulator);
    if (n <= 0)
      return;

    accumulator -= n;

    while (n-- > 0)
    {
      var p = new Particle(nowSeconds, ParticleKind.Rocket);
      p.SetPosition(Position);
      toAdd.Add(p);
    }
  }
}

public class Simulation
{

  public bool firing = true;
  /// <summary>
  /// Dynamic array of all current particles.
  /// </summary>
  private List<Particle> particles = new();

  /// <summary>
  /// Actual number of particles.
  /// </summary>
  public int Particles => particles.Count;

  /// <summary>
  /// Particle number limit.
  /// </summary>
  public int MaxParticles { get; private set; }

  /// <summary>
  /// Last simulated time in seconds.
  /// </summary>
  private double SimulatedTime;

  /// <summary>
  /// Number of particles generated in one second.
  /// </summary>
  public double ParticleRate { get; set; }

  private List<Launcher> launchers = new();

  /// <summary>
  /// Initialize a new particle simulator.
  /// </summary>
  /// <param name="now">Current real time in seconds.</param>
  /// <param name="particleRate">Maximum particle generation rate in particles per second.</param>
  /// <param name="maxParticles">Maximum number of particles in the system (can be slightly exceeded).</param>
  /// <param name="initParticles">Initial number of particles (the rest will be generated later).</param>
  public Simulation (double now, double particleRate, int maxParticles, int initParticles)
  {
    SimulatedTime = now;
    ParticleRate = particleRate;
    MaxParticles = maxParticles;
    Generate(initParticles);

    launchers.Add(new Launcher
    {
      Position = new Vector3(0, 0, 0),
      Rate = Math.Max(0.1, ParticleRate / 4000.0)
    });

    launchers.Add(new Launcher
    {
      Position = new Vector3(1, 0, 0),
      Rate = Math.Max(0.1, ParticleRate / 3000.0)
    });

    launchers.Add(new Launcher
    {
      Position = new Vector3(-1, 0, 0),
      Rate = Math.Max(0.1, ParticleRate / 5000.0)
    });

    launchers.Add(new Launcher
    {
      Position = new Vector3(0, 0, -1),
      Rate = Math.Max(0.1, ParticleRate / 4500.0)
    });

  }

  private void Generate(int number)
  {
    if (number <= 0)
      return;

    while (number-- > 0)
    {
      // Generate one new particle
      particles.Add(new(SimulatedTime));
    }
  }

  public void SimulateTo (double time)
  {
    if (time <= SimulatedTime)
      return;

    double dt = time - SimulatedTime;
    SimulatedTime = time;

    // Collect new particles here
    List<Particle> toAdd = new();

    // Launchers emit new rockets continuously
    if (this.firing)
      foreach (var l in launchers)
        l.SimulateStep(dt, toAdd, time);

    // Simulate all the particles
    List<int> toRemove = new();
    for (int i = 0; i < particles.Count; i++)
    {
      if (!particles[i].SimulateTo(time))
      {

        // If a rocket exploded, spawn sparks
        if (particles[i].Kind == ParticleKind.Rocket && particles[i].Exploded)
        {
          // cap to avoid exceeding MaxParticles
          int freeNow = MaxParticles - (particles.Count + toAdd.Count);
          int baseSparks = 120;
          if (particles[i].RocketType == 1)
            baseSparks = 180;
          if (particles[i].RocketType == 2)
            baseSparks = 240;

          int sparks = Math.Min(baseSparks, Math.Max(0, freeNow));
          particles[i].SpawnSparks(toAdd, time, sparks);
        }

        toRemove.Add(i);
      }
      else if (particles[i].Kind == ParticleKind.Rocket || particles[i].Kind == ParticleKind.Spark)
        {
          int freeNow = MaxParticles - (particles.Count + toAdd.Count);
          if (freeNow > 0)
          {
            var t = new Particle(time, ParticleKind.Trace);
            t.Position = particles[i].PrevPosition;
            t.Velocity = new Vector3(0, 0, 0);
            t.Color = particles[i].Color * 0.5f;
            t.Size = 1.5f;
            t.Age = 0.1f;
            toAdd.Add(t);
        }
      }

      if (particles[i].Kind == ParticleKind.Spark && particles[i].Exploded)
      {
        int freeNow = MaxParticles - (particles.Count + toAdd.Count);
        int mini = Math.Min(40, Math.Max(0, freeNow));

        // mini burst centered at that spark
        for (int k = 0; k < mini; k++)
        {
          var s2 = new Particle(time, ParticleKind.Spark);
          s2.Position = particles[i].Position;
          s2.Color = particles[i].Color;
          // give it a smaller spherical velocity
          s2.Velocity = new Vector3(
            (float)(8.0 * (Particle.rnd.NextDouble() - 0.5)),
            (float)(8.0 * (Particle.rnd.NextDouble() - 0.5)),
            (float)(8.0 * (Particle.rnd.NextDouble() - 0.5))
          );
          s2.Age = 0.3 + 0.4 * Particle.rnd.NextDouble();
          s2.Size = 1.5f;
          s2.AllowSecondary = false;  // prevents chaining
          toAdd.Add(s2);
        }
      }
    }

    // Remove dead ones
    for (var i = toRemove.Count; --i >= 0;)
      particles.RemoveAt(toRemove[i]);

    // Add newly spawned particles, but never exceed MaxParticles
    int free = MaxParticles - particles.Count;
    if (free > 0 && toAdd.Count > 0)
    {
      int addCount = Math.Min(free, toAdd.Count);
      for (int i = 0; i < addCount; i++)
        particles.Add(toAdd[i]);
    }
  }

  public void FireOnce (Vector3 position)
  {
    if (particles.Count >= MaxParticles)
      return;
    var p = new Particle(SimulatedTime, ParticleKind.Rocket);
    p.SetPosition(position);
    particles.Add(p);
  }

  /// <summary>
  /// [Re]fills the vertex buffer with current data.
  /// </summary>
  /// <param name="buffer">Vertex buffer array.</param>
  /// <returns>Number of particles to update and render.</returns>
  public int FillBuffer (float[] buffer)
  {
    int i = 0;

    // particles
    foreach (var p in particles)
      p.FillBuffer(buffer, ref i);

    int particleVertices = particles.Count;

    // launcher markers
    foreach (var l in launchers)
    {
      // Position
      buffer[i++] = l.Position.X;
      buffer[i++] = l.Position.Y;
      buffer[i++] = l.Position.Z;

      // Color
      buffer[i++] = 1.0f;
      buffer[i++] = 1.0f;
      buffer[i++] = 1.0f;

      // Normal
      buffer[i++] = 0.0f;
      buffer[i++] = 1.0f;
      buffer[i++] = 0.0f;

      // Tex coords
      buffer[i++] = 0.5f;
      buffer[i++] = 0.5f;

      // Size
      buffer[i++] = 10.0f;
    }

    return particleVertices + launchers.Count;
  }

  public void changeFiring()
  {
    this.firing = !this.firing;
  }

  /// <summary>
  /// Removes all the particles.
  /// </summary>
  public void Reset()
  {
    particles.Clear();
  }
}

internal class Program
{
  // System objects.
  private static IWindow? window;
  private static GL? Gl;

  // VB locking (too lousy?)
  private static object renderLock = new();

  // Window size.
  private static float width;
  private static float height;

  // Trackball.
  private static Trackball? tb;

  // FPS counter.
  private static FPS fps = new();

  // Scene dimensions.
  private static Vector3 sceneCenter = new Vector3(0.0f, 10.0f, 0.0f);
  private static float sceneDiameter = 1.5f;

  // Global 3D data buffer.
  private const int MAX_VERTICES = 65536;
  private const int VERTEX_SIZE = 12;     // x, y, z, R, G, B, Nx, Ny, Nz, s, t, size

  /// <summary>
  /// Current dynamic vertex buffer in .NET memory.
  /// Better idea is to store the buffer on GPU and update it every frame.
  /// </summary>
  private static float[] vertexBuffer = new float[MAX_VERTICES * VERTEX_SIZE];

  /// <summary>
  /// Current number of vertices to draw.
  /// </summary>
  private static int vertices = 0;

  public static int maxParticles = 0;
  public static double particleRate = 1000.0;

  private static BufferObject<float>? Vbo;
  private static VertexArrayObject<float>? Vao;

  // Texture.
  private static Util.Texture? texture;
  private static bool useTexture = false;
  private static string textureFile = ":check:";
  private const int TEX_SIZE = 128;

  // Lighting.
  private static bool usePhong = false;

  // Shader program.
  private static ShaderProgram? ShaderPrg;

  private static double nowSeconds = FPS.NowInSeconds;

  // Particle simulation system.
  private static Simulation? sim;

  //////////////////////////////////////////////////////
  // Application.

  private static string WindowTitle()
  {
    StringBuilder sb = new("08-Fireworks");

    if (sim != null)
    {
      sb.Append(string.Format(CultureInfo.InvariantCulture, " [{0} of {1}], rate={2:f0}", sim.Particles, sim.MaxParticles, sim.ParticleRate));
    }

    sb.Append(string.Format(CultureInfo.InvariantCulture, ", fps={0:f1}", fps.Fps));
    if (window != null &&
        window.VSync)
      sb.Append(" [VSync]");

    double pps = fps.Pps;
    if (pps > 0.0)
      if (pps < 5.0e5)
        sb.Append(string.Format(CultureInfo.InvariantCulture, ", pps={0:f1}k", pps * 1.0e-3));
      else
        sb.Append(string.Format(CultureInfo.InvariantCulture, ", pps={0:f1}m", pps * 1.0e-6));

    if (tb != null)
    {
      sb.Append(tb.UsePerspective ? ", perspective" : ", orthographic");
      sb.Append(string.Format(CultureInfo.InvariantCulture, ", zoom={0:f2}", tb.Zoom));
    }

    if (useTexture &&
        texture != null &&
        texture.IsValid())
      sb.Append($", txt={texture.name}");
    else
      sb.Append(", no texture");

    if (usePhong)
      sb.Append(", Phong shading");

    return sb.ToString();
  }

  private static void SetWindowTitle()
  {
    if (window != null)
      window.Title = WindowTitle();
  }

  private static void Main(string[] args)
  {
    Parser.Default.ParseArguments<Options>(args)
      .WithParsed<Options>(o =>
      {
        WindowOptions options = WindowOptions.Default;
        options.Size = new Vector2D<int>(o.WindowWidth, o.WindowHeight);
        options.Title = WindowTitle();
        options.PreferredDepthBufferBits = 24;
        options.VSync = true;

        window = Window.Create(options);
        width  = o.WindowWidth;
        height = o.WindowHeight;

        window.Load    += OnLoad;
        window.Render  += OnRender;
        window.Closing += OnClose;
        window.Resize  += OnResize;

        textureFile = o.TextureFile;
        maxParticles = Math.Min(MAX_VERTICES, o.Particles);
        particleRate = o.ParticleRate;

        window.Run();
      });
  }

  private static void VaoPointers()
  {
    Debug.Assert(Vao != null);
    Vao.Bind();
    Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, VERTEX_SIZE,  0);
    Vao.VertexAttributePointer(1, 3, VertexAttribPointerType.Float, VERTEX_SIZE,  3);
    Vao.VertexAttributePointer(2, 3, VertexAttribPointerType.Float, VERTEX_SIZE,  6);
    Vao.VertexAttributePointer(3, 2, VertexAttribPointerType.Float, VERTEX_SIZE,  9);
    Vao.VertexAttributePointer(4, 1, VertexAttribPointerType.Float, VERTEX_SIZE, 11);
  }

  private static void OnLoad()
  {
    Debug.Assert(window != null);

    // Initialize all the inputs (keyboard + mouse).
    IInputContext input = window.CreateInput();
    for (int i = 0; i < input.Keyboards.Count; i++)
    {
      input.Keyboards[i].KeyDown += KeyDown;
      input.Keyboards[i].KeyUp   += KeyUp;
    }
    for (int i = 0; i < input.Mice.Count; i++)
    {
      input.Mice[i].MouseDown   += MouseDown;
      input.Mice[i].MouseUp     += MouseUp;
      input.Mice[i].MouseMove   += MouseMove;
      input.Mice[i].DoubleClick += MouseDoubleClick;
      input.Mice[i].Scroll      += MouseScroll;
    }

    // OpenGL global reference (shortcut).
    Gl = GL.GetApi(window);

    //------------------------------------------------------
    // Render data.

    // Init the rendering data.
    lock (renderLock)
    {
      // Initialize the simulation object and fill the VB.
      sim = new Simulation(nowSeconds, particleRate, maxParticles, 0);
      vertices = sim.FillBuffer(vertexBuffer);

      // Vertex Array Object = Vertex buffer + Index buffer.
      Vbo = new BufferObject<float>(Gl, vertexBuffer, BufferTargetARB.ArrayBuffer);
      Vao = new VertexArrayObject<float>(Gl, Vbo);
      VaoPointers();

      // Initialize the shaders.
      ShaderPrg = new ShaderProgram(Gl, "vertex.glsl", "fragment.glsl");

      // Initialize the texture.
      if (textureFile.StartsWith(":"))
      {
        // Generated texture.
        texture = new(TEX_SIZE, TEX_SIZE, textureFile);
        texture.GenerateTexture(Gl);
      }
      else
      {
        texture = new(textureFile, textureFile);
        texture.OpenglTextureFromFile(Gl);
      }

      // Trackball.
      tb = new(sceneCenter, sceneDiameter);
      tb.Zoom = 0.07f;
    }

    // Main window.
    SetWindowTitle();
    SetupViewport();
  }

  /// <summary>
  /// Mouse horizontal scaling coefficient.
  /// One unit/pixel of mouse movement corresponds to this distance in world space.
  /// </summary>
  private static float mouseCx =  0.001f;

  /// <summary>
  /// Mouse vertical scaling coefficient.
  /// Vertical scaling is just negative value of horizontal one.
  /// </summary>
  private static float mouseCy = -0.001f;

  /// <summary>
  /// Does all necessary steps after window setup/resize.
  /// Assumes valid values in 'width' and 'height'.
  /// </summary>
  private static void SetupViewport()
  {
    // OpenGL viewport.
    Gl?.Viewport(0, 0, (uint)width, (uint)height);

    tb?.ViewportChange((int)width, (int)height, 0.05f, 20.0f);

    // The tight coordinate is used for mouse scaling.
    float minSize = Math.Min(width, height);
    mouseCx = sceneDiameter / minSize;
    // Vertical mouse scaling is just negative...
    mouseCy = -mouseCx;
  }

  /// <summary>
  /// Called after window resize.
  /// </summary>
  /// <param name="newSize">New window size in pixels.</param>
  private static void OnResize(Vector2D<int> newSize)
  {
    width  = newSize[0];
    height = newSize[1];
    SetupViewport();
  }

  /// <summary>
  /// Called every time the content of the window should be redrawn.
  /// </summary>
  /// <param name="obj"></param>
  private static unsafe void OnRender(double obj)
  {
    Debug.Assert(Gl != null);
    Debug.Assert(ShaderPrg != null);
    Debug.Assert(tb != null);

    Gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

    lock (renderLock)
    {
      // Simulation the particle system.
      nowSeconds = FPS.NowInSeconds;
      if (sim != null)
      {
        sim.SimulateTo(nowSeconds);
        vertices = sim.FillBuffer(vertexBuffer);
      }

      // Rendering properties (set in every frame for clarity).
      Gl.Enable(GLEnum.DepthTest);
      Gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
      Gl.Disable(GLEnum.CullFace);
      Gl.Enable(GLEnum.VertexProgramPointSize);

      // Draw the scene (set of Object-s).
      VaoPointers();
      ShaderPrg.Use();

      // Shared shader uniforms - matrices.
      ShaderPrg.TrySetUniform("view", tb.View);
      ShaderPrg.TrySetUniform("projection", tb.Projection);
      ShaderPrg.TrySetUniform("model", Matrix4.Identity);

      // Shared shader uniforms - Phong shading.
      ShaderPrg.TrySetUniform("lightColor", 1.0f, 1.0f, 1.0f);
      ShaderPrg.TrySetUniform("lightPosition", -8.0f, 8.0f, 8.0f);
      ShaderPrg.TrySetUniform("eyePosition", tb.Eye);
      ShaderPrg.TrySetUniform("Ka", 0.1f);
      ShaderPrg.TrySetUniform("Kd", 0.7f);
      ShaderPrg.TrySetUniform("Ks", 0.3f);
      ShaderPrg.TrySetUniform("shininess", 60.0f);
      ShaderPrg.TrySetUniform("usePhong", usePhong);

      // Shared shader uniforms - Texture.
      if (texture == null || !texture.IsValid())
        useTexture = false;
      ShaderPrg.TrySetUniform("useTexture", useTexture);
      ShaderPrg.TrySetUniform("tex", 0);
      if (useTexture)
        texture?.Bind(Gl);

      // Draw the particle system.
      vertices = (sim != null) ? sim.FillBuffer(vertexBuffer) : 0;

      if (Vbo != null &&
          vertices > 0)
      {
        Vbo.UpdateData(vertexBuffer, 0, vertices * VERTEX_SIZE);

        // Draw the batch of points.
        Gl.DrawArrays((GLEnum)PrimitiveType.Points, 0, (uint)vertices);

        // Update Pps.
        fps.AddPrimitives(vertices);
      }
    }

    // Cleanup.
    Gl.UseProgram(0);
    if (useTexture)
      Gl.BindTexture(TextureTarget.Texture2D, 0);

    // FPS.
    if (fps.AddFrames())
      SetWindowTitle();
  }

  /// <summary>
  /// Handler for window close event.
  /// </summary>
  private static void OnClose()
  {
    Vao?.Dispose();
    ShaderPrg?.Dispose();

    // Remember to dispose the textures.
    texture?.Dispose();
  }

  /// <summary>
  /// Shift counter (0 = no shift pressed).
  /// </summary>
  private static int shiftDown = 0;

  /// <summary>
  /// Ctrl counter (0 = no ctrl pressed).
  /// </summary>
  private static int ctrlDown = 0;

  /// <summary>
  /// Handler function for keyboard key up.
  /// </summary>
  /// <param name="arg1">Keyboard object.</param>
  /// <param name="arg2">Key identification.</param>
  /// <param name="arg3">Key scancode.</param>
  private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
  {
    if (tb != null &&
        tb.KeyDown(arg1, arg2, arg3))
    {
      SetWindowTitle();
      //return;
    }

    switch (arg2)
    {
      case Key.ShiftLeft:
      case Key.ShiftRight:
        shiftDown++;
        break;

      case Key.ControlLeft:
      case Key.ControlRight:
        ctrlDown++;
        break;

      case Key.T:
        // Toggle texture.
        useTexture = !useTexture;
        if (useTexture)
          Ut.Message($"Texture: {texture?.name}");
        else
          Ut.Message("Texturing off");
        SetWindowTitle();
        break;

      case Key.I:
        // Toggle Phong shading.
        usePhong = !usePhong;
         Ut.Message("Phong shading: " + (usePhong ? "on" : "off"));
        SetWindowTitle();
        break;

      case Key.P:
        // Perspective <-> orthographic.
        if (tb != null)
        {
          tb.UsePerspective = !tb.UsePerspective;
          SetWindowTitle();
        }
        break;

      case Key.C:
        // Reset view.
        if (tb != null)
        {
          tb.Reset();
          Ut.Message("Camera reset");
        }
        break;

      case Key.V:
        // Toggle VSync.
        if (window != null)
        {
          window.VSync = !window.VSync;
          if (window.VSync)
          {
            Ut.Message("VSync on");
            fps.Reset();
          }
          else
            Ut.Message("VSync off");
        }
        break;

      case Key.R:
        // Reset the simulator.
        if (sim != null)
        {
          sim.Reset();
          Ut.Message("Simulator reset");
        }
        break;

      case Key.Up:
        // Increase particle generation rate.
        if (sim != null)
        {
          sim.ParticleRate *= 1.1;
          SetWindowTitle();
        }
        break;

      case Key.Down:
        // Decrease particle generation rate.
        if (sim != null)
        {
          sim.ParticleRate /= 1.1;
          SetWindowTitle();
        }
        break;

      case Key.F:
        sim?.FireOnce(new Vector3(0, 0, 0));
        break;

      case Key.Space:
        sim?.changeFiring();
        break;

      case Key.F1:
        // Help.
        Ut.Message("T           toggle texture", true);
        Ut.Message("I           toggle Phong shading", true);
        Ut.Message("P           toggle perspective", true);
        Ut.Message("V           toggle VSync", true);
        Ut.Message("C           camera reset", true);
        Ut.Message("R           reset the simulation", true);
        Ut.Message("Up, Down    change particle generation rate", true);
        Ut.Message("F1          print help", true);
        Ut.Message("Esc         quit the program", true);
        Ut.Message("Mouse.left  Trackball rotation", true);
        Ut.Message("Mouse.wheel zoom in/out", true);
        break;

      case Key.Escape:
        // Close the application.
        window?.Close();
        break;
    }
  }

  /// <summary>
  /// Handler function for keyboard key up.
  /// </summary>
  /// <param name="arg1">Keyboard object.</param>
  /// <param name="arg2">Key identification.</param>
  /// <param name="arg3">Key scancode.</param>
  private static void KeyUp(IKeyboard arg1, Key arg2, int arg3)
  {
    if (tb != null &&
        tb.KeyUp(arg1, arg2, arg3))
      return;

    switch (arg2)
    {
      case Key.ShiftLeft:
      case Key.ShiftRight:
        shiftDown--;
        break;

      case Key.ControlLeft:
      case Key.ControlRight:
        ctrlDown--;
        break;
    }
  }

  /// <summary>
  /// Mouse dragging - current X coordinate in pixels.
  /// </summary>
  private static float currentX = 0.0f;

  /// <summary>
  /// Mouse dragging - current Y coordinate in pixels.
  /// </summary>
  private static float currentY = 0.0f;

  /// <summary>
  /// True if dragging mode is active.
  /// </summary>
  private static bool dragging = false;

  /// <summary>
  /// Handler function for mouse button down.
  /// </summary>
  /// <param name="mouse">Mouse object.</param>
  /// <param name="btn">Button identification.</param>
  private static void MouseDown(IMouse mouse, MouseButton btn)
  {
    if (tb != null)
      tb.MouseDown(mouse, btn);

    if (btn == MouseButton.Right)
    {
      Ut.MessageInvariant($"Right button down: {mouse.Position}");

      // Start dragging.
      dragging = true;
      currentX = mouse.Position.X;
      currentY = mouse.Position.Y;
    }
  }

  /// <summary>
  /// Handler function for mouse button up.
  /// </summary>
  /// <param name="mouse">Mouse object.</param>
  /// <param name="btn">Button identification.</param>
  private static void MouseUp(IMouse mouse, MouseButton btn)
  {
    if (tb != null)
      tb.MouseUp(mouse, btn);

    if (btn == MouseButton.Right)
    {
      Ut.MessageInvariant($"Right button up: {mouse.Position}");

      // Stop dragging.
      dragging = false;
    }
  }

  /// <summary>
  /// Handler function for mouse move.
  /// </summary>
  /// <param name="mouse">Mouse object.</param>
  /// <param name="xy">New mouse position in pixels.</param>
  private static void MouseMove(IMouse mouse, System.Numerics.Vector2 xy)
  {
    if (tb != null)
      tb.MouseMove(mouse, xy);

    if (mouse.IsButtonPressed(MouseButton.Right))
    {
      Ut.MessageInvariant($"Mouse drag: {xy}");
    }

    // Object dragging.
    if (dragging)
    {
      float newX = mouse.Position.X;
      float newY = mouse.Position.Y;

      if (newX != currentX || newY != currentY)
      {
        //if (Objects.Count > 0)
        //{
        //  LastObject.Translate(new((newX - currentX) * mouseCx, (newY - currentY) * mouseCy, 0.0f));
        //}

        currentX = newX;
        currentY = newY;
      }
    }
  }

  /// <summary>
  /// Handler function for mouse button double click.
  /// </summary>
  /// <param name="mouse">Mouse object.</param>
  /// <param name="btn">Button identification.</param>
  /// <param name="xy">Double click position in pixels.</param>
  private static void MouseDoubleClick(IMouse mouse, MouseButton btn, System.Numerics.Vector2 xy)
  {
    if (btn == MouseButton.Right)
    {
      Ut.Message("Closed by double-click.", true);
      window?.Close();
    }
  }

  /// <summary>
  /// Handler function for mouse wheel rotation.
  /// </summary>
  /// <param name="mouse">Mouse object.</param>
  /// <param name="btn">Mouse wheel object (Y coordinate is used here).</param>
  private static void MouseScroll(IMouse mouse, ScrollWheel wheel)
  {
    if (tb != null)
    {
      tb.MouseWheel(mouse, wheel);
      SetWindowTitle();
    }

    // wheel.Y is -1 or 1
    Ut.MessageInvariant($"Mouse scroll: {wheel.Y}");
  }
}
