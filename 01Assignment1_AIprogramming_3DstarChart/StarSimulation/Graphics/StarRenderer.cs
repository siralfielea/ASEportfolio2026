using System;
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using StarChart.Navigation;

namespace StarChart.Graphics;

public class StarRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Shader _shader;
    private uint _vao;
    private uint _vbo;
    private uint _starCount;

    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;
        layout (location = 2) in float aMagnitude;
        layout (location = 3) in float aSolarDiameter;

        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform float uViewportHeight;
        uniform float uFovRadians;

        out vec3 vColor;

        void main()
        {
            vColor = aColor;

            vec4 viewPos = uView * vec4(aPosition, 1.0);
            gl_Position = uProjection * viewPos;

            float dist = length(viewPos.xyz);

            // Scale factor blending apparent magnitude and physical solar diameter
            float magScale = max(0.5, 3.5 - (aMagnitude * 0.3));
            float physicalScale = log2(max(aSolarDiameter, 0.1) + 1.0);
            float worldSize = 0.05 * magScale * (0.8 + 0.4 * physicalScale);

            // True perspective projection point sizing
            float projSize = (worldSize * uViewportHeight) / (2.0 * max(dist, 0.01) * tan(uFovRadians * 0.5));

            // Clamp point size so distant stars stay crisp (min 3.5px) and close stars expand into 3D spheres
            gl_PointSize = clamp(projSize, 3.5, 512.0);
        }
    ";

    private const string FragmentShaderSource = @"
        #version 330 core
        in vec3 vColor;
        out vec4 FragColor;

        void main()
        {
            // Convert PointCoord from [0, 1] to [-1, 1]
            vec2 pos = gl_PointCoord * 2.0 - vec2(1.0);
            float distSq = dot(pos, pos);

            // Discard fragments outside the sphere disc boundary
            if (distSq > 1.0) {
                discard;
            }

            // Reconstruct 3D sphere surface normal Z coordinate
            float z = sqrt(1.0 - distSq);

            // Solar limb darkening: brighter at center, dims smoothly toward edge
            float limbDarkening = 0.4 + 0.6 * z;
            vec3 sphereColor = vColor * limbDarkening;

            // Smooth edge anti-aliasing
            float edgeAlpha = smoothstep(1.0, 0.85, sqrt(distSq));

            FragColor = vec4(sphereColor, edgeAlpha);
        }
    ";

    public StarRenderer(GL gl)
    {
        _gl = gl;
        _shader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);
    }

    public void LoadCatalog(StarCatalog catalog)
    {
        _starCount = (uint)catalog.Stars.Count;
        if (_starCount == 0) return;

        // 8 floats per star: [X, Y, Z, R, G, B, Magnitude, SolarDiameter]
        float[] vertexData = new float[_starCount * 8];
        for (int i = 0; i < catalog.Stars.Count; i++)
        {
            var star = catalog.Stars[i];
            Vector3 color = star.GetRGBColor();

            vertexData[i * 8 + 0] = star.Position.X;
            vertexData[i * 8 + 1] = star.Position.Y;
            vertexData[i * 8 + 2] = star.Position.Z;

            vertexData[i * 8 + 3] = color.X;
            vertexData[i * 8 + 4] = color.Y;
            vertexData[i * 8 + 5] = color.Z;

            vertexData[i * 8 + 6] = star.Magnitude;
            vertexData[i * 8 + 7] = star.SolarDiameter;
        }

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)vertexData, BufferUsageARB.StaticDraw);

        uint stride = 8 * sizeof(float);

        // Location 0: Position
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(0);

        // Location 1: Color
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        _gl.EnableVertexAttribArray(1);

        // Location 2: Magnitude
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        _gl.EnableVertexAttribArray(2);

        // Location 3: Solar Diameter
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, 7 * sizeof(float));
        _gl.EnableVertexAttribArray(3);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void Render(Camera camera, Vector2D<int> windowSize)
    {
        if (_starCount == 0) return;

        _shader.Use();

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 proj = camera.GetProjectionMatrix();

        _shader.SetMatrix4("uView", ref view);
        _shader.SetMatrix4("uProjection", ref proj);

        // Uniforms for true perspective point size calculation
        _shader.SetFloat("uViewportHeight", (float)windowSize.Y);
        _shader.SetFloat("uFovRadians", camera.FovDegrees * (MathF.PI / 180.0f));

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Points, 0, _starCount);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
