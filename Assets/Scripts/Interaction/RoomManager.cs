

using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;
 
    [SerializeField] private Image fadeImage;
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float extraCooldown = 0.15f;

    private bool isTransitioning = false;
    private GameObject player;
    private Rigidbody2D playerRb;
    //private PlayerMovement playerMovement; // Tham chiếu tới script di chuyển, để tắt hẳn lúc chuyển cảnh

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        playerRb = player.GetComponent<Rigidbody2D>();
       // playerMovement = player.GetComponent<PlayerMovement>();
    }

    public void TransitionRoom(PolygonCollider2D newBounds, Vector3 destinationPosition, Vector2 pushOffset)
    {
        if (!isTransitioning)
        {
            StartCoroutine(DoTransition(newBounds, destinationPosition, pushOffset));
        }
    }

    private System.Collections.IEnumerator DoTransition(PolygonCollider2D newBounds, Vector3 destinationPosition, Vector2 pushOffset)
    {
        isTransitioning = true;

        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.gameObject.SetActive(true);
        }

        // QUAN TRỌNG: Tắt hẳn script di chuyển trong suốt quá trình chuyển cảnh,
        // để Player KHÔNG THỂ nhích đi dù đang giữ phím, tránh lạc vị trí lúc màn hình đen
       // playerMovement.enabled = false;
        playerRb.linearVelocity = Vector2.zero;

        // Bước 1: Fade sang đen
        yield return Fade(0f, 1f);

        // Bước 2: Đổi vùng giới hạn camera + đẩy Player qua khỏi ranh giới
        confiner.BoundingShape2D = newBounds;
        confiner.InvalidateBoundingShapeCache();

        player.transform.position = destinationPosition + (Vector3)pushOffset;
        playerRb.linearVelocity = Vector2.zero;

        // Bước 3: Fade sáng trở lại
        yield return Fade(1f, 0f);
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        // Bước 4: Đợi thêm 1 chút rồi mới cho phép di chuyển + chuyển phòng tiếp theo
        yield return new WaitForSeconds(extraCooldown);

        //playerMovement.enabled = true; // Trả lại quyền điều khiển cho người chơi
        isTransitioning = false;
    }

    private System.Collections.IEnumerator Fade(float fromAlpha, float toAlpha)
    {
        if (fadeImage == null) yield break;

        float timer = 0f;
        Color c = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, timer / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = toAlpha;
        fadeImage.color = c;
    }
    public void SetRoomBoundsImmediate(PolygonCollider2D bounds)
    {
        if (bounds == null || confiner == null)
        {
            return;
        }

        confiner.BoundingShape2D = bounds;
        confiner.InvalidateBoundingShapeCache();
    }
}
