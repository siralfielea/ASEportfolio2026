using System;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace StarChart;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;

    private static uint _vao;
    private static uint _vbo;
    private static uint _shaderProgram;

    // Shader to project point positions and set point size on screen
    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;

        void main()
        {
            gl_Position = vec4(aPosition, 1.0);
            gl_PointSize = 12.0; // Point size in pixels
        }
    ";

    // Shader to shape square GL points into round glowing star discs
    private const string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;

        void main()
        {
            // Transform square point coordinate (0..1) to centered range (-1..1)
            vec2 circCoord = 2.0 * gl_PointCoord - vec2(1.0);
            float dist = length(circCoord);
            
            if (dist > 1.0) {
                discard; // Clip outside circle
            }

            // Soft glow falloff from center
            float alpha = 1.0 - smoothstep(0.2, 1.0, dist);
            FragColor = vec4(0.85, 0.92, 1.0, alpha); // Soft blue-white tint
        }
    ";

    static void Main(string[] args)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "Star Chart",
            VSync = true
        };

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;

        _window.Run();
    }

    private static StarCatalog _catalog = new();
    private static void OnLoad()
    {
        _gl = _window.CreateOpenGL();

        // Enable GL settings for point sizing and alpha blending
        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // 1. Compile and link shaders
        uint vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.DetachShader(_shaderProgram, vertexShader);
        _gl.DetachShader(_shaderProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Load star dataset
            string csvPath = Path.Combine(AppContext.BaseDirectory, "Assets", "testStars.csv");
        _catalog = CsvStarLoader.LoadFromFile(csvPath);
        Console.WriteLine($"Loaded {_catalog.Stars.Count} stars into catalog.");

        // Pack 3D position (x, y, z) and RGB color (r, g, b) per vertex into a float array
        float[] vertexData = new float[_catalog.Stars.Count * 6];
        for (int i = 0; i < _catalog.Stars.Count; i++)
        {
            var star = _catalog.Stars[i];
            Vector3 color = star.GetRGBColor();

            vertexData[i * 6 + 0] = star.Position.X;
            vertexData[i * 6 + 1] = star.Position.Y;
            vertexData[i * 6 + 2] = star.Position.Z;
            vertexData[i * 6 + 3] = color.X;
            vertexData[i * 6 + 4] = color.Y;
            vertexData[i * 6 + 5] = color.Z;
        }

        // 3. Generate and bind buffers
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)vertexData, BufferUsageARB.StaticDraw);


        // Configure position attribute at location 0
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private static void OnRender(double deltaTime)
    {
        // Deep space background clear
        _gl.ClearColor(0.01f, 0.01f, 0.03f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _gl.UseProgram(_shaderProgram);
        _gl.BindVertexArray(_vao);

        // Draw 5 star points
        _gl.DrawArrays(PrimitiveType.Points, 0, 5);
    }

    private static uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new Exception($"Error compiling {type}: {infoLog}");
        }

        return shader;
    }
    private static void OnUpdate(double deltaTime) { }

    private static void OnClosing()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_shaderProgram);
    }
}
