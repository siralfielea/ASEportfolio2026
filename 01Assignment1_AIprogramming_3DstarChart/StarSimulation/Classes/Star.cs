using System;
using System.Numerics;

namespace StarChart;

public class Star
{
    public int Id { get; set; }
    public string? ProperName { get; set; }
    public string? Designation { get; set; }
    public Vector3 Position { get; set; }
    public float Magnitude { get; set; }
    public float Temperature { get; set; }
    public float Luminosity { get; set; }

    // Returns true ONLY if the star has an actual proper human name (e.g. Sirius, Betelgeuse, Sol)
    public bool HasProperName => !string.IsNullOrWhiteSpace(ProperName);
    
    // Returns true if the star has a catalog designation (e.g. Bayer/Flamsteed, HD, HIP)
    public bool HasDesignation => !string.IsNullOrWhiteSpace(Designation);

    // Displays Proper Name if available, falls back to Designation (HD/HIP), then ID
    public string DisplayName => HasProperName 
        ? ProperName! 
        : (HasDesignation ? Designation! : $"Star #{Id}");

    public Vector3 GetRGBColor()
    {
        float temp = Temperature / 100.0f;
        float red, green, blue;

        if (temp <= 66.0f)
        {
            red = 255.0f;
            green = Math.Clamp(99.4708025861f * MathF.Log(temp) - 161.1195681661f, 0f, 255f);
        }
        else
        {
            red = Math.Clamp(329.698727446f * MathF.Pow(temp - 60.0f, -0.1332047592f), 0f, 255f);
            green = Math.Clamp(288.1221695283f * MathF.Pow(temp - 60.0f, -0.0755148492f), 0f, 255f);
        }

        if (temp >= 66.0f)
        {
            blue = 255.0f;
        }
        else if (temp <= 19.0f)
        {
            blue = 0.0f;
        }
        else
        {
            blue = Math.Clamp(138.5177312231f * MathF.Log(temp - 10.0f) - 305.0447927307f, 0f, 255f);
        }

        return new Vector3(red / 255.0f, green / 255.0f, blue / 255.0f);
    }
}
