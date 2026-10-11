using UnityEngine;

public class StoryLootChest : MonoBehaviour
{
    [SerializeField] private string itemName = "Nhật Ký Nạn Nhân";
    [SerializeField] private string requiredFlag = "AtticStairsOpen";
    [SerializeField] private string openedFlag = "AtticChestOpened";
    [SerializeField] private StoryEventDirector storyEvents;
    [SerializeField] private string onOpenedEventId = "DiaryFound";
    [SerializeField, Min(0.1f)] private float interactionRange = 2f;

    private Transform player;
    private bool inventoryFull;

    private void Update()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || GameplayPause.IsPaused
            || DemoNpcDialogueController.IsInteractionBlocked) return;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }
        if (player == null || !progress.Data.HasFlag(requiredFlag)
            || Vector2.Distance(player.position, transform.position) > interactionRange)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (progress.Data.HasFlag(openedFlag))
            {
                TriggerDiaryEvent();
                return;
            }

            if (!progress.AddItem(itemName))
            {
                inventoryFull = true;
                return;
            }
            inventoryFull = false;
            progress.SetFlag(openedFlag);
            TriggerDiaryEvent();
        }
    }

    private void OnGUI()
    {
        PlayerProgressionService progress = PlayerProgressionService.Instance;
        if (progress == null || player == null || GameplayPause.IsPaused
            || DemoNpcDialogueController.IsInteractionBlocked
            || !progress.Data.HasFlag(requiredFlag)
            || Vector2.Distance(player.position, transform.position) > interactionRange)
            return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screen = camera.WorldToScreenPoint(transform.position);
        if (screen.z <= 0f) return;
        Rect label = new Rect(screen.x - 120f, Screen.height - screen.y - 55f, 240f, 28f);
        bool opened = progress.Data.HasFlag(openedFlag);
        if (opened)
        {
            if (storyEvents == null) storyEvents = FindAnyObjectByType<StoryEventDirector>();
            if (storyEvents != null && storyEvents.IsEventCompleted(onOpenedEventId)) return;
        }
        GUI.Label(label, opened
            ? "Nhấn E để tiếp tục xem nhật ký"
            : inventoryFull ? "Túi đồ đầy — hãy tạo chỗ trống rồi nhấn E" : "Nhấn E để mở rương");
    }

    private void TriggerDiaryEvent()
    {
        if (storyEvents == null) storyEvents = FindAnyObjectByType<StoryEventDirector>();
        if (storyEvents == null)
        {
            Debug.LogError("[StoryLootChest] Cannot continue the diary event: StoryEventDirector is missing.", this);
            return;
        }

        storyEvents.TriggerEvent(onOpenedEventId);
    }
}
