using System;
using System.IO;
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace StarChart;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;

    private static uint _vao;
    private static uint _vbo;
    private static uint _shaderProgram;

    private static StarCatalog _catalog = new();

    // Shader with View and Projection matrices
    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;

        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vColor;

        void main()
        {
            vColor = aColor;
            gl_Position = uProjection * uView * vec4(aPosition, 1.0);
            gl_PointSize = 10.0;
        }
    ";

    private const string FragmentShaderSource = @"
        #version 330 core
        in vec3 vColor;
        out vec4 FragColor;

        void main()
        {
            vec2 circCoord = 2.0 * gl_PointCoord - vec2(1.0);
            float dist = length(circCoord);
            
            if (dist > 1.0) {
                discard;
            }

            float alpha = 1.0 - smoothstep(0.2, 1.0, dist);
            FragColor = vec4(vColor, alpha);
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

    private static void OnLoad()
    {
        _gl = _window.CreateOpenGL();

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

        // 2. Load Star Dataset from Assets
        string csvPath = Path.Combine(AppContext.BaseDirectory, "Assets", "testStars.csv");
        _catalog = CsvStarLoader.LoadFromFile(csvPath);
        Console.WriteLine($"Loaded {_catalog.Stars.Count} stars into catalog.");

        // 3. Interleave vertex attributes: [x, y, z, r, g, b] per star
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

        // 4. Create and bind buffers
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)vertexData, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private static void OnRender(double deltaTime)
    {
        _gl.ClearColor(0.01f, 0.01f, 0.03f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_catalog.Stars.Count == 0) return;

        _gl.UseProgram(_shaderProgram);

        // Position camera back at Z = 50 facing origin (0,0,0)
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 50f), Vector3.Zero, Vector3.UnitY);

        // Perspective Projection matrix (60 deg FOV, near 0.1f, far 1000f)
        float fov = 60.0f * (MathF.PI / 180.0f);
        float aspectRatio = (float)_window.Size.X / _window.Size.Y;
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspectRatio, 0.1f, 1000.0f);

        // Pass matrices to vertex shader uniforms
        int viewLoc = _gl.GetUniformLocation(_shaderProgram, "uView");
        int projLoc = _gl.GetUniformLocation(_shaderProgram, "uProjection");

        _gl.UniformMatrix4(viewLoc, 1, false, ref view.M11);
        _gl.UniformMatrix4(projLoc, 1, false, ref projection.M11);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_catalog.Stars.Count);
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
