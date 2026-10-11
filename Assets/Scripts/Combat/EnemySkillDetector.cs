using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemySkillDetector : MonoBehaviour
{
    private EnemySkillController controller;
    private EnemyController enemyController;
    private int slotIndex;
    private string detectTag;

    public void Init(
        EnemySkillController controller,
        int slotIndex,
        string detectTag,
        EnemyController enemyController)
    {
        this.controller = controller;
        this.slotIndex = slotIndex;
        this.detectTag = detectTag;
        this.enemyController = enemyController;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(detectTag) || !other.CompareTag(detectTag))
            return;

        if (controller != null) controller.TryCastSlot(slotIndex, other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(detectTag) || !other.CompareTag(detectTag))
            return;

        // Vẫn ở trong vùng → tiếp tục kiểm tra cooldown để cast skill
        if (controller != null) controller.TryCastSlot(slotIndex, other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // if (string.IsNullOrEmpty(detectTag) || !other.CompareTag(detectTag))
        //     return;

        // // Target rời khỏi vùng → ngừng đuổi
        // if (enemyController != null)
        // {
        //     enemyController.ClearTarget();
        // }
        // else
        // {
        //     Debug.LogWarning(
        //         "EnemySkillDetector không tìm thấy EnemyController.",
        //         this
        //     );
        // }
    }
}
