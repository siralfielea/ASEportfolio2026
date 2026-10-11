using System;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace StarChart;

public static class CsvStarLoader
{
    /// <summary>
    /// Loads star records from a CSV file into a StarCatalog instance.
    /// Expects CSV columns: id, name, ra, dec, dist, mag, ci (or x, y, z)
    /// </summary>
    public static StarCatalog LoadFromFile(string filePath)
    {
        var catalog = new StarCatalog();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Star catalog file not found at: {filePath}");
        }

        using var reader = new StreamReader(filePath);
        string? headerLine = reader.ReadLine();
        if (headerLine == null) return catalog;

        // Map column indices dynamically from CSV header
        var headers = headerLine.Split(',');
        int idxId = Array.FindIndex(headers, h => h.Trim().Equals("id", StringComparison.OrdinalIgnoreCase));
        int idxName = Array.FindIndex(headers, h => h.Trim().Equals("proper", StringComparison.OrdinalIgnoreCase) || h.Trim().Equals("name", StringComparison.OrdinalIgnoreCase));
        int idxX = Array.FindIndex(headers, h => h.Trim().Equals("x", StringComparison.OrdinalIgnoreCase));
        int idxY = Array.FindIndex(headers, h => h.Trim().Equals("y", StringComparison.OrdinalIgnoreCase));
        int idxZ = Array.FindIndex(headers, h => h.Trim().Equals("z", StringComparison.OrdinalIgnoreCase));
        int idxRA = Array.FindIndex(headers, h => h.Trim().Equals("ra", StringComparison.OrdinalIgnoreCase));
        int idxDec = Array.FindIndex(headers, h => h.Trim().Equals("dec", StringComparison.OrdinalIgnoreCase));
        int idxDist = Array.FindIndex(headers, h => h.Trim().Equals("dist", StringComparison.OrdinalIgnoreCase));
        int idxMag = Array.FindIndex(headers, h => h.Trim().Equals("mag", StringComparison.OrdinalIgnoreCase));
        int idxCi = Array.FindIndex(headers, h => h.Trim().Equals("ci", StringComparison.OrdinalIgnoreCase)); // B-V Color Index
        int idxTemp = Array.FindIndex(headers, h => h.Trim().Equals("temp", StringComparison.OrdinalIgnoreCase));

        int autoId = 1;

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');

            // Parse ID and Name
            int id = (idxId >= 0 && ParseFloat(cols[idxId], out float parsedId)) ? (int)parsedId : autoId++;
            string? name = (idxName >= 0 && idxName < cols.Length) ? cols[idxName].Trim() : null;
            if (string.IsNullOrWhiteSpace(name)) name = null;

            // Resolve 3D Position (Parsecs / Light Years)
            Vector3 position = Vector3.Zero;
            if (idxX >= 0 && idxY >= 0 && idxZ >= 0 &&
                idxX < cols.Length && idxY < cols.Length && idxZ < cols.Length &&
                ParseFloat(cols[idxX], out float x) &&
                ParseFloat(cols[idxY], out float y) &&
                ParseFloat(cols[idxZ], out float z))
            {
                position = new Vector3(x, y, z);
            }
            else if (idxRA >= 0 && idxDec >= 0 && idxDist >= 0 &&
                     idxRA < cols.Length && idxDec < cols.Length && idxDist < cols.Length &&
                     ParseFloat(cols[idxRA], out float raHours) &&
                     ParseFloat(cols[idxDec], out float decDeg) &&
                     ParseFloat(cols[idxDist], out float dist))
            {
                position = ConvertSphericalToCartesian(raHours, decDeg, dist);
            }

            // Parse Magnitude
            float mag = (idxMag >= 0 && idxMag < cols.Length && ParseFloat(cols[idxMag], out float m)) ? m : 5.0f;

            // Resolve Temperature in Kelvin
            float temp = 5778.0f; // Default Sol temperature
            if (idxTemp >= 0 && idxTemp < cols.Length && ParseFloat(cols[idxTemp], out float tKelvin))
            {
                temp = tKelvin;
            }
            else if (idxCi >= 0 && idxCi < cols.Length && ParseFloat(cols[idxCi], out float ci))
            {
                // Ballesteros formula: Temperature from B-V color index
                temp = 4600.0f * (1.0f / (0.92f * ci + 1.7f) + 1.0f / (0.92f * ci + 0.62f));
            }

            // Calculate approximate luminosity from magnitude (Sol = 1.0)
            float luminosity = MathF.Pow(10.0f, (4.83f - mag) / 2.5f);

            var star = new Star
            {
                Id = id,
                Name = name,
                Position = position,
                Magnitude = mag,
                Temperature = Math.Clamp(temp, 1000.0f, 40000.0f),
                Luminosity = Math.Clamp(luminosity, 0.0001f, 1000000.0f)
            };

            catalog.AddStar(star);
        }

        return catalog;
    }

    /// <summary>
    /// Converts RA (Hours 0-24), Dec (Degrees -90 to +90), and Distance into 3D Cartesian coordinates.
    /// </summary>
    private static Vector3 ConvertSphericalToCartesian(float raHours, float decDegrees, float distance)
    {
        float raRad = raHours * (15.0f * MathF.PI / 180.0f); // 1 hour = 15 degrees
        float decRad = decDegrees * (MathF.PI / 180.0f);

        float x = distance * MathF.Cos(decRad) * MathF.Cos(raRad);
        float y = distance * MathF.Sin(decRad);
        float z = distance * MathF.Cos(decRad) * MathF.Sin(raRad);

        return new Vector3(x, y, z);
    }

    private static bool ParseFloat(string value, out float result)
    {
        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
