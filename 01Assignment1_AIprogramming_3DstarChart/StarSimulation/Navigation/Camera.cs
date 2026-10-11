using System;
using System.Numerics;

namespace StarChart.Navigation;

public class Camera
{
    public Vector3 Position { get; set; } = new(0f, 0f, 50f);
    public Vector3 TargetPosition { get; set; } = new(0f, 0f, 50f);

    public float Yaw { get; set; } = -90.0f;   // Face towards -Z by default
    public float Pitch { get; set; } = 0.0f;     // Horizon level
    public float TargetYaw { get; set; } = -90.0f;
    public float TargetPitch { get; set; } = 0.0f;

    public float AspectRatio { get; set; } = 16f / 9f;
    public float FovDegrees { get; set; } = 60.0f;

    public Vector3 Front { get; private set; } = -Vector3.UnitZ;
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public Vector3 Up { get; private set; } = Vector3.UnitY;

    public float MouseSensitivity { get; set; } = 0.15f;
    public float PanSensitivity { get; set; } = 0.05f;
    public float ZoomSensitivity { get; set; } = 3.0f;

    public Camera()
    {
        UpdateVectors();
    }

    public void FlyTo(Vector3 starPosition, float distanceOffset = 5.0f)
    {
        // Position camera behind the star looking straight at it
        TargetPosition = starPosition - (Front * distanceOffset);
    }

    public void ProcessMouseRotation(float deltaX, float deltaY)
    {
        TargetYaw += deltaX * MouseSensitivity;
        TargetPitch -= deltaY * MouseSensitivity; // Invert deltaY for standard FPS controls

        TargetPitch = Math.Clamp(TargetPitch, -89.0f, 89.0f);
    }

    public void ProcessMousePan(float deltaX, float deltaY)
    {
        Vector3 panOffset = (-Right * deltaX + Up * deltaY) * PanSensitivity;
        TargetPosition += panOffset;
    }

    public void ProcessMouseScroll(float scrollOffsetY)
    {
        TargetPosition += Front * scrollOffsetY * ZoomSensitivity;
    }

    public void Update(double deltaTime)
    {
        float t = Math.Clamp((float)deltaTime * 8.0f, 0f, 1f);

        // Smoothly interpolate position, yaw, and pitch
        Position = Vector3.Lerp(Position, TargetPosition, t);
        Yaw = MathHelperLerp(Yaw, TargetYaw, t);
        Pitch = MathHelperLerp(Pitch, TargetPitch, t);

        UpdateVectors();
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Position + Front, Up);
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        float fovRad = FovDegrees * (MathF.PI / 180.0f);
        return Matrix4x4.CreatePerspectiveFieldOfView(fovRad, AspectRatio, 0.01f, 3000.0f);
    }

    private void UpdateVectors()
    {
        float yawRad = Yaw * (MathF.PI / 180.0f);
        float pitchRad = Pitch * (MathF.PI / 180.0f);

        Vector3 front = new(
            MathF.Cos(yawRad) * MathF.Cos(pitchRad),
            MathF.Sin(pitchRad),
            MathF.Sin(yawRad) * MathF.Cos(pitchRad)
        );

        Front = Vector3.Normalize(front);
        Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Front));
    }

    private static float MathHelperLerp(float value1, float value2, float amount)
    {
        return value1 + (value2 - value1) * amount;
    }
}
