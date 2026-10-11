using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Animation đơn giản cho vật phẩm nằm trong world (bồng bềnh idle) và hiệu ứng khi nhặt.
    /// Không liên quan đến state Player/Enemy.
    /// </summary>
    public class ItemAnimation : MonoBehaviour
    {
        [Header("Idle bobbing")]
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;

        [Header("Pickup effect")]
        [SerializeField] private GameObject pickupVfxPrefab;

        private Vector3 _startPos;

        private void Start()
        {
            _startPos = transform.position;
        }

        private void Update()
        {
            float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _startPos + new Vector3(0f, offset, 0f);
        }

        /// <summary>Gọi khi Inventory nhặt được item — chạy hiệu ứng rồi tự hủy.</summary>
        public void PlayPickupEffect()
        {
            if (pickupVfxPrefab != null)
            {
                Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}
