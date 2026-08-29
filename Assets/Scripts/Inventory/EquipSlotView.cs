using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 무기/방어구 한 칸. 캔버스에서 직접 만든 슬롯 이미지에 붙인다.
///
/// 반드시 Canvas 아래의 UI 오브젝트여야 한다. (Create Empty 로 만든 일반 오브젝트가 아니라
/// UI > Image 로 만들거나, 캔버스 자식으로 만든 오브젝트여야 RectTransform 을 갖는다)
/// Image 컴포넌트가 있어야 드롭을 받을 수 있다 (Raycast Target 켤 것).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EquipSlotView : MonoBehaviour, IItemDropTarget, IDropHandler
{
    [Header("이 슬롯이 무엇인지")]
    public ItemType slotType = ItemType.Weapon;

    [Tooltip("같은 종류 안에서의 순번. 무기 1 = 0, 무기 2 = 1")]
    public int slotIndex;

    [Header("연결 (선택)")]
    [Tooltip("아이템이 들어갈 자리. 비워두면 이 오브젝트 자신.")]
    [SerializeField] private RectTransform itemAnchor;

    [Tooltip("비어 있을 때만 보여줄 것 (실루엣 아이콘, '무기 1' 글자 등). 아래 자동 숨김을 쓰면 안 채워도 된다.")]
    [SerializeField] private GameObject emptyPlaceholder;

    [Tooltip("켜면 아이템이 장착됐을 때 이 슬롯의 자식 오브젝트를 전부 숨긴다.\n" +
             "'무기 1' 같은 예시 그림을 하나하나 연결하지 않아도 알아서 사라진다.\n" +
             "슬롯 테두리를 자식으로 뒀다면 그것도 같이 숨겨지므로, 그럴 땐 끄고 위 칸에 직접 연결할 것.")]
    [SerializeField] private bool hideChildrenWhenFilled = true;

    [Tooltip("이 슬롯에 낄 수 있는 아이템을 드래그 중일 때 켜줄 강조 표시.")]
    [SerializeField] private GameObject compatibleHighlight;

    [Header("아이템 여백")]
    [Tooltip("슬롯 안쪽에서 아이템을 얼마나 띄울지(픽셀).")]
    [SerializeField] private float itemPadding = 6f;

    public InventoryUI Owner { get; set; }
    public float ItemPadding => itemPadding;

    /// <summary>
    /// 아이템이 들어갈 자리. Item Anchor 를 비워두면 이 오브젝트 자신을 쓴다.
    /// UI 오브젝트가 아니면 RectTransform 이 없어서 null 이 나온다. ('as' 캐스팅이라 예외 대신 null)
    /// </summary>
    public RectTransform ItemAnchor
    {
        get
        {
            if (itemAnchor != null) return itemAnchor;
            return transform as RectTransform;
        }
    }

    private void Awake()
    {
        if (transform as RectTransform == null)
        {
            Debug.LogError(
                $"[EquipSlotView] '{name}' 이 UI 오브젝트가 아닙니다 (RectTransform 없음). " +
                "Canvas 안에서 UI > Image 로 만든 오브젝트에 붙여야 합니다.", this);
        }
    }

    public void SetEmpty(bool empty)
    {
        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(empty);

        if (!hideChildrenWhenFilled) return;

        // 직속 자식만 훑는다. 아이템 위젯과 아이템이 들어갈 자리, 강조 표시는 건드리지 않는다.
        // (이 함수는 아이템 위젯을 만들기 "전"에 불리므로 아직 ItemView 자식은 없다)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.GetComponent<ItemView>() != null) continue;
            if (itemAnchor != null && child == itemAnchor) continue;
            if (compatibleHighlight != null && child.gameObject == compatibleHighlight) continue;

            child.gameObject.SetActive(empty);
        }
    }

    public void SetHighlight(bool on)
    {
        if (compatibleHighlight != null) compatibleHighlight.SetActive(on);
    }

    public void OnDrop(PointerEventData eventData)
    {
        ItemView view = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemView>() : null;
        if (view == null) return;
        view.DropHandled = AcceptDrop(view);
    }

    public bool AcceptDrop(ItemView view)
    {
        return Owner != null && Owner.DropOnEquipSlot(view, slotType, slotIndex);
    }
}
