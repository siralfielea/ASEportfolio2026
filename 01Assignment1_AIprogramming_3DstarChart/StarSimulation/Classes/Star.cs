using System.Numerics;

namespace StarChart;
public class Star {
public int Id { get; set; }
    public string? Name { get; set; } // Nullable: real stars have names, catalog stars might not
    
    // Spatial Position in 3D Space (e.g. in Light-Years or Parsecs from Sol at 0,0,0)
    public Vector3 Position { get; set; }
    
    // Physical & Astronomical Properties
    public float Temperature { get; set; } // In Kelvin (e.g. 5778 K for Sun)
    public float Luminosity { get; set; }  // Relative to Sun (Sol = 1.0)
    public float Magnitude { get; set; }   // Apparent or Absolute magnitude
    public float AbsoluteRadius { get; set; } // Relative to Sun radius

    // Utility properties
    public bool IsNamed => !string.IsNullOrWhiteSpace(Name);
    public string DisplayName => IsNamed ? Name! : $"UNNAMED-{Id:D6}";

    /// <summary>
    /// Calculates star color (RGB) based on surface temperature (Kelvin blackbody approximation).
    /// Hot stars (>10,000K) are blue; Cool stars (<3,500K) are red; Mid-range (~5,800K) are yellow/white.
    /// </summary>
    public Vector3 GetRGBColor()
    {
        float temp = Temperature / 100.0f;
        float r, g, b;

        // Red channel
        if (temp <= 66)
            r = 255;
        else
            r = 329.698727446f * MathF.Pow(temp - 60, -0.1332047592f);

        // Green channel
        if (temp <= 66)
            g = 99.4708025861f * MathF.Log(temp) - 161.1195681661f;
        else
            g = 288.1221695283f * MathF.Pow(temp - 60, -0.0755148492f);

        // Blue channel
        if (temp >= 66)
            b = 255;
        else if (temp <= 19)
            b = 0;
        else
            b = 138.5177312231f * MathF.Log(temp - 10) - 305.0447927307f;

        return new Vector3(
            Math.Clamp(r / 255.0f, 0.0f, 1.0f),
            Math.Clamp(g / 255.0f, 0.0f, 1.0f),
            Math.Clamp(b / 255.0f, 0.0f, 1.0f)
        );
    }
}
