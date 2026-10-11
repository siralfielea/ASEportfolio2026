using System;
using System.IO;
using ImGuiNET;
using Silk.NET.Input;
using System.Numerics;
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

    private static Vector2 _lastMousePos;

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

        // Wire mouse input events
        if (_inputContext.Mice.Count > 0)
        {
            var mouse = _inputContext.Mice[0];
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;
            mouse.Click += OnMouseClick; // <--- Wire Click Event
        }
    }

    private static void OnMouseClick(IMouse mouse, MouseButton button, Vector2 position)
    {
        // Ignore clicks if ImGui is capturing input (e.g. clicking buttons on sidebar)
        if (ImGui.GetIO().WantCaptureMouse) return;

        if (button == MouseButton.Left)
        {
            Star? pickedStar = StarPicker.PickStar(position, _window.Size, _catalog, _camera);

            if (pickedStar != null)
            {
                _uiManager.SelectStar(pickedStar);
                Console.WriteLine($"Selected star: {pickedStar.DisplayName}");
            }
        }
    }
    private static void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        Vector2 delta = new(position.X - _lastMousePos.X, position.Y - _lastMousePos.Y);
        _lastMousePos = position;

        // Skip viewport navigation if ImGui is capturing mouse input
        if (ImGui.GetIO().WantCaptureMouse) return;

        // Right Mouse Drag: Rotate camera (Yaw / Pitch)
        if (mouse.IsButtonPressed(MouseButton.Right))
        {
            _camera.ProcessMouseRotation(delta.X, delta.Y);
        }
        // Middle Mouse Drag OR Shift + Right Drag: Pan camera (XY plane)
        else if (mouse.IsButtonPressed(MouseButton.Middle) || 
                (mouse.IsButtonPressed(MouseButton.Right) && _inputContext.Keyboards[0].IsKeyPressed(Key.ShiftLeft)))
        {
            _camera.ProcessMousePan(delta.X, delta.Y);
        }
    }

    private static void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        if (ImGui.GetIO().WantCaptureMouse) return;

        _camera.ProcessMouseScroll(scroll.Y);
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

        _renderer.Render(_camera);
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
