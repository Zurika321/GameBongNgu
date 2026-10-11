using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class MapZoneRegion : MonoBehaviour
{
    [SerializeField] private string displayName = "Zone";
    [SerializeField] private Color mapColor = new Color(0.25f, 0.75f, 0.9f, 1f);
    [SerializeField] private PolygonCollider2D polygon;

    public string DisplayName => displayName;
    public Color MapColor => mapColor;
    public PolygonCollider2D Polygon => polygon;

    private void Awake()
    {
        ResolvePolygon();
        if (polygon != null)
        {
            polygon.isTrigger = true;
            // polygon.enabled = false;
        }
    }

    private void OnValidate()
    {
        ResolvePolygon();
    }

    private void ResolvePolygon()
    {
        if (polygon == null) polygon = GetComponent<PolygonCollider2D>();
    }
}
