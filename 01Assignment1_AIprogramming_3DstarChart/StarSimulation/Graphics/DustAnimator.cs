using System;
using System.Numerics;
using Silk.NET.OpenGL;
using StarChart.Navigation;

namespace StarChart.Graphics;

public class WarpDustRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Shader _shader;
    private uint _vao;
    private uint _vbo;

    private readonly int _particleCount = 1500;
    private readonly Vector3[] _dustPositions;
    private readonly float[] _lineBuffer; // 6 floats per line: [X1, Y1, Z1, X2, Y2, Z2]

    private Vector3 _lastCameraPos;
    private float _currentSpeed = 0.0f;
    private float _warpAlpha = 0.0f;

    private const float BoxRadius = 35.0f;

    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;

        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            gl_Position = uProjection * uView * vec4(aPosition, 1.0);
        }
    ";

    private const string FragmentShaderSource = @"
        #version 330 core
        uniform float uAlpha;
        out vec4 FragColor;

        void main()
        {
            FragColor = vec4(0.8, 0.95, 1.0, uAlpha * 0.75);
        }
    ";

    public WarpDustRenderer(GL gl, Vector3 initialCameraPos)
    {
        _gl = gl;
        _shader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);
        _lastCameraPos = initialCameraPos;

        _dustPositions = new Vector3[_particleCount];
        _lineBuffer = new float[_particleCount * 6];

        Random rand = new Random();
        for (int i = 0; i < _particleCount; i++)
        {
            _dustPositions[i] = initialCameraPos + RandomOffset(rand, BoxRadius);
        }

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)_lineBuffer, BufferUsageARB.DynamicDraw);

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void Update(float deltaTime, Vector3 cameraPos, bool isFlyToActive)
    {
        if (deltaTime <= 0.0001f) return;

        // Calculate motion vector and speed
        Vector3 moveDelta = cameraPos - _lastCameraPos;
        float instantaneousSpeed = moveDelta.Length() / deltaTime;
        _lastCameraPos = cameraPos;

        // Smooth speed and opacity
        _currentSpeed = Lerp(_currentSpeed, instantaneousSpeed, deltaTime * 6.0f);
        float targetAlpha = isFlyToActive ? 1.0f : Math.Clamp(_currentSpeed / 12.0f, 0.0f, 1.0f);
        _warpAlpha = Lerp(_warpAlpha, targetAlpha, deltaTime * 5.0f);

        // Don't waste GPU updates if almost static
        if (_warpAlpha < 0.01f) return;

        Vector3 moveDir = moveDelta.LengthSquared() > 0.0001f
            ? Vector3.Normalize(moveDelta)
            : Vector3.Zero;

        float streakLength = Math.Clamp(_currentSpeed * 0.05f + (_warpAlpha * 2.5f), 0.1f, 10.0f);
        Random rand = Random.Shared;

        for (int i = 0; i < _particleCount; i++)
        {
            Vector3 pos = _dustPositions[i];

            // Wrap particles relative to camera movement box
            Vector3 rel = pos - cameraPos;
            if (MathF.Abs(rel.X) > BoxRadius) pos.X = cameraPos.X - MathF.Sign(rel.X) * BoxRadius;
            if (MathF.Abs(rel.Y) > BoxRadius) pos.Y = cameraPos.Y - MathF.Sign(rel.Y) * BoxRadius;
            if (MathF.Abs(rel.Z) > BoxRadius) pos.Z = cameraPos.Z - MathF.Sign(rel.Z) * BoxRadius;

            _dustPositions[i] = pos;

            // Line Start (Head)
            _lineBuffer[i * 6 + 0] = pos.X;
            _lineBuffer[i * 6 + 1] = pos.Y;
            _lineBuffer[i * 6 + 2] = pos.Z;

            // Line End (Tail stretching back along movement direction)
            Vector3 tail = pos - (moveDir * streakLength);
            _lineBuffer[i * 6 + 3] = tail.X;
            _lineBuffer[i * 6 + 4] = tail.Y;
            _lineBuffer[i * 6 + 5] = tail.Z;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (ReadOnlySpan<float>)_lineBuffer);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public void Render(Camera camera)
    {
        if (_warpAlpha < 0.01f) return;

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // Additive blending for dust glow

        _shader.Use();

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 proj = camera.GetProjectionMatrix();

        _shader.SetMatrix4("uView", ref view);
        _shader.SetMatrix4("uProjection", ref proj);
        _shader.SetFloat("uAlpha", _warpAlpha);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(_particleCount * 2));
        _gl.BindVertexArray(0);

        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0.0f, 1.0f);

    private static Vector3 RandomOffset(Random rand, float radius)
    {
        return new Vector3(
            (float)(rand.NextDouble() * 2.0 - 1.0) * radius,
            (float)(rand.NextDouble() * 2.0 - 1.0) * radius,
            (float)(rand.NextDouble() * 2.0 - 1.0) * radius
        );
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
