using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// B키로 여닫는 인벤토리. 캔버스는 에디터에서 직접 만들고, 이 스크립트는 거기에 "붙여서 굴리는" 역할만 한다.
///
/// 이 스크립트가 하는 일
///   - B / Esc 입력 처리, 부드러운 열기·닫기 연출, 게임 일시정지
///   - 배경 블러 스냅샷
///   - PlayerInventory(모델)를 읽어서 격자와 장착 슬롯에 아이템 위젯을 깔기
///   - 드래그 앤 드롭, 우클릭 빠른 장착, R 회전
///
/// 이 스크립트가 하지 않는 일
///   - UI 만들기. 패널/슬롯/격자는 전부 캔버스에서 직접 배치하고 아래 필드에 연결한다.
/// </summary>
[RequireComponent(typeof(ScreenBlurBackground))]
public class InventoryUI : MonoBehaviour
{
    /// <summary>어디서든 InventoryUI.Instance.Toggle() 로 접근한다.</summary>
    public static InventoryUI Instance { get; private set; }

    [Header("싱글톤")]
    [Tooltip("켜면 씬이 바뀌어도 살아남는다. 이 스크립트가 붙은 오브젝트가 하이어라키 최상위여야 한다.")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.B;
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("연결 · 필수")]
    [Tooltip("인벤토리 화면 전체를 담은 오브젝트. 닫힐 때 이게 꺼진다.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("페이드에 쓸 CanvasGroup. 보통 panelRoot 에 붙인다.")]
    [SerializeField] private CanvasGroup panelGroup;

    [Tooltip("열릴 때 확대/축소될 대상. 보통 책 그림이 들어있는 컨테이너.")]
    [SerializeField] private RectTransform contentRoot;

    [Tooltip("드래그하는 동안 아이템이 잠깐 올라갈 빈 RectTransform. 캔버스에서 가장 마지막(맨 위) 자식으로 두고 CanvasGroup 의 Blocks Raycasts 를 꺼둘 것.")]
    [SerializeField] private RectTransform dragLayer;

    [Tooltip("보관함 격자 영역.")]
    [SerializeField] private StashGridView stashGrid;

    [Tooltip("아이템 하나를 그리는 프리팹 (ItemView 스크립트가 붙어있어야 한다).")]
    [SerializeField] private ItemView itemPrefab;

    [Header("연결 · 장착 슬롯")]
    [Tooltip("비워두면 panelRoot 아래에서 EquipSlotView 를 전부 자동으로 찾는다.")]
    [SerializeField] private EquipSlotView[] equipSlots;

    [Header("연결 · 배경 (선택)")]
    [Tooltip("블러 스냅샷이 그려질 RawImage. 비워두면 블러를 쓰지 않는다.")]
    [SerializeField] private RawImage blurImage;

    [Tooltip("블러 위에 덧씌울 검은 막 Image.")]
    [SerializeField] private Image dimImage;

    [Header("연결 · 스탯 라벨 (선택)")]
    [SerializeField] private UILabel attackLabel = new UILabel();
    [SerializeField] private UILabel defenseLabel = new UILabel();
    [SerializeField] private UILabel critChanceLabel = new UILabel();
    [SerializeField] private UILabel critDamageLabel = new UILabel();
    [SerializeField] private UILabel capacityLabel = new UILabel();

    [Header("연결 · 모델 (선택)")]
    [Tooltip("비워두면 PlayerInventory.Instance 를 쓰거나 씬에서 찾는다.")]
    [SerializeField] private PlayerInventory inventory;

    [Tooltip("여는 동안 입력을 막을 컨트롤러. 비워두면 자동으로 찾는다.")]
    [SerializeField] private PlayerController playerController;

    [Header("열기 / 닫기 연출")]
    [SerializeField] private float openDuration = 0.18f;
    [SerializeField] private float closeDuration = 0.13f;

    [Tooltip("열릴 때 시작 크기. 1보다 작으면 살짝 커지면서 나타난다.")]
    [Range(0.5f, 1f)] [SerializeField] private float openScaleFrom = 1;

    [Tooltip("진행도(0~1)에 대한 가속 곡선. 끝을 1보다 살짝 올리면 톡 튀었다 자리잡는다.")]
    [SerializeField]
    private AnimationCurve openCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.2f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("동작")]
    [Tooltip("열려 있는 동안 게임을 멈춘다. 닫히는 연출이 끝나야 다시 흐른다.")]
    [SerializeField] private bool pauseGame = true;
    [SerializeField] private bool showCursor = true;

    [Tooltip("시작할 때 보관함 크기에 맞춰 빈 칸 배경을 다시 깐다. 에디터에서 미리 깔아뒀으면 꺼도 된다.")]
    [SerializeField] private bool buildCellsAtStart = true;

    // ---- 내부 ----
    public bool IsOpen { get; private set; }
    public RectTransform DragLayer => dragLayer;
    public Vector2 DragGrabOffset { get; private set; }
    public Camera UICamera { get; private set; }

    private ScreenBlurBackground blur;
    private ItemView dragging;
    private Coroutine transition;
    private float openProgress;
    private bool inputStateApplied;
    private bool ready;

    private bool cursorWasVisible;
    private CursorLockMode cursorPrevLock;
    private float savedTimeScale = 1f;

    // ================= 수명주기 =================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
        {
            if (transform.parent != null)
            {
                Debug.LogWarning("[InventoryUI] 씬을 넘어 유지하려면 최상위 오브젝트여야 합니다. 부모에서 분리합니다.", this);
                transform.SetParent(null, true);
            }
            DontDestroyOnLoad(gameObject);
        }

        blur = GetComponent<ScreenBlurBackground>();
        ResolveReferences();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void Start()
    {
        if (!Validate())
        {
            Debug.LogError("[InventoryUI] 연결이 빠져서 인벤토리를 끕니다. 위 경고에 적힌 필드를 채워주세요.", this);
            enabled = false;
            return;
        }

        EnsureEventSystem();

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        UICamera = canvas != null ? canvas.worldCamera : null;

        stashGrid.Owner = this;
        foreach (EquipSlotView slot in equipSlots) slot.Owner = this;

        blur.Attach(blurImage, dimImage);

        if (inventory != null)
        {
            stashGrid.FitToGrid(inventory.Stash.Width, inventory.Stash.Height);
            if (buildCellsAtStart) stashGrid.BuildCells(inventory.Stash.Width, inventory.Stash.Height);
        }

        ready = true;

        panelRoot.SetActive(false);
        IsOpen = false;
        openProgress = 0f;
        ApplyOpenProgress();
    }

    private void OnDestroy()
    {
        Unbind();
        if (Instance == this) Instance = null;
    }

    /// <summary>필수 연결이 다 채워졌는지 확인하고, 빠진 것마다 어떤 필드인지 알려준다.</summary>
    private bool Validate()
    {
        bool ok = true;

        ok &= Require(panelRoot, "Panel Root");
        ok &= Require(dragLayer, "Drag Layer");
        ok &= Require(stashGrid, "Stash Grid");
        ok &= Require(itemPrefab, "Item Prefab");

        if (panelGroup == null && panelRoot != null)
        {
            panelGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        if (contentRoot == null && panelRoot != null)
            contentRoot = panelRoot.transform as RectTransform;

        if (equipSlots == null || equipSlots.Length == 0)
        {
            equipSlots = panelRoot != null
                ? panelRoot.GetComponentsInChildren<EquipSlotView>(true)
                : new EquipSlotView[0];

            if (equipSlots.Length == 0)
                Debug.LogWarning("[InventoryUI] EquipSlotView 가 하나도 없습니다. 장착 슬롯에 스크립트를 붙였는지 확인하세요.", this);
        }

        // 슬롯/격자가 Canvas 밖 오브젝트에 붙어 있으면 RectTransform 이 없어서 나중에 터진다. 여기서 미리 잡는다.
        foreach (EquipSlotView slot in equipSlots)
        {
            if (slot == null) continue;
            if (slot.ItemAnchor != null) continue;

            Debug.LogWarning($"[InventoryUI] 장착 슬롯 '{slot.name}' 이 UI 오브젝트가 아닙니다. Canvas 안에서 UI > Image 로 다시 만드세요.", slot);
            ok = false;
        }

        if (stashGrid != null && stashGrid.Rect == null)
        {
            Debug.LogWarning($"[InventoryUI] 보관함 격자 '{stashGrid.name}' 이 UI 오브젝트가 아닙니다. Canvas 안에서 UI > Image 로 다시 만드세요.", stashGrid);
            ok = false;
        }

        if (inventory == null)
        {
            Debug.LogWarning("[InventoryUI] PlayerInventory 를 찾지 못했습니다. 플레이어에 붙였는지 확인하세요.", this);
            ok = false;
        }

        return ok;
    }

    private bool Require(UnityEngine.Object value, string fieldName)
    {
        if (value != null) return true;
        Debug.LogWarning($"[InventoryUI] '{fieldName}' 이 비어 있습니다.", this);
        return false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();

        if (IsOpen) Close();
        if (ready) Refresh();
    }

    private void ResolveReferences()
    {
        PlayerInventory found = PlayerInventory.Instance != null
            ? PlayerInventory.Instance
            : FindFirstObjectByType<PlayerInventory>();

        if (found != inventory)
        {
            Unbind();
            inventory = found;
            Bind();
        }

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
    }

    private void Bind()
    {
        if (inventory == null) return;
        inventory.Stash.Changed += Refresh;
        inventory.EquipmentChanged += Refresh;
        inventory.StatsChanged += RefreshStats;
    }

    private void Unbind()
    {
        if (inventory == null) return;
        inventory.Stash.Changed -= Refresh;
        inventory.EquipmentChanged -= Refresh;
        inventory.StatsChanged -= RefreshStats;
    }

    private void Update()
    {
        if (!ready) return;

        if (Input.GetKeyDown(toggleKey)) Toggle();
        else if (IsOpen && Input.GetKeyDown(closeKey)) Close();

        if (IsOpen && dragging != null && Input.GetKeyDown(rotateKey)) RotateDragged();
    }

    // ================= 열기 / 닫기 =================
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!ready || IsOpen) return;
        IsOpen = true;

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        // 완전히 닫혀 있던 상태에서만 화면을 새로 찍는다. UI 를 켜기 전에 찍어야 자기 자신이 안 찍힌다.
        if (openProgress <= 0.001f)
        {
            panelRoot.SetActive(false);
            yield return blur.CaptureRoutine();

            if (!IsOpen) yield break;
        }

        panelRoot.SetActive(true);
        Refresh();
        ApplyInputState(true);

        yield return AnimateTo(1f);
        transition = null;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        CancelDrag();

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return AnimateTo(0f);

        panelRoot.SetActive(false);
        ApplyInputState(false); // 연출이 끝난 뒤에 게임을 다시 흐르게 한다
        transition = null;
    }

