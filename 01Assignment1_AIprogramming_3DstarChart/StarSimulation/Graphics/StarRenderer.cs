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

    public StarRenderer(GL gl)
    {
        _gl = gl;
        _shader = new Shader(_gl, VertexShaderSource, FragmentShaderSource);
    }

    public void LoadCatalog(StarCatalog catalog)
    {
        _starCount = (uint)catalog.Stars.Count;
        if (_starCount == 0) return;

        float[] vertexData = new float[_starCount * 6];
        for (int i = 0; i < catalog.Stars.Count; i++)
        {
            var star = catalog.Stars[i];
            Vector3 color = star.GetRGBColor();

            vertexData[i * 6 + 0] = star.Position.X;
            vertexData[i * 6 + 1] = star.Position.Y;
            vertexData[i * 6 + 2] = star.Position.Z;

            vertexData[i * 6 + 3] = color.X;
            vertexData[i * 6 + 4] = color.Y;
            vertexData[i * 6 + 5] = color.Z;
        }

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
