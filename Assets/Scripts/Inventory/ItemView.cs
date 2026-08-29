using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리에 놓이는 아이템 한 덩어리. 프리팹으로 만들어 InventoryUI 의 Item Prefab 에 연결한다.
///
/// 프리팹 구성 예시
///   ItemView            ... Image(투명해도 됨, Raycast Target 켤 것) + CanvasGroup + 이 스크립트
///     └ Icon            ... Image      -> Icon Image 에 연결
///     └ CountRoot       ... 빈 오브젝트 -> Count Root 에 연결
///          └ Count      ... Text/TMP   -> Count Label 에 연결
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("표시")]
    [Tooltip("아이템 아이콘이 들어갈 Image. 비워두면 이 오브젝트의 Image 를 쓴다.")]
    [SerializeField] private Image iconImage;

    [Tooltip("개수 표시 묶음. 쌓이지 않는 아이템이면 통째로 꺼진다.\n" +
             "반드시 자식 오브젝트를 넣을 것. 이 오브젝트 자신을 넣으면 아이템이 통째로 사라진다.")]
    [SerializeField] private GameObject countRoot;

    [Header("칸 차지 배경")]
    [Tooltip("아이템이 몇 칸을 차지하는지 보여줄 반투명 배경. 비워두면 루트 Image 를 그대로 쓴다.\n" +
             "아이템 사각형 전체를 덮으므로 따로 크기를 맞출 필요는 없다.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("배경에 쓸 스프라이트. 비우면 단색 사각형이 깔린다. 9-슬라이스 테두리를 넣어도 된다.")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("보관함 격자에서의 배경 색. 알파를 낮춰 반투명하게.")]
    [SerializeField] private Color gridBackgroundColor = new Color(0f, 0f, 0f, 0.22f);

    [Tooltip("장착 슬롯에서도 배경을 깔지. 보통은 슬롯 그림이 이미 있으니 꺼둔다.")]
    [SerializeField] private bool showBackgroundInSlot;

    [SerializeField] private Color slotBackgroundColor = new Color(0f, 0f, 0f, 0.12f);

    [SerializeField] private UILabel countLabel = new UILabel();

    public ItemInstance Item { get; private set; }
    public InventoryUI Owner { get; private set; }

    /// <summary>장착 슬롯에서 꺼낸 것인지, 보관함 격자에서 꺼낸 것인지.</summary>
    public bool FromEquipSlot { get; private set; }
    public ItemType EquipSlotType { get; private set; }
    public int EquipSlotIndex { get; private set; }

    /// <summary>드롭 대상이 받아갔는지.</summary>
    public bool DropHandled { get; set; }

    public RectTransform RectTransform => transform as RectTransform;

    private CanvasGroup canvasGroup;

    public void Bind(InventoryUI owner, ItemInstance item, bool fromEquipSlot, ItemType slotType = ItemType.Misc, int slotIndex = -1)
    {
        Owner = owner;
        Item = item;
        FromEquipSlot = fromEquipSlot;
        EquipSlotType = slotType;
        EquipSlotIndex = slotIndex;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        EnsureIconChild();

        if (iconImage != null && item.data != null)
        {
            // 격자에 놓였을 때와 장착 슬롯에 끼워졌을 때 각각 다른 그림/각도를 쓸 수 있다.
            Sprite icon = item.data.GetIcon(fromEquipSlot);

            iconImage.sprite = icon;
            iconImage.color = item.data.tint;
            iconImage.preserveAspect = true;
            iconImage.enabled = icon != null;

            ApplyIconTransform(item.data, fromEquipSlot);
        }

        ApplyBackground(fromEquipSlot);

        bool stackable = item.data != null && item.data.maxStack > 1;

        // Count Root 에 자기 자신을 꽂아두면, 안 쌓이는 아이템일 때 SetActive(false) 로
        // 아이템 위젯 전체가 꺼져버려서 "인벤토리에 안 들어온 것처럼" 보인다.
        if (countRoot == gameObject)
        {
            Debug.LogWarning(
                $"[ItemView] '{name}' 프리팹의 Count Root 에 자기 자신이 들어가 있습니다. " +
                "개수 텍스트를 담은 자식 오브젝트를 넣거나 비워두세요. 이번에는 무시합니다.", this);
        }
        else if (countRoot != null)
        {
            countRoot.SetActive(stackable);
        }

        if (stackable) countLabel.Set(item.count.ToString());
    }

    /// <summary>
    /// 아이콘은 반드시 자식 오브젝트여야 한다.
    /// 루트를 돌리면 위젯 사각형 자체가 기울어져서 격자에 놓을 때 좌상단 계산이 어긋난다.
    ///
    /// 프리팹에서 Icon 을 따로 안 만들었으면 여기서 자동으로 만들어준다.
    /// (루트 Image 는 드래그를 받는 판정용으로만 남기고 투명하게 바꾼다)
    /// </summary>
    private void EnsureIconChild()
    {
        Image rootImage = GetComponent<Image>();

        // 아이콘이 루트에 붙어 있으면 자식으로 옮긴다.
        if (iconImage == null || iconImage.rectTransform == transform)
        {
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(transform, false);

            RectTransform rt = (RectTransform)iconGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false; // 드래그는 루트(배경)가 받는다

            iconImage = icon;
        }

        // 루트 Image 는 "몇 칸 차지하는지" 보여주는 배경 겸 드래그 판정판으로 쓴다.
        if (backgroundImage == null) backgroundImage = rootImage;
    }

    /// <summary>
    /// 아이템 사각형 전체에 반투명 배경을 깐다.
    /// 이 사각형이 곧 차지하는 칸 수(가로 W칸 × 세로 H칸)라서, 따로 계산할 게 없다.
    /// </summary>
    private void ApplyBackground(bool inEquipSlot)
    {
        if (backgroundImage == null) return;

        bool show = !inEquipSlot || showBackgroundInSlot;

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = backgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backgroundImage.color = show
            ? (inEquipSlot ? slotBackgroundColor : gridBackgroundColor)
            : new Color(1f, 1f, 1f, 0f);

        // 색이 투명해도 드래그는 받아야 한다.
        backgroundImage.raycastTarget = true;
    }

    /// <summary>ItemData 에 지정한 각도/배율/오프셋을 아이콘에만 적용한다.</summary>
    private void ApplyIconTransform(ItemData data, bool inEquipSlot)
    {
        RectTransform rt = iconImage.rectTransform;
        if (rt == transform) return; // EnsureIconChild 가 처리했어야 하는 경우

        rt.localRotation = Quaternion.Euler(0f, 0f, data.GetIconAngle(inEquipSlot));
        rt.localScale = Vector3.one * Mathf.Max(0.01f, data.GetIconScale(inEquipSlot));
        rt.anchoredPosition = data.GetIconOffset(inEquipSlot);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Item == null || Owner == null) return;

        DropHandled = false;

        canvasGroup.blocksRaycasts = false; // 이걸 꺼야 밑에 있는 격자/슬롯이 드롭을 받는다
        canvasGroup.alpha = 0.8f;

        // 부모 변경 / 크기 정규화 / 잡은 지점 계산은 순서가 중요해서 InventoryUI 가 한 번에 처리한다.
        Owner.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Owner == null || Owner.DragLayer == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Owner.DragLayer, eventData.position, Owner.UICamera, out Vector2 local))
        {
            RectTransform.localPosition = local + Owner.DragGrabOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        Owner.EndDrag();

        // 성공했든 실패했든 이 위젯은 버리고 모델 기준으로 전부 다시 그린다.
        // (드래그 중에는 DragLayer 밑에 있어서 Refresh 가 못 지운다)
        transform.SetParent(null, false);
        Destroy(gameObject);
        Owner.Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (Item == null || Owner == null) return;

        Owner.QuickMove(this);
    }
}
