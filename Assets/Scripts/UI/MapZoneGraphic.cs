using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MapZoneGraphic : Graphic
{
    [SerializeField] private float lineThickness = 3f;
    [SerializeField] private float markerSize = 12f;

    private Transform mapTarget;
    private float worldRadius = 8f;
    private MapZoneRegion[] regions = new MapZoneRegion[0];
    private EnemyController[] enemies = new EnemyController[0];
    private DemoNpcDialogueProfile[] npcs = new DemoNpcDialogueProfile[0];

    public void Refresh(Transform target, float radius)
    {
        mapTarget = target;
        if (mapTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) mapTarget = player.transform;
        }

        worldRadius = Mathf.Max(1f, radius);
        regions = FindObjectsByType<MapZoneRegion>();
        enemies = FindObjectsByType<EnemyController>();
        npcs = FindObjectsByType<DemoNpcDialogueProfile>();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        foreach (MapZoneRegion region in regions)
        {
            if (region == null || region.Polygon == null || region.Polygon.pathCount == 0) continue;
            AddPolygonOutline(vertexHelper, region);
        }

        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null) AddMarker(vertexHelper, enemy.transform.position, new Color(1f, 0.38f, 0.32f, 1f));
        }

        foreach (DemoNpcDialogueProfile npc in npcs)
        {
            if (npc != null) AddMarker(vertexHelper, npc.transform.position, new Color(0.72f, 0.58f, 1f, 1f));
        }

        if (mapTarget != null)
        {
            AddMarker(vertexHelper, mapTarget.position, new Color(1f, 0.9f, 0.35f, 1f), markerSize * 1.5f);
        }
    }

    private void AddPolygonOutline(VertexHelper vertexHelper, MapZoneRegion region)
    {
        Vector2[] path = region.Polygon.GetPath(0);
        float mapScale = rectTransform.rect.height * 0.5f;
        Vector2[] points = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 worldPoint = region.Polygon.transform.TransformPoint(path[i]);
            points[i] = WorldToMapPosition(worldPoint) * mapScale;
        }

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            Vector2 edge = end - start;
            if (edge.sqrMagnitude < 0.001f) continue;

            Vector2 normal = new Vector2(-edge.y, edge.x).normalized * lineThickness * 0.5f;
            AddQuad(vertexHelper, start - normal, start + normal, end + normal, end - normal, region.MapColor);
        }
    }

    private void AddMarker(VertexHelper vertexHelper, Vector3 worldPosition, Color markerColor, float? size = null)
    {
        float mapScale = rectTransform.rect.height * 0.5f;
        Vector2 center = WorldToMapPosition(worldPosition) * mapScale;
        float halfSize = (size ?? markerSize) * 0.5f;
        AddQuad(
            vertexHelper,
            center + new Vector2(-halfSize, -halfSize),
            center + new Vector2(-halfSize, halfSize),
            center + new Vector2(halfSize, halfSize),
            center + new Vector2(halfSize, -halfSize),
            markerColor);
    }

    private Vector2 WorldToMapPosition(Vector3 worldPosition)
    {
        Vector2 mapCenter = mapTarget != null ? (Vector2)mapTarget.position : Vector2.zero;
        return ((Vector2)worldPosition - mapCenter) / worldRadius;
    }

    private static void AddQuad(VertexHelper vertexHelper, Vector2 bottomLeft, Vector2 topLeft,
        Vector2 topRight, Vector2 bottomRight, Color vertexColor)
    {
        int firstVertex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, bottomLeft, vertexColor);
        AddVertex(vertexHelper, topLeft, vertexColor);
        AddVertex(vertexHelper, topRight, vertexColor);
        AddVertex(vertexHelper, bottomRight, vertexColor);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertexHelper.AddVert(vertex);
    }
}
