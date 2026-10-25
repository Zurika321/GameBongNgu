using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MapZoneGraphic : Graphic
{
    [SerializeField] private float lineThickness = 3f;

    private Transform mapTarget;
    private float worldRadius = 8f;
    private MapZoneRegion[] regions = new MapZoneRegion[0];
    private bool cacheDirty = true;
    private bool hasRenderState;
    private Vector2 lastTargetPosition;
    private float lastWorldRadius;
    private float nextCacheRefreshTime;

    protected override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        cacheDirty = true;
    }

    protected override void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        base.OnDisable();
    }

    public void Refresh(Transform target, float radius)
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        bool targetChanged = mapTarget != target;
        mapTarget = target;
        worldRadius = Mathf.Max(1f, radius);
        bool radiusChanged = Mathf.Abs(worldRadius - lastWorldRadius) > 0.01f;
        Vector2 targetPosition = mapTarget != null ? (Vector2)mapTarget.position : Vector2.zero;
        bool targetMoved = !hasRenderState || (targetPosition - lastTargetPosition).sqrMagnitude >= 0.0025f;
        bool refreshCache = cacheDirty || (targetMoved && Time.unscaledTime >= nextCacheRefreshTime);

        if (refreshCache)
        {
            regions = FindObjectsByType<MapZoneRegion>(FindObjectsSortMode.None);
            nextCacheRefreshTime = Time.unscaledTime + 1f;
            cacheDirty = false;
        }

        if (!hasRenderState || targetChanged || targetMoved || radiusChanged || refreshCache)
        {
            lastTargetPosition = targetPosition;
            lastWorldRadius = worldRadius;
            hasRenderState = true;
            SetVerticesDirty();
        }
    }

    public void InvalidateCache()
    {
        cacheDirty = true;
        hasRenderState = false;
        SetVerticesDirty();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => InvalidateCache();
    private void HandleSceneUnloaded(Scene scene) => InvalidateCache();

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        foreach (MapZoneRegion region in regions)
        {
            if (region == null || region.Polygon == null || region.Polygon.pathCount == 0) continue;
            AddPolygonOutline(vertexHelper, region);
        }
    }

    private void AddPolygonOutline(VertexHelper vertexHelper, MapZoneRegion region)
    {
        Vector2[] path = region.Polygon.GetPath(0);
        Rect bounds = rectTransform.rect;
        float mapScale = bounds.height * 0.5f;
        Vector2[] points = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 worldPoint = region.Polygon.transform.TransformPoint(path[i]);
            points[i] = bounds.center + WorldToMapPosition(worldPoint) * mapScale;
        }

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            if (!ClipSegmentToRect(ref start, ref end, bounds)) continue;
            Vector2 edge = end - start;
            if (edge.sqrMagnitude < 0.001f) continue;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized * lineThickness * 0.5f;
            AddQuad(vertexHelper, start - normal, start + normal, end + normal, end - normal, region.MapColor);
        }
    }

    private static bool ClipSegmentToRect(ref Vector2 start, ref Vector2 end, Rect rect)
    {
        Vector2 delta = end - start;
        float tMin = 0f;
        float tMax = 1f;
        if (!Clip(-delta.x, start.x - rect.xMin, ref tMin, ref tMax)
            || !Clip(delta.x, rect.xMax - start.x, ref tMin, ref tMax)
            || !Clip(-delta.y, start.y - rect.yMin, ref tMin, ref tMax)
            || !Clip(delta.y, rect.yMax - start.y, ref tMin, ref tMax))
            return false;

        Vector2 originalStart = start;
        start = originalStart + delta * tMin;
        end = originalStart + delta * tMax;
        return true;
    }

    private static bool Clip(float p, float q, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(p) < 0.000001f) return q >= 0f;
        float ratio = q / p;
        if (p < 0f)
        {
            if (ratio > tMax) return false;
            if (ratio > tMin) tMin = ratio;
        }
        else
        {
            if (ratio < tMin) return false;
            if (ratio < tMax) tMax = ratio;
        }
        return true;
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
