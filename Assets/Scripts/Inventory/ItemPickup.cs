using UnityEngine;

/// <summary>
/// 바닥에 떨어져 있는 아이템. 플레이어가 닿거나 키를 누르면 인벤토리로 들어간다.
///
/// 만드는 법
///   빈 GameObject + SpriteRenderer(아이템 그림) + Collider2D(Is Trigger 켜기) + 이 스크립트
///   Item 에 ItemData 에셋을 넣으면 끝.
///
/// 보관함이 꽉 차서 일부만 들어간 경우, 남은 개수만큼 바닥에 계속 남아있는다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    [Header("무엇을 주는가")]
    [SerializeField] private ItemData item;
    [Min(1)] [SerializeField] private int count = 1;

    [Header("줍는 방식")]
    [Tooltip("켜면 닿는 즉시 줍는다. 끄면 범위 안에서 아래 키를 눌러야 한다.")]
    [SerializeField] private bool pickupOnTouch = true;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;

    [Header("연출 (선택)")]
    [Tooltip("범위 안에 들어왔을 때만 켜줄 오브젝트. 'F' 아이콘 같은 것.")]
    [SerializeField] private GameObject promptRoot;

    [SerializeField] private AudioClip pickupSfx;

    [Tooltip("주웠을 때 터뜨릴 이펙트 프리팹. 앞서 만든 EffectImage 프리팹을 써도 된다.")]
    [SerializeField] private GameObject pickupEffect;

    [Header("둥둥 뜨는 연출")]
    [Tooltip("위아래로 흔들리는 폭(월드 단위). 0이면 가만히 있는다.")]
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2f;

    private const float retryInterval = 0.5f;

    private PlayerInventory nearby;
    private Vector3 basePosition;
    private float bobTimer;
    private float retryTimer;

    // ---------------- 수명주기 ----------------
    private void Reset()
    {
        // 컴포넌트를 처음 붙였을 때 트리거로 만들어준다. (안 그러면 플레이어가 아이템에 부딪혀 멈춘다)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        basePosition = transform.position;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[ItemPickup] '{name}' 의 Collider2D 가 트리거가 아닙니다. Is Trigger 를 켭니다.", this);
            col.isTrigger = true;
        }

        if (item == null)
            Debug.LogWarning($"[ItemPickup] '{name}' 에 Item 이 비어 있습니다.", this);

        SetPrompt(false);
    }

    private void Update()
    {
        if (bobHeight > 0f)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(bobTimer) * bobHeight);
        }

        if (nearby == null) return;

        if (!pickupOnTouch)
        {
            if (Input.GetKeyDown(pickupKey)) TryPickup(nearby);
            return;
        }

        // 닿는 즉시 줍는 모드에서, 처음엔 보관함이 꽉 차 실패했더라도
        // 플레이어가 자리를 비우면 다시 주울 수 있게 위에 서 있는 동안 주기적으로 재시도한다.
        retryTimer -= Time.deltaTime;
        if (retryTimer <= 0f)
        {
            retryTimer = retryInterval;
            TryPickup(nearby);
        }
    }

    // ---------------- 감지 ----------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInventory inv = FindInventory(other);
        if (inv == null) return;

        nearby = inv;

        if (pickupOnTouch) TryPickup(inv);
        else SetPrompt(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (FindInventory(other) == null) return;

        nearby = null;
        SetPrompt(false);
    }

    /// <summary>
    /// 부딪힌 콜라이더에서 플레이어 인벤토리를 찾는다.
    /// 콜라이더가 자식(발밑 판정 등)에 달려 있을 수 있어서 부모까지 훑는다.
    /// PlayerInventory 를 가진 상대만 통과하므로 적이나 투사체가 아이템을 먹지 않는다.
    /// </summary>
    private static PlayerInventory FindInventory(Collider2D other)
    {
        return other.GetComponentInParent<PlayerInventory>();
    }

    // ---------------- 줍기 ----------------
    [ContextMenu("테스트: 지금 줍기")]
    private void PickupNow()
    {
        if (Application.isPlaying) TryPickup(PlayerInventory.Instance);
    }

    private void TryPickup(PlayerInventory inv)
    {
        if (inv == null || item == null) return;

        ItemInstance instance = new ItemInstance(item, count);
        int before = instance.count;

        bool all = inv.Pickup(instance);
        int taken = all ? before : before - instance.count;

        if (taken <= 0)
        {
            Debug.Log($"[ItemPickup] 보관함이 꽉 차서 '{item.displayName}' 을(를) 줍지 못했습니다.", this);
            return;
        }

        PlayFeedback();

        if (all)
        {
            Destroy(gameObject);
            return;
        }

        // 일부만 들어갔다. 남은 만큼만 바닥에 남긴다.
        count = instance.count;
        Debug.Log($"[ItemPickup] '{item.displayName}' {taken}개만 넣었습니다. {count}개가 남아있습니다.", this);
    }

    private void PlayFeedback()
    {
        if (pickupSfx != null)
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
    }

    private void SetPrompt(bool on)
    {
        if (promptRoot != null) promptRoot.SetActive(on);
    }
}
