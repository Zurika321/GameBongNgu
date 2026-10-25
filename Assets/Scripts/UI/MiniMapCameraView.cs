using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders a bounded, transparent world view only when its center moves.</summary>
public sealed class MiniMapCameraView : MonoBehaviour
{
    private const int TextureResolution = 512;
    private Camera mainCamera;
    private Camera mapCamera;
    private RenderTexture renderTexture;
    private RawImage destination;
    private Transform target;
    private float viewScale = 3f;
    private Vector3 lastRenderedPosition;
    private float lastRenderedSize;
    private float nextRenderTime;
    private bool dirty = true;

    public void Configure(Camera sourceCamera, Transform followTarget, RawImage output, float scale)
    {
        mainCamera = sourceCamera;
        target = followTarget;
        destination = output;
        viewScale = Mathf.Max(1f, scale);
        if (mainCamera == null || destination == null) return;

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(TextureResolution, TextureResolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "MiniMap Render Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            renderTexture.Create();
        }

        if (mapCamera == null)
        {
            GameObject cameraObject = new GameObject("MiniMap Camera");
            mapCamera = cameraObject.AddComponent<Camera>();
        }

        mapCamera.orthographic = true;
        mapCamera.aspect = 1f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        mapCamera.cullingMask = mainCamera.cullingMask;
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) mapCamera.cullingMask &= ~(1 << uiLayer);
        mapCamera.targetTexture = renderTexture;
        mapCamera.allowHDR = false;
        mapCamera.allowMSAA = false;
        mapCamera.useOcclusionCulling = false;
        mapCamera.enabled = false;

        destination.texture = renderTexture;
        destination.color = Color.white;
        destination.raycastTarget = false;
        destination.uvRect = new Rect(0f, 0f, 1f, 1f);
        dirty = true;
        RenderIfNeeded(true);
    }

    public void SetTarget(Transform followTarget)
    {
        if (target == followTarget) return;
        target = followTarget;
        dirty = true;
    }

    private void LateUpdate() => RenderIfNeeded(false);

    private void RenderIfNeeded(bool force)
    {
        if (mainCamera == null || mapCamera == null || target == null || renderTexture == null) return;

        float viewSize = Mathf.Max(0.01f, mainCamera.orthographicSize * viewScale);
        Vector3 position = target.position;
        position.z = mainCamera.transform.position.z;
        bool moved = (position - lastRenderedPosition).sqrMagnitude >= 0.0025f;
        bool zoomChanged = Mathf.Abs(viewSize - lastRenderedSize) >= 0.01f;
        if (!force && !dirty && !moved && !zoomChanged) return;
        if (!force && !dirty && Time.unscaledTime < nextRenderTime) return;

        mapCamera.orthographicSize = viewSize;
        mapCamera.transform.SetPositionAndRotation(position, Quaternion.identity);
        mapCamera.Render();

        lastRenderedPosition = position;
        lastRenderedSize = viewSize;
        nextRenderTime = Time.unscaledTime + 0.1f;
        dirty = false;
    }

    private void OnDestroy()
    {
        if (mapCamera != null) Destroy(mapCamera.gameObject);
        if (renderTexture != null)
        {
            if (destination != null && destination.texture == renderTexture)
                destination.texture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}

