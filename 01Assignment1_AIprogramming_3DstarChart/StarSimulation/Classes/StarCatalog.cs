namespace StarChart;

public class StarCatalog
{
    public List<Star> Stars { get; } = new();

    public void AddStar(Star star)
    {
        Stars.Add(star);
    }

    /// <summary>
    /// Finds the closest star in space to a given target star.
    /// </summary>
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
