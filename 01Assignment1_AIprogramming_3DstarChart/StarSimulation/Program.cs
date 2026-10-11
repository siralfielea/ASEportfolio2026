using System;
using System.IO;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using StarChart.Graphics;
using StarChart.Navigation;
using StarChart.UI;

namespace StarChart;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;
    private static IInputContext _inputContext = null!;

    private static StarCatalog _catalog = new();
    private static StarRenderer _renderer = null!;
    private static Camera _camera = new();
    private static UIManager _uiManager = null!;

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
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;

        _window.Run();
    }

    private static void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _inputContext = _window.CreateInput();

        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _camera.AspectRatio = (float)_window.Size.X / _window.Size.Y;

        string csvPath = Path.Combine(AppContext.BaseDirectory, "Assets", "testStars.csv");
        _catalog = CsvStarLoader.LoadFromFile(csvPath);

        _renderer = new StarRenderer(_gl);
        _renderer.LoadCatalog(_catalog);

        _uiManager = new UIManager(_gl, _window, _inputContext);
        _uiManager.OnFlyToRequested += star =>
        {
            _camera.FlyTo(star.Position);
        };
    }

    private static void OnUpdate(double deltaTime)
    {
        _camera.Update(deltaTime);
        _uiManager.Update((float)deltaTime);
    }

    private static void OnRender(double deltaTime)
    {
        _gl.ClearColor(0.01f, 0.01f, 0.03f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // 1. Render 3D Star Scene
        _renderer.Render(_camera);

        // 2. Render ImGui Overlay
        _uiManager.Render(_catalog, _camera, _window.Size);
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        _camera.AspectRatio = (float)size.X / size.Y;
    }

    private static void OnClosing()
    {
        _uiManager?.Dispose();
        _renderer?.Dispose();
        _inputContext?.Dispose();
    }
}
