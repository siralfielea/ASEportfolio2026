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

        // Open file stream
        using var fileStream = File.OpenRead(fullPath);

        // Decompress GZip automatically if file ends in .gz
        using Stream streamToRead = fullPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(streamToRead);

        string? headerLine = reader.ReadLine();
        if (headerLine == null) return catalog;

        var headers = headerLine.Split(',');
        int idxId = Array.FindIndex(headers, h => h.Trim().Equals("id", StringComparison.OrdinalIgnoreCase));
        int idxName = Array.FindIndex(headers, h => h.Trim().Equals("proper", StringComparison.OrdinalIgnoreCase) || h.Trim().Equals("name", StringComparison.OrdinalIgnoreCase));
        
        int idxBayer = Array.FindIndex(headers, h => h.Trim().Equals("bayer", StringComparison.OrdinalIgnoreCase));
        int idxFlam  = Array.FindIndex(headers, h => h.Trim().Equals("flam", StringComparison.OrdinalIgnoreCase));
        int idxCon   = Array.FindIndex(headers, h => h.Trim().Equals("con", StringComparison.OrdinalIgnoreCase));
        int idxHd    = Array.FindIndex(headers, h => h.Trim().Equals("hd", StringComparison.OrdinalIgnoreCase));
        int idxHip   = Array.FindIndex(headers, h => h.Trim().Equals("hip", StringComparison.OrdinalIgnoreCase));

        int idxX = Array.FindIndex(headers, h => h.Trim().Equals("x", StringComparison.OrdinalIgnoreCase));
        int idxY = Array.FindIndex(headers, h => h.Trim().Equals("y", StringComparison.OrdinalIgnoreCase));
        int idxZ = Array.FindIndex(headers, h => h.Trim().Equals("z", StringComparison.OrdinalIgnoreCase));
        int idxRA = Array.FindIndex(headers, h => h.Trim().Equals("ra", StringComparison.OrdinalIgnoreCase));
        int idxDec = Array.FindIndex(headers, h => h.Trim().Equals("dec", StringComparison.OrdinalIgnoreCase));
        int idxDist = Array.FindIndex(headers, h => h.Trim().Equals("dist", StringComparison.OrdinalIgnoreCase));
        int idxMag = Array.FindIndex(headers, h => h.Trim().Equals("mag", StringComparison.OrdinalIgnoreCase));
        int idxCi = Array.FindIndex(headers, h => h.Trim().Equals("ci", StringComparison.OrdinalIgnoreCase));
        int idxTemp = Array.FindIndex(headers, h => h.Trim().Equals("temp", StringComparison.OrdinalIgnoreCase));

        int autoId = 1;

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');

            int id = (idxId >= 0 && idxId < cols.Length && ParseFloat(cols[idxId], out float parsedId)) ? (int)parsedId : autoId++;
            string? name = (idxName >= 0 && idxName < cols.Length) ? cols[idxName].Trim() : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                string bayer = (idxBayer >= 0 && idxBayer < cols.Length) ? cols[idxBayer].Trim() : "";
                string flam  = (idxFlam  >= 0 && idxFlam  < cols.Length) ? cols[idxFlam].Trim()  : "";
                string con   = (idxCon   >= 0 && idxCon   < cols.Length) ? cols[idxCon].Trim()   : "";

                if (!string.IsNullOrEmpty(bayer) || !string.IsNullOrEmpty(flam))
                {
                    name = $"{flam} {bayer} {con}".Trim();
                }
                else if (idxHd >= 0 && idxHd < cols.Length && !string.IsNullOrWhiteSpace(cols[idxHd]))
                {
                    name = $"HD {cols[idxHd].Trim()}";
                }
                else if (idxHip >= 0 && idxHip < cols.Length && !string.IsNullOrWhiteSpace(cols[idxHip]))
                {
                    name = $"HIP {cols[idxHip].Trim()}";
                }
                else
                {
                    name = null;
                }
            }

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

            float mag = (idxMag >= 0 && idxMag < cols.Length && ParseFloat(cols[idxMag], out float m)) ? m : 5.0f;

            float temp = 5778.0f;
            if (idxTemp >= 0 && idxTemp < cols.Length && ParseFloat(cols[idxTemp], out float tKelvin))
            {
                temp = tKelvin;
            }
            else if (idxCi >= 0 && idxCi < cols.Length && ParseFloat(cols[idxCi], out float ci))
            {
                temp = 4600.0f * (1.0f / (0.92f * ci + 1.7f) + 1.0f / (0.92f * ci + 0.62f));
            }

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
        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
