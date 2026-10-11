using System.Collections.Generic;
using System.Numerics;

namespace StarChart;

public class StarCatalog
{
    public List<Star> Stars { get; } = new();

    public void AddStar(Star star)
    {
        Stars.Add(star);
    }

    /// <summary>
    /// Sorts the catalog so named stars appear first, followed by remaining stars ordered by visual magnitude.
    /// </summary>
    public void SortNamedAndBrightestFirst()
    {
        Stars.Sort((a, b) =>
        {
            if (a.HasProperName && !b.HasProperName) return -1;
            if (!a.HasProperName && b.HasProperName) return 1;
            return a.Magnitude.CompareTo(b.Magnitude);
        });
    }

    public Star? FindNearestStar(Star target)
    {
        Star? nearest = null;
        float minDistanceSq = float.MaxValue;

        foreach (var star in Stars)
        {
            if (star == target) continue;

            float distSq = Vector3.DistanceSquared(target.Position, star.Position);
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                nearest = star;
            }
        }

        return nearest;
    }
}
