using System;
using System.Numerics;
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

    // Shader with perspective distance attenuation and magnitude scaling
    private const string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;
        layout (location = 2) in float aMagnitude;

        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vColor;

        void main()
        {
            vColor = aColor;

            // Transform into View Space to measure distance to camera
            vec4 viewPos = uView * vec4(aPosition, 1.0);
            gl_Position = uProjection * viewPos;

            float dist = length(viewPos.xyz);

            // Brighter stars (lower/negative magnitude) render larger
            float magScale = max(0.5, 3.5 - (aMagnitude * 0.25));

            // Perspective point size formula: grows as distance decreases
            float dynamicSize = (25.0 * magScale) / max(dist, 0.05);

            // Clamp point size between 3px (far away) and 160px (up close)
            gl_PointSize = clamp(dynamicSize, 3.0, 160.0);
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

            // Radial soft glow falloff
            float alpha = 1.0 - smoothstep(0.15, 1.0, dist);
            FragColor = vec4(vColor, alpha);
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

        // Pack 7 floats per star: [X, Y, Z, R, G, B, Magnitude]
        float[] vertexData = new float[_starCount * 7];
        for (int i = 0; i < catalog.Stars.Count; i++)
        {
            var star = catalog.Stars[i];
            Vector3 color = star.GetRGBColor();

            vertexData[i * 7 + 0] = star.Position.X;
            vertexData[i * 7 + 1] = star.Position.Y;
            vertexData[i * 7 + 2] = star.Position.Z;

            vertexData[i * 7 + 3] = color.X;
            vertexData[i * 7 + 4] = color.Y;
            vertexData[i * 7 + 5] = color.Z;

            vertexData[i * 7 + 6] = star.Magnitude;
        }

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)vertexData, BufferUsageARB.StaticDraw);

        uint stride = 7 * sizeof(float); // 7 floats stride

        // Location 0: Position (3 floats)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(0);

        // Location 1: Color (3 floats)
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        _gl.EnableVertexAttribArray(1);

        // Location 2: Magnitude (1 float)
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        _gl.EnableVertexAttribArray(2);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void Render(Camera camera)
    {
        if (_starCount == 0) return;

        _shader.Use();

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 proj = camera.GetProjectionMatrix();

        _shader.SetMatrix4("uView", ref view);
        _shader.SetMatrix4("uProjection", ref proj);

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