    /// <summary>게임이 멈춰 있어도 돌아가야 하므로 unscaled 시간을 쓴다.</summary>
    private IEnumerator AnimateTo(float target)
    {
        float duration = target > openProgress ? openDuration : closeDuration;
        float speed = 1f / Mathf.Max(0.0001f, duration);

        while (!Mathf.Approximately(openProgress, target))
        {
            openProgress = Mathf.MoveTowards(openProgress, target, Time.unscaledDeltaTime * speed);
            ApplyOpenProgress();
            yield return null;
        }

        openProgress = target;
        ApplyOpenProgress();
    }

    private void ApplyOpenProgress()
    {
        float e = openCurve.Evaluate(openProgress);

        if (panelGroup != null)
        {
            panelGroup.alpha = Mathf.Clamp01(e);

            // 반쯤 열리기 전에는 클릭을 받지 않는다. 열리는 도중에 아이템이 잡히면 위치 계산이 어긋난다.
            bool interactive = openProgress > 0.5f;
            panelGroup.blocksRaycasts = interactive;
            panelGroup.interactable = interactive;
        }

        if (contentRoot != null)
            contentRoot.localScale = Vector3.one * Mathf.LerpUnclamped(openScaleFrom, 1.22f, e);
    }

    private void ApplyInputState(bool open)
    {
        if (open == inputStateApplied) return;
        inputStateApplied = open;

        if (playerController != null) playerController.enabled = !open;

        if (showCursor)
        {
            if (open)
            {
                cursorWasVisible = Cursor.visible;
                cursorPrevLock = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = cursorWasVisible;
                Cursor.lockState = cursorPrevLock;
            }
        }

        if (pauseGame)
        {
            if (open)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = savedTimeScale;
            }
        }
    }

    // ================= 드래그 =================
    public void BeginDrag(ItemView view, PointerEventData eventData)
    {
        dragging = view;

        RectTransform rt = view.RectTransform;

        // 장착 슬롯 안의 아이템은 슬롯에 꽉 차게 늘어나 있어서, 그대로 옮기면 드래그 레이어 크기로 부푼다.
        // 좌상단을 유지한 채 격자 기준 크기로 되돌린다.
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector3 topLeftWorld = corners[1];

        rt.SetParent(dragLayer, true);
        rt.SetAsLastSibling();

        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = stashGrid.SizeFor(view.Item);
        rt.position = topLeftWorld; // 피벗이 좌상단이라 이 값이 곧 좌상단 위치

        RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, eventData.position, UICamera, out Vector2 pointerLocal);
        Vector2 itemLocal = dragLayer.InverseTransformPoint(rt.position);
        DragGrabOffset = itemLocal - pointerLocal;

        HighlightCompatibleSlots(view.Item);
    }

    public void EndDrag()
    {
        dragging = null;
        HighlightCompatibleSlots(null);
    }

    private void CancelDrag()
    {
        if (dragging == null) return;
        dragging.DropHandled = false;
        dragging = null;
        HighlightCompatibleSlots(null);
    }

    private void HighlightCompatibleSlots(ItemInstance item)
    {
        foreach (EquipSlotView slot in equipSlots)
        {
            bool on = item != null && item.data != null && item.data.IsEquippable && item.data.type == slot.slotType;
            slot.SetHighlight(on);
        }
    }

    private void RotateDragged()
    {
        ItemInstance item = dragging.Item;
        if (item == null || item.data == null || !item.data.allowRotation) return;

        item.rotated = !item.rotated;
        dragging.RectTransform.sizeDelta = stashGrid.SizeFor(item);
    }

    // ================= 드롭 처리 =================
    public bool DropOnStash(ItemView view)
    {
        if (inventory == null || view.Item == null) return false;

        Vector2Int cell = stashGrid.WorldTopLeftToCell(view.RectTransform);
        ItemInstance item = view.Item;

        // 같은 종류 위에 떨어뜨리면 겹쳐 쌓기
        ItemInstance under = inventory.Stash.GetAt(cell.x, cell.y);
        if (under != null && under != item && under.CanStackWith(item))
        {
            under.Absorb(item);
            if (item.count <= 0)
            {
                if (view.FromEquipSlot) inventory.DetachFromSlot(view.EquipSlotType, view.EquipSlotIndex);
                else inventory.Stash.Remove(item);
                Refresh();
                return true;
            }
        }

        if (view.FromEquipSlot)
        {
            // 슬롯에서 뺀 다음 놓아본다. 실패하면 다시 끼워준다.
            ItemInstance detached = inventory.DetachFromSlot(view.EquipSlotType, view.EquipSlotIndex);
            if (detached == null) return false;

            if (inventory.Stash.Place(detached, cell.x, cell.y))
            {
                Refresh();
                return true;
            }

            inventory.Equip(detached, view.EquipSlotType, view.EquipSlotIndex);
            Refresh();
            return false;
        }

        bool placed = inventory.Stash.Place(item, cell.x, cell.y);
        Refresh();
        return placed;
    }

    public bool DropOnEquipSlot(ItemView view, ItemType slotType, int slotIndex)
    {
        if (inventory == null || view.Item == null) return false;

        if (view.FromEquipSlot && view.EquipSlotType == slotType && view.EquipSlotIndex == slotIndex)
        {
            Refresh();
            return true; // 제자리
        }

        bool ok = inventory.Equip(view.Item, slotType, slotIndex);
        Refresh();
        return ok;
    }

    /// <summary>우클릭 빠른 이동: 격자 -> 빈 장착 슬롯, 장착 슬롯 -> 격자.</summary>
    public void QuickMove(ItemView view)
    {
        if (inventory == null || view.Item == null) return;

        if (view.FromEquipSlot)
            inventory.Unequip(view.EquipSlotType, view.EquipSlotIndex);
        else if (view.Item.data.IsEquippable)
            inventory.TryEquipToFreeSlot(view.Item);

        Refresh();
    }

    // ================= 갱신 =================
    public void Refresh()
    {
        if (!ready || inventory == null) return;

        // ---- 격자 ----
        if (stashGrid.ItemLayer == null) return;

        ClearItemViews(stashGrid.ItemLayer);

        foreach (ItemInstance item in inventory.Stash.Items)
        {
            ItemView view = Instantiate(itemPrefab, stashGrid.ItemLayer);
            RectTransform rt = view.RectTransform;

            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = stashGrid.SizeFor(item);

            // 부모(ItemLayer) 앵커가 어긋나 있어도 칸에 정확히 맞도록 격자 기준 월드 좌표로 놓는다
            rt.position = stashGrid.CellToWorldTopLeft(item.gridX, item.gridY);

            view.Bind(this, item, false);
        }

        // ---- 장착 슬롯 ----
        foreach (EquipSlotView slot in equipSlots)
        {
            if (slot == null || slot.ItemAnchor == null) continue;

            ClearItemViews(slot.ItemAnchor);

            ItemInstance equipped = inventory.GetEquipped(slot.slotType, slot.slotIndex);
            slot.SetEmpty(equipped == null);

            if (equipped == null) continue;

            ItemView view = Instantiate(itemPrefab, slot.ItemAnchor);
            RectTransform rt = view.RectTransform;

            // 슬롯 안에서는 격자 크기를 무시하고 슬롯을 채운다
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            float p = slot.ItemPadding;
            rt.offsetMin = new Vector2(p, p);
            rt.offsetMax = new Vector2(-p, -p);

            view.Bind(this, equipped, true, slot.slotType, slot.slotIndex);
        }

        RefreshStats();
    }

    private void RefreshStats()
    {
        if (inventory == null) return;

        if (attackLabel.IsAssigned) attackLabel.Set(inventory.Attack.ToString());
        if (defenseLabel.IsAssigned) defenseLabel.Set(inventory.Defense.ToString());
        if (critChanceLabel.IsAssigned) critChanceLabel.Set($"{inventory.CritChance * 100f:0.#}%");
        if (critDamageLabel.IsAssigned) critDamageLabel.Set($"{inventory.CritMultiplier * 100f:0.#}%");

        if (capacityLabel.IsAssigned)
        {
            int total = inventory.Stash.TotalCellCount();
            int used = total - inventory.Stash.FreeCellCount();
            capacityLabel.Set($"{used} / {total}");
        }
    }

    /// <summary>
    /// Destroy 는 프레임 끝에야 반영돼서 지운 직후에도 childCount 에 남는다.
    /// 부모에서 먼저 떼어낸 뒤 지우면 그 자리에서 사라진다.
    /// </summary>
    private static void ClearItemViews(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.GetComponent<ItemView>() == null) continue;

            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }
}
