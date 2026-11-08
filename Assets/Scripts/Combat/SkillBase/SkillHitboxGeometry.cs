using UnityEngine;
public static class SkillHitboxGeometry
{
    // Geometry resolution, not gameplay balance data.
    public static Vector2[] Cone(float radius, float angle, int segments = 24)
    {
        segments = Mathf.Max(3, segments);
        var points = new Vector2[segments + 2];
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-angle / 2f, angle / 2f, i / (float)segments) * Mathf.Deg2Rad;
            points[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return points;
    }
}
