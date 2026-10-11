using System;
using System.Numerics;

namespace StarChart.Navigation;

public class Camera
{
    public Vector3 Position { get; set; } = new(0f, 0f, 50f);
    public Vector3 TargetPosition { get; set; } = new(0f, 0f, 50f);
    public float AspectRatio { get; set; } = 16f / 9f;
    public float FovDegrees { get; set; } = 60.0f;

    public void FlyTo(Vector3 starPosition, float distanceOffset = 5.0f)
    {
        TargetPosition = starPosition + new Vector3(0f, 0f, distanceOffset);
    }

    public void Update(double deltaTime)
    {
        // Smooth linear interpolation (lerp) towards target position
        Position = Vector3.Lerp(Position, TargetPosition, (float)deltaTime * 4.0f);
    }

    public Matrix4x4 GetViewMatrix()
    {
        // Camera positioned at Position looking at origin or offset forward
        return Matrix4x4.CreateLookAt(Position, Position - Vector3.UnitZ, Vector3.UnitY);
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        float fovRad = FovDegrees * (MathF.PI / 180.0f);
        return Matrix4x4.CreatePerspectiveFieldOfView(fovRad, AspectRatio, 0.1f, 2000.0f);
    }
}
