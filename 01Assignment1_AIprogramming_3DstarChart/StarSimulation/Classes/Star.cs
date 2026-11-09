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

    // Solar Diameter relative to the Sun (Sun = 1.0, Betelgeuse ≈ 1000.0)
    public float SolarDiameter => GetSolarDiameter();

    public bool HasProperName => !string.IsNullOrWhiteSpace(ProperName);
    public bool HasDesignation => !string.IsNullOrWhiteSpace(Designation);

    public string DisplayName => HasProperName
        ? ProperName!
        : (HasDesignation ? Designation! : $"Star #{Id}");

    /// <summary>
    /// Calculates stellar diameter relative to the Sun (Sol = 1.0) using Stefan-Boltzmann ratio.
    /// </summary>
    public float GetSolarDiameter()
    {
        if (Temperature <= 0.0f) return 1.0f;

        const float solarTemp = 5778.0f; // Sun surface temp in Kelvin

        // R / R_sun = sqrt(L / L_sun) * (T_sun / T)^2
        float radiusRatio = MathF.Sqrt(Luminosity) * MathF.Pow(solarTemp / Temperature, 2.0f);

        return radiusRatio;
    }

    /// <summary>
    /// Returns absolute physical diameter in kilometers.
    /// </summary>
    public double GetDiameterKm()
    {
        const double solarDiameterKm = 1_392_700.0; // Sun diameter in km
        return GetSolarDiameter() * solarDiameterKm;
    }

    /// <summary>
    /// Formats the star's physical diameter in kilometers with human-readable suffixes (Million, Billion, etc.).
    /// </summary>
    public string GetFormattedDiameterKm()
    {
        double km = GetDiameterKm();

        if (km >= 1_000_000_000_000.0)
            return $"{km / 1_000_000_000_000.0:F2} Trillion km";
        if (km >= 1_000_000_000.0)
            return $"{km / 1_000_000_000.0:F2} Billion km";
        if (km >= 1_000_000.0)
            return $"{km / 1_000_000.0:F2} Million km";
        if (km >= 1_000.0)
            return $"{km:N0} km";

        return $"{km:F0} km";
    }

    /// <summary>
    /// Formats the star's diameter relative to the Sun (e.g. "1.00 R⊙" or "1,200 R⊙").
    /// </summary>
    public string GetFormattedSolarDiameter()
    {
        float d = SolarDiameter;

        if (d >= 1000.0f)
            return $"{d:N0} R⊙";
        if (d >= 10.0f)
            return $"{d:F1} R⊙";
        if (d >= 0.01f)
            return $"{d:F2} R⊙";

        return $"{d:F4} R⊙";
    }

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
