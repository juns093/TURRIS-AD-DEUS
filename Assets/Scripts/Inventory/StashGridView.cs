using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타르코프식 격자 영역. 캔버스에서 만든 "보관함 배경" RectTransform 에 붙인다.
///
/// 자식 구조 (권장)
///   Grid            ... Image(Raycast Target 켤 것) + 이 스크립트
///     └ Cells       ... 빈 칸 배경들이 생성될 자리   -> Cell Layer
///     └ Items       ... 아이템이 올라갈 자리          -> Item Layer
/// Cells / Items 는 부모에 꽉 차게(Stretch) 맞춰두면 된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class StashGridView : MonoBehaviour, IItemDropTarget, IDropHandler
{
    [Header("칸 크기")]
    [Tooltip("칸 하나의 픽셀 크기. 쓰는 UI 아트의 칸 크기에 맞춘다.")]
    public int cellSize = 64;

    [Tooltip("칸 사이 간격.")]
    public int cellGap = 2;

    [Header("연결")]
    [Tooltip("아이템이 올라갈 부모. 비워두면 이 오브젝트 자신.")]
    [SerializeField] private RectTransform itemLayer;

    [Tooltip("빈 칸 배경이 생성될 부모. 비워두면 칸 배경을 만들지 않는다.")]
    [SerializeField] private RectTransform cellLayer;

    [Tooltip("빈 칸 배경으로 쓸 스프라이트. UI 팩의 Item Holder 이미지를 넣으면 된다.")]
    [SerializeField] private Sprite cellSprite;

    [SerializeField] private Color cellColor = Color.white;

    [Header("에디터 미리보기")]
    [Tooltip("아래 컨텍스트 메뉴로 칸을 깔 때 쓸 크기. 실제 크기는 PlayerInventory 값을 따른다.")]
    [SerializeField] private Vector2Int editorPreviewSize = new Vector2Int(10, 12);

    [Tooltip("켜면 게임 시작할 때 PlayerInventory 의 보관함 크기에 맞춰 이 영역 크기를 자동으로 맞춘다.")]
    [SerializeField] private bool autoFitSize = true;

    public InventoryUI Owner { get; set; }

    public int Step => cellSize + cellGap;

    /// <summary>UI 오브젝트가 아니면 null. ('as' 캐스팅이라 예외 대신 null 이 나온다)</summary>
    public RectTransform Rect => transform as RectTransform;
    public RectTransform ItemLayer => itemLayer != null ? itemLayer : Rect;

    private void Awake()
    {
        if (Rect == null)
        {
            Debug.LogError(
                $"[StashGridView] '{name}' 이 UI 오브젝트가 아닙니다 (RectTransform 없음). " +
                "Canvas 안에서 UI > Image 로 만든 오브젝트에 붙여야 합니다.", this);
        }
    }

    // ---------------- 좌표 변환 ----------------
    /// <summary>드래그 중인 사각형의 좌상단 모서리가 몇 번 칸 위에 있는지. 피벗 설정과 무관하게 동작한다.</summary>
    public Vector2Int WorldTopLeftToCell(RectTransform itemRect)
    {
        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners); // 0=좌하 1=좌상 2=우상 3=우하

        Vector2 local = Rect.InverseTransformPoint(corners[1]);

        float fromLeft = local.x - Rect.rect.xMin;
        float fromTop = Rect.rect.yMax - local.y;

        return new Vector2Int(
            Mathf.RoundToInt(fromLeft / Step),
            Mathf.RoundToInt(fromTop / Step));
    }

    /// <summary>
    /// 격자 좌표 -> 그 칸 좌상단의 월드 좌표.
    ///
    /// ItemLayer / CellBG 가 격자와 정확히 겹쳐 있지 않아도(앵커를 잘못 잡았어도) 항상 맞게 놓이도록,
    /// 부모 기준 좌표가 아니라 격자 사각형 자체를 기준으로 계산한다.
    /// 아이템은 피벗이 좌상단이므로 이 값을 transform.position 에 그대로 넣으면 된다.
    /// </summary>
    public Vector3 CellToWorldTopLeft(int x, int y)
    {
        Vector2 local = new Vector2(
            Rect.rect.xMin + x * Step,
            Rect.rect.yMax - y * Step);

        return Rect.TransformPoint(local);
    }

    /// <summary>격자 좌표 -> ItemLayer 안에서의 위치. (ItemLayer 가 격자에 정확히 겹쳐 있을 때만 맞다)</summary>
    public Vector2 CellToAnchoredPosition(int x, int y)
    {
        return new Vector2(x * Step, -y * Step);
    }

    public Vector2 SizeFor(ItemInstance item)
    {
        return new Vector2(item.Width * Step - cellGap, item.Height * Step - cellGap);
    }

    // ---------------- 크기 맞추기 / 칸 깔기 ----------------
    public void FitToGrid(int width, int height)
    {
        if (!autoFitSize || Rect == null) return;

        // 앵커가 늘어나 있으면(Stretch) sizeDelta 는 '여백'을 뜻해서 크기를 직접 못 정한다.
        // 이 경우 손대면 오히려 어긋나므로 그냥 둔다.
        if (Rect.anchorMin != Rect.anchorMax)
        {
            Debug.LogWarning(
                $"[StashGridView] '{name}' 의 앵커가 Stretch 라 크기를 자동으로 맞출 수 없습니다. " +
                "Auto Fit Size 를 끄고 크기를 직접 맞추거나, 앵커를 한 점으로 바꾸세요.", this);
            return;
        }

        Rect.sizeDelta = new Vector2(width * Step - cellGap, height * Step - cellGap);
    }

    /// <summary>빈 칸 배경을 깐다. 에디터에서 컨텍스트 메뉴로 미리 깔아두거나, 실행 시 자동으로 부른다.</summary>
    public void BuildCells(int width, int height)
    {
        if (cellLayer == null || cellSprite == null) return;

        ClearCells();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cellLayer, false);

                RectTransform rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.position = CellToWorldTopLeft(x, y); // 격자 사각형 기준으로 정확히 얹는다

                Image img = go.GetComponent<Image>();
                img.sprite = cellSprite;
                img.color = cellColor;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false; // 드롭은 부모 격자가 받는다
            }
        }
    }

    private void ClearCells()
    {
        if (cellLayer == null) return;

        for (int i = cellLayer.childCount - 1; i >= 0; i--)
        {
            GameObject child = cellLayer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    [ContextMenu("칸 깔기 (미리보기 크기로)")]
    private void BuildCellsPreview()
    {
        FitToGrid(editorPreviewSize.x, editorPreviewSize.y);
        BuildCells(editorPreviewSize.x, editorPreviewSize.y);
    }

    [ContextMenu("칸 지우기")]
    private void ClearCellsMenu() => ClearCells();

    // ---------------- 드롭 ----------------
    public void OnDrop(PointerEventData eventData)
    {
        ItemView view = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemView>() : null;
        if (view == null) return;
        view.DropHandled = AcceptDrop(view);
    }

    public bool AcceptDrop(ItemView view)
    {
        return Owner != null && Owner.DropOnStash(view);
    }
}
