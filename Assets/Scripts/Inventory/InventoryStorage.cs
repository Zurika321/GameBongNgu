using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class InventoryStorage : MonoBehaviour
{
    [Header("Thông tin rương")]
    [SerializeField] private string containerId = "Nhà Hoang - Rương";
    [SerializeField] private string displayName = "Rương gỗ";

    [Header("Prompt (kéo vào Inspector)")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string interactionPrompt = "Press 'E' to interact";

    [Header("Cửa sổ kho (thường đặt trong CoreCombatDemo)")]
    [SerializeField] private StorageWindowUI window;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();

    public string ContainerId => containerId;
    public string DisplayName => displayName;
    public string InteractionPrompt => interactionPrompt;

    // ConnectedStoryDirector cần biến này để kiểm tra khoảng cách tới rương.
    public float InteractionRange
    {
        get
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null) return 0f;
            Vector3 e = col.bounds.extents;
            return Mathf.Max(e.x, e.y);
        }
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        ResolveWindow();

        if (window == null)
            Debug.LogWarning("[InventoryStorage] Chưa tìm thấy StorageWindowUI. Rương sẽ tự kết nối khi cửa sổ được nạp.", this);

        if (promptText != null)
        {
            promptText.text = interactionPrompt;
            promptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (window == null) ResolveWindow();

        bool inRange = playerColliders.Count > 0;
        bool open = window != null && window.IsOpenFor(this);
        bool showPrompt = inRange && !open && !GameplayPause.IsPaused;

        if (window != null && window.HasInteractionPrompt)
        {
            window.SetInteractionPrompt(this, showPrompt);
            if (promptText != null) promptText.gameObject.SetActive(false);
        }
        else if (promptText != null)
        {
            promptText.text = interactionPrompt;
            promptText.gameObject.SetActive(showPrompt);
        }

        if (!inRange || window == null || !Input.GetKeyDown(KeyCode.E)) return;

        if (open) window.Close();                                   // đang mở -> E để đóng
        else if (!GameplayPause.IsPaused && !window.IsOpen) window.Open(this);
    }

    private void ResolveWindow()
    {
        if (window != null) return;
        window = StorageWindowUI.FindWindow();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            playerColliders.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
        if (playerColliders.Count == 0 && window != null && window.IsOpenFor(this))
            window.Close();                                         // đi ra khỏi vùng -> tự đóng + lưu
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (window != null) window.SetInteractionPrompt(this, false);
        if (window != null && window.IsOpenFor(this)) window.Close();
        if (promptText != null) promptText.gameObject.SetActive(false);
    }
}
