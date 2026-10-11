using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;

namespace StarChart;

public static class CsvStarLoader
{
    public static StarCatalog LoadFromFile(string filePath)
    {
        var catalog = new StarCatalog();

        string fullPath = filePath;
        if (!File.Exists(fullPath))
        {
            string baseDir = AppContext.BaseDirectory;
            string inBase = Path.Combine(baseDir, filePath);
            string inAssets = Path.Combine(baseDir, "Assets", filePath);

            if (File.Exists(inBase)) fullPath = inBase;
            else if (File.Exists(inAssets)) fullPath = inAssets;
            else throw new FileNotFoundException($"Star catalog file not found. Checked: '{filePath}', '{inBase}', and '{inAssets}'");
        }

        using var fileStream = File.OpenRead(fullPath);
        using Stream streamToRead = fullPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(streamToRead);

        string? headerLine = reader.ReadLine();
        if (headerLine == null) return catalog;

        // Split and strip quotes from header names
        string[] headers = SplitAndCleanCsvLine(headerLine);
        
        int idxId   = Array.FindIndex(headers, h => h.Equals("id", StringComparison.OrdinalIgnoreCase));
        int idxName = Array.FindIndex(headers, h => h.Equals("proper", StringComparison.OrdinalIgnoreCase) || h.Equals("name", StringComparison.OrdinalIgnoreCase));
        
        int idxBayer = Array.FindIndex(headers, h => h.Equals("bayer", StringComparison.OrdinalIgnoreCase));
        int idxFlam  = Array.FindIndex(headers, h => h.Equals("flam", StringComparison.OrdinalIgnoreCase));
        int idxCon   = Array.FindIndex(headers, h => h.Equals("con", StringComparison.OrdinalIgnoreCase));
        int idxBf    = Array.FindIndex(headers, h => h.Equals("bf", StringComparison.OrdinalIgnoreCase));
        int idxGl    = Array.FindIndex(headers, h => h.Equals("gl", StringComparison.OrdinalIgnoreCase));
        int idxHr    = Array.FindIndex(headers, h => h.Equals("hr", StringComparison.OrdinalIgnoreCase));
        int idxHd    = Array.FindIndex(headers, h => h.Equals("hd", StringComparison.OrdinalIgnoreCase));
        int idxHip   = Array.FindIndex(headers, h => h.Equals("hip", StringComparison.OrdinalIgnoreCase));

        int idxX    = Array.FindIndex(headers, h => h.Equals("x", StringComparison.OrdinalIgnoreCase));
        int idxY    = Array.FindIndex(headers, h => h.Equals("y", StringComparison.OrdinalIgnoreCase));
        int idxZ    = Array.FindIndex(headers, h => h.Equals("z", StringComparison.OrdinalIgnoreCase));
        int idxRA   = Array.FindIndex(headers, h => h.Equals("ra", StringComparison.OrdinalIgnoreCase));
        int idxDec  = Array.FindIndex(headers, h => h.Equals("dec", StringComparison.OrdinalIgnoreCase));
        int idxDist = Array.FindIndex(headers, h => h.Equals("dist", StringComparison.OrdinalIgnoreCase));
        int idxMag  = Array.FindIndex(headers, h => h.Equals("mag", StringComparison.OrdinalIgnoreCase));
        int idxCi   = Array.FindIndex(headers, h => h.Equals("ci", StringComparison.OrdinalIgnoreCase));
        int idxTemp = Array.FindIndex(headers, h => h.Equals("temp", StringComparison.OrdinalIgnoreCase));
        int idxLum  = Array.FindIndex(headers, h => h.Equals("lum", StringComparison.OrdinalIgnoreCase));

        int autoId = 1;

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = SplitAndCleanCsvLine(line);

            int id = (idxId >= 0 && idxId < cols.Length && ParseFloat(cols[idxId], out float parsedId)) ? (int)parsedId : autoId++;

            // 1. Extract proper human name
            string? properName = (idxName >= 0 && idxName < cols.Length && !string.IsNullOrWhiteSpace(cols[idxName])) 
                ? cols[idxName] 
                : null;

            // 2. Extract designation fallback if proper name is missing
            string? designation = null;
            string bayer = (idxBayer >= 0 && idxBayer < cols.Length) ? cols[idxBayer] : "";
            string flam  = (idxFlam  >= 0 && idxFlam  < cols.Length) ? cols[idxFlam]  : "";
            string con   = (idxCon   >= 0 && idxCon   < cols.Length) ? cols[idxCon]   : "";
            string bf    = (idxBf    >= 0 && idxBf    < cols.Length) ? cols[idxBf]    : "";
            string gl    = (idxGl    >= 0 && idxGl    < cols.Length) ? cols[idxGl]    : "";
            string hr    = (idxHr    >= 0 && idxHr    < cols.Length) ? cols[idxHr]    : "";
            string hd    = (idxHd    >= 0 && idxHd    < cols.Length) ? cols[idxHd]    : "";
            string hip   = (idxHip   >= 0 && idxHip   < cols.Length) ? cols[idxHip]   : "";

            if (!string.IsNullOrEmpty(bf))
            {
                designation = string.IsNullOrEmpty(con) ? bf : $"{bf} {con}";
            }
            else if (!string.IsNullOrEmpty(bayer) || !string.IsNullOrEmpty(flam))
            {
                designation = $"{flam} {bayer} {con}".Trim();
            }
            else if (!string.IsNullOrEmpty(gl))
            {
                designation = $"Gliese {gl}";
            }
            else if (!string.IsNullOrEmpty(hr))
            {
                designation = $"HR {hr}";
            }
            else if (!string.IsNullOrEmpty(hd))
            {
                designation = $"HD {hd}";
            }
            else if (!string.IsNullOrEmpty(hip))
            {
                designation = $"HIP {hip}";
            }

            // 3. Resolve 3D Cartesian coordinates
            Vector3 position = Vector3.Zero;
            bool hasValidPos = false;

            if (idxX >= 0 && idxY >= 0 && idxZ >= 0 &&
                idxX < cols.Length && idxY < cols.Length && idxZ < cols.Length &&
                ParseFloat(cols[idxX], out float x) &&
                ParseFloat(cols[idxY], out float y) &&
                ParseFloat(cols[idxZ], out float z))
            {
                position = new Vector3(x, y, z);
                hasValidPos = true;
            }
            else if (idxRA >= 0 && idxDec >= 0 && idxDist >= 0 &&
                     idxRA < cols.Length && idxDec < cols.Length && idxDist < cols.Length &&
                     ParseFloat(cols[idxRA], out float raHours) &&
                     ParseFloat(cols[idxDec], out float decDeg) &&
                     ParseFloat(cols[idxDist], out float dist))
            {
                position = ConvertSphericalToCartesian(raHours, decDeg, dist);
                hasValidPos = true;
            }

            if (!hasValidPos) continue;

            // Allow Sol at origin (0,0,0); skip any unmeasured star dumped at origin
            bool isSol = id == 0 || (properName != null && properName.Equals("Sol", StringComparison.OrdinalIgnoreCase));
            if (!isSol && position.LengthSquared() < 0.000001f)
            {
                continue;
            }

            float mag = (idxMag >= 0 && idxMag < cols.Length && ParseFloat(cols[idxMag], out float m)) ? m : 5.0f;

            // Calculate Kelvin temperature from 'temp' or Color Index 'ci'
            float temp = 5778.0f;
            if (idxTemp >= 0 && idxTemp < cols.Length && ParseFloat(cols[idxTemp], out float tKelvin))
            {
                temp = tKelvin;
            }
            else if (idxCi >= 0 && idxCi < cols.Length && ParseFloat(cols[idxCi], out float ci))
            {
                temp = 4600.0f * (1.0f / (0.92f * ci + 1.7f) + 1.0f / (0.92f * ci + 0.62f));
            }

            // Calculate Luminosity
            float luminosity = 1.0f;
            if (idxLum >= 0 && idxLum < cols.Length && ParseFloat(cols[idxLum], out float l))
            {
                luminosity = l;
            }
            else
            {
                luminosity = MathF.Pow(10.0f, (4.83f - mag) / 2.5f);
            }

            var star = new Star
            {
                Id = id,
                ProperName = properName,
                Designation = designation,
                Position = position,
                Magnitude = mag,
                Temperature = Math.Clamp(temp, 1000.0f, 40000.0f),
                Luminosity = Math.Clamp(luminosity, 0.0001f, 1000000.0f)
            };

            catalog.AddStar(star);
        }

        return catalog;
    }

    private static string[] SplitAndCleanCsvLine(string line)
    {
        string[] raw = line.Split(',');
        for (int i = 0; i < raw.Length; i++)
        {
            raw[i] = raw[i].Trim().Trim('"', '\'', '\r', '\n', '\uFEFF').Trim();
        }
        return raw;
    }

    private static Vector3 ConvertSphericalToCartesian(float raHours, float decDegrees, float distance)
    {
        float raRad = raHours * (15.0f * MathF.PI / 180.0f);
        float decRad = decDegrees * (MathF.PI / 180.0f);

        float x = distance * MathF.Cos(decRad) * MathF.Cos(raRad);
        float y = distance * MathF.Sin(decRad);
        float z = distance * MathF.Cos(decRad) * MathF.Sin(raRad);

        return new Vector3(x, y, z);
    }

    private static bool ParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
