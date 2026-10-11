using System;
using System.Numerics;
using Silk.NET.Maths;

namespace StarChart.Navigation;

public static class StarPicker
{
    /// <summary>
    /// Projects 3D star coordinates to 2D screen pixels and returns the closest star under the cursor.
    /// </summary>
    /// <param name="mousePos">Screen mouse pixel position (X, Y)</param>
    /// <param name="windowSize">Window dimensions</param>
    /// <param name="catalog">Star catalog</param>
    /// <param name="camera">Active 3D camera</param>
    /// <param name="pickRadiusPixels">Selection tolerance radius in pixels (default 16px)</param>
    public static Star? PickStar(
        Vector2 mousePos,
        Vector2D<int> windowSize,
        StarCatalog catalog,
        Camera camera,
        float pickRadiusPixels = 16.0f)
    {
        if (catalog.Stars.Count == 0) return null;

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 proj = camera.GetProjectionMatrix();
        Matrix4x4 viewProj = view * proj;

        Star? bestStar = null;
        float minDepth = float.MaxValue;
        float maxAllowedDistSq = pickRadiusPixels * pickRadiusPixels;

        foreach (var star in catalog.Stars)
        {
            // Transform 3D star position to Clip Space
            Vector4 clipPos = Vector4.Transform(new Vector4(star.Position, 1.0f), viewProj);

            // Ignore stars behind the camera near plane
            if (clipPos.W <= 0.1f) continue;

            // Convert to Normalized Device Coordinates (NDC: -1 to +1)
            Vector3 ndc = new Vector3(clipPos.X, clipPos.Y, clipPos.Z) / clipPos.W;

            // Ignore stars outside the view frustum depth
            if (ndc.Z < -1.0f || ndc.Z > 1.0f) continue;

            // Map NDC (-1..1) to screen coordinates (0..Width, 0..Height)
            float screenX = (ndc.X + 1.0f) * 0.5f * windowSize.X;
            float screenY = (1.0f - ndc.Y) * 0.5f * windowSize.Y; // Invert Y for screen space

            Vector2 starScreenPos = new Vector2(screenX, screenY);
            float distSq = Vector2.DistanceSquared(mousePos, starScreenPos);

            if (distSq <= maxAllowedDistSq)
            {
                // If multiple stars fall within click radius, pick the one closer to camera in depth
                if (clipPos.W < minDepth)
                {
                    minDepth = clipPos.W;
                    bestStar = star;
                }
            }
        }

        return bestStar;
    }
}
