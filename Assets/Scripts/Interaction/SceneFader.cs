using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine; // Nếu báo lỗi thiếu namespace, thử đổi thành: using Cinemachine;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [SerializeField] private Image fadeImage;              // Kéo TransitionFade vào đây
    [SerializeField] private CinemachineConfiner2D confiner; // Kéo object CmCam vào đây (chính là component Confiner)
    [SerializeField] private float fadeDuration = 0.4f;     // Thời gian fade mỗi chiều (giây)

    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Gọi hàm này từ DoorTransition để bắt đầu quá trình chuyển phòng
    public void TransitionTo(Transform spawnPoint, Collider2D newBounds)
    {
        if (!isTransitioning)
        {
            StartCoroutine(DoTransition(spawnPoint, newBounds));
        }
    }

    private System.Collections.IEnumerator DoTransition(Transform spawnPoint, Collider2D newBounds)
    {
        isTransitioning = true;

        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.gameObject.SetActive(true);
        }

        // Bước 1: Fade màn hình sang đen
        yield return StartCoroutine(Fade(0f, 1f));

        // Bước 2: Dịch chuyển Player tới vị trí phòng mới (lúc này màn hình đang đen, người chơi không thấy)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = spawnPoint.position;

        // Bước 3: Đổi vùng giới hạn camera sang phòng mới
        confiner.BoundingShape2D = newBounds;
        confiner.InvalidateBoundingShapeCache(); // Bắt Cinemachine tính lại vùng giới hạn theo hình mới

        // Bước 4: Fade màn hình sáng trở lại
        yield return StartCoroutine(Fade(1f, 0f));
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

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
}
