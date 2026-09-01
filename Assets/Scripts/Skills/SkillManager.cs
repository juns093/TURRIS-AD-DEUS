using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 클릭 방식 스킬 배치 시스템의 중심. 드래그 앤 드롭을 쓰지 않는다.
///
/// 편집 모드가 아닐 때(전투 모드) 슬롯을 클릭하면 SkillExecutor 로 스킬을 실행한다.
/// 편집 모드일 때는 스킬 목록/슬롯을 클릭해서 "선택"한 뒤, 다른 슬롯을 클릭해서 그 자리로
/// 옮기거나(빈 슬롯) 자리를 맞바꾼다(이미 다른 스킬이 있는 슬롯).
///
/// 이 스크립트가 하지 않는 일
///   - UI 만들기. 편집 패널/슬롯/스킬 목록은 캔버스에서 직접 배치하고 아래 필드에 연결한다.
///   - 실제 스킬 효과. 그건 SkillExecutor.OnSkillUsed 에 연결한다.
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("스킬 데이터")]
    [Tooltip("스킬 목록에 나열할 전체 스킬. Project 에서 Nibo/Skill 로 만든 에셋을 끌어다 놓는다.")]
    [SerializeField] private List<SkillData> allSkills = new List<SkillData>();

    [Header("연결 · 스킬 목록")]
    [Tooltip("스킬 목록 항목(SkillItem)이 깔릴 부모. Horizontal/Grid Layout Group 을 붙여두면 자동 정렬된다.")]
    [SerializeField] private Transform skillListParent;

    [Tooltip("스킬 하나를 그리는 프리팹 (SkillItem 스크립트가 붙어있어야 한다).")]
    [SerializeField] private SkillItem skillItemPrefab;

    [Header("연결 · 스킬 슬롯")]
    [Tooltip("전투 중 실행되는 스킬 바. 왼쪽부터 순서대로 0, 1, 2... 번 슬롯이 된다.")]
    [SerializeField] private SkillSlot[] slots;

    [Tooltip("각 슬롯을 발동시킬 단축키. 위 Slots 와 순서가 1:1로 대응한다.\n" +
             "슬롯보다 적게 넣어도 되고, 그 경우 키가 없는 슬롯은 클릭으로만 쓴다.")]
    [SerializeField] private KeyCode[] slotKeys = { KeyCode.Alpha1, KeyCode.Alpha2 };

    [Tooltip("키패드 숫자(1,2...)도 같이 받을지.")]
    [SerializeField] private bool alsoAcceptKeypad = true;

    [Header("연결 · 편집 패널")]
    [Tooltip("스킬 배치 화면 전체를 담은 오브젝트. 평소엔 꺼져 있어야 한다.")]
    [SerializeField] private GameObject editPanelRoot;

    [Tooltip("편집 패널을 여는 버튼 (인벤토리의 스킬 아이콘 등).")]
    [SerializeField] private Button openButton;

    [Tooltip("편집 패널의 '완료' 버튼.")]
    [SerializeField] private Button doneButton;

    [Tooltip("선택 취소용 빈 영역. 편집 패널 맨 밑에 화면 전체를 덮는 투명 버튼을 깔고 연결하면 된다. 비워도 된다.")]
    [SerializeField] private Button cancelAreaButton;

    [Header("연결 · 스킬 설명창")]
    [Tooltip("스킬을 고르면 켜지는 설명창 전체. 아무것도 안 고른 상태에서는 꺼진다.")]
    [SerializeField] private GameObject detailRoot;

    [Tooltip("설명창에 크게 보여줄 스킬 아이콘.")]
    [SerializeField] private Image detailIcon;

    [Tooltip("스킬 이름. 레거시 Text 와 TextMeshPro 둘 다 받는다.")]
    [SerializeField] private UILabel detailName = new UILabel();

    [Tooltip("스킬 설명. SkillData 의 Description 이 그대로 들어간다.")]
    [SerializeField] private UILabel detailDescription = new UILabel();

    [Tooltip("쿨타임 표시. 비워두면 생략된다.")]
    [SerializeField] private UILabel detailCooldown = new UILabel();

    [Header("연출 · 패널 전환")]
    [Tooltip("스킬 패널이 열릴 때 물러날 인벤토리 쪽 패널. 보통 'Inven'.\n" +
             "가운데에서 줄어들며 사라지므로 Pivot 이 (0.5, 0.5) 여야 자연스럽다.")]
    [SerializeField] private RectTransform inventoryPanel;

    [Tooltip("인벤토리 패널을 페이드시킬 CanvasGroup. 비워두면 Inventory Panel 에서 찾거나 자동으로 붙인다.")]
    [SerializeField] private CanvasGroup inventoryGroup;

    [Tooltip("스킬 패널의 RectTransform. 비워두면 Edit Panel Root 를 그대로 쓴다.")]
    [SerializeField] private RectTransform skillPanel;

    [Tooltip("스킬 패널을 페이드시킬 CanvasGroup. 비워두면 Edit Panel Root 에서 찾거나 자동으로 붙인다.")]
    [SerializeField] private CanvasGroup skillGroup;

    [Tooltip("전환에 걸리는 시간(초). 0.25~0.35 정도가 빠르고 자연스럽다.")]
    [Range(0.1f, 1f)] [SerializeField] private float transitionDuration = 0.3f;

    [Tooltip("물러나는 쪽이 줄어드는 배율이자, 들어오는 쪽이 시작하는 배율.\n" +
             "1에서 멀어질수록 확대/축소 폭이 커진다.")]
    [Range(0.6f, 1f)] [SerializeField] private float awayScale = 0.92f;

    [SerializeField] private Ease transitionEase = Ease.OutQuad;

    [Header("저장")]
    [Tooltip("PlayerPrefs 에 저장할 때 쓸 키 접두어. 캐릭터마다 다른 배치를 쓰고 싶으면 바꿔서 여러 SkillManager 를 둘 수 있다.")]
    [SerializeField] private string saveKeyPrefix = "SkillLoadout";

    public bool IsEditing { get; private set; }
    public SkillData SelectedSkill { get; private set; }

    private SkillData[] slotAssignments;
    private readonly List<SkillItem> listItems = new List<SkillItem>();

    private Sequence transition;
    private Vector3 inventoryBaseScale = Vector3.one;
    private Vector3 skillBaseScale = Vector3.one;

    // ================= 수명주기 =================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        slotAssignments = new SkillData[slots != null ? slots.Length : 0];

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].Setup(this, i);
        }

        BuildSkillList();
        Load();
        ResolvePanels();

        if (openButton != null) openButton.onClick.AddListener(OpenEditMode);
        if (doneButton != null) doneButton.onClick.AddListener(CloseEditMode);
        if (cancelAreaButton != null) cancelAreaButton.onClick.AddListener(CancelAreaClicked);

        if (editPanelRoot != null) editPanelRoot.SetActive(false);

        RefreshSlotsUI();
    }

    /// <summary>
    /// 전환에 쓸 RectTransform / CanvasGroup 을 채우고, 원래 크기를 기억해 둔다.
    /// 원래 크기를 기억해야 하는 이유: 패널이 이미 1이 아닌 배율로 배치돼 있을 수 있어서,
    /// 애니메이션이 끝날 때 1로 되돌리면 크기가 바뀌어 버린다.
    /// </summary>
    private void ResolvePanels()
    {
        if (skillPanel == null && editPanelRoot != null)
            skillPanel = editPanelRoot.transform as RectTransform;

        if (skillGroup == null && editPanelRoot != null)
        {
            skillGroup = editPanelRoot.GetComponent<CanvasGroup>();
            if (skillGroup == null) skillGroup = editPanelRoot.AddComponent<CanvasGroup>();
        }

        if (inventoryGroup == null && inventoryPanel != null)
        {
            inventoryGroup = inventoryPanel.GetComponent<CanvasGroup>();
            if (inventoryGroup == null) inventoryGroup = inventoryPanel.gameObject.AddComponent<CanvasGroup>();
        }

        if (inventoryPanel != null) inventoryBaseScale = inventoryPanel.localScale;
        if (skillPanel != null) skillBaseScale = skillPanel.localScale;
    }

    private void Update()
    {
        // 스킬 편집 중에 인벤토리가 통째로 닫히면(패널이 꺼지면) 연출이 중간에 멈춘 채로 남는다.
        // 그대로 두면 다음에 인벤토리를 열었을 때 투명한 채로 나타나므로 즉시 원상복구한다.
        if (IsEditing && editPanelRoot != null && !editPanelRoot.activeInHierarchy)
            ResetToInventoryInstant();

        if (SkillExecutor.Instance == null || slots == null) return;

        HandleSlotHotkeys();

        for (int i = 0; i < slots.Length; i++)
        {
            SkillData skill = slotAssignments[i];
            if (skill == null || skill.cooldown <= 0f) continue;

            slots[i].SetCooldown(
                SkillExecutor.Instance.GetCooldownRatio(skill),
                SkillExecutor.Instance.GetCooldownRemaining(skill));
        }
    }

    private void OnDestroy()
    {
        transition?.Kill();
        if (Instance == this) Instance = null;
    }

    private void BuildSkillList()
    {
        if (skillListParent == null || skillItemPrefab == null) return;

        listItems.Clear();

        foreach (SkillData skill in allSkills)
        {
            if (skill == null) continue;

            SkillItem item = Instantiate(skillItemPrefab, skillListParent);
            item.Bind(this, skill);
            listItems.Add(item);
        }
    }

    // ================= 편집 모드 열기 / 닫기 =================
    public void OpenEditMode()
    {
        if (IsEditing) return;
        IsEditing = true;

        RefreshSlotsUI();
        RefreshSelectionVisuals();
        PlayTransition(true);
    }

    public void CloseEditMode()
    {
        if (!IsEditing) return;

        Deselect();
        Save();

        IsEditing = false;
        PlayTransition(false);
    }

    /// <summary>
    /// 두 패널을 겹쳐서 맞바꾸는 연출.
    /// 물러나는 쪽은 가운데로 살짝 줄어들며 사라지고, 들어오는 쪽은 조금 작은 상태에서 커지며 나타난다.
    ///
    /// 인벤토리가 Time.timeScale = 0 으로 게임을 멈춰두므로 SetUpdate(true) 로 unscaled 시간을 쓴다.
    /// 이걸 빼면 인벤토리를 연 순간 연출이 아예 흐르지 않는다.
    /// </summary>
    private void PlayTransition(bool toSkill)
    {
        if (editPanelRoot == null) return;

        transition?.Kill();

        // 인벤토리 패널의 "제자리 크기"는 InventoryUI 가 정한다.
        // (InventoryUI 의 Content Root 가 이 패널이면 열림 연출이 매번 스케일을 덮어쓴다)
        // 그래서 씬에 저장된 값이 아니라, 물러나기 직전의 실제 크기를 기준으로 삼아야
        // 되돌아왔을 때 크기가 어긋나지 않는다.
        if (toSkill && inventoryPanel != null) inventoryBaseScale = inventoryPanel.localScale;

        float duration = Mathf.Max(0.01f, transitionDuration);

        // 연출 중에는 양쪽 다 클릭을 받지 않는다. 반쯤 사라진 패널이 클릭을 가로채면 어색하다.
        SetInteractive(inventoryGroup, false);
        SetInteractive(skillGroup, false);

        transition = DOTween.Sequence().SetUpdate(true);

        if (toSkill)
        {
            editPanelRoot.SetActive(true);

            ApplyState(skillGroup, skillPanel, 0f, skillBaseScale * awayScale);

            Join(inventoryGroup, inventoryPanel, 0f, inventoryBaseScale * awayScale, duration);
            Join(skillGroup, skillPanel, 1f, skillBaseScale, duration);

            transition.OnComplete(() =>
            {
                ApplyState(skillGroup, skillPanel, 1f, skillBaseScale);
                SetInteractive(skillGroup, true);
                transition = null;
            });
        }
        else
        {
            ApplyState(inventoryGroup, inventoryPanel, 0f, inventoryBaseScale * awayScale);

            Join(skillGroup, skillPanel, 0f, skillBaseScale * awayScale, duration);
            Join(inventoryGroup, inventoryPanel, 1f, inventoryBaseScale, duration);

            transition.OnComplete(() =>
            {
                ApplyState(inventoryGroup, inventoryPanel, 1f, inventoryBaseScale);
                SetInteractive(inventoryGroup, true);

                // 다음에 열릴 때 어차피 다시 세팅하지만, 꺼둔 패널을 깨끗한 상태로 남겨둔다.
                ApplyState(skillGroup, skillPanel, 1f, skillBaseScale);
                editPanelRoot.SetActive(false);
                transition = null;
            });
        }
    }

    /// <summary>연출을 기다리지 않고 인벤토리가 보이는 상태로 즉시 되돌린다.</summary>
    private void ResetToInventoryInstant()
    {
        transition?.Kill();
        transition = null;

        IsEditing = false;
        SelectedSkill = null;

        ApplyState(inventoryGroup, inventoryPanel, 1f, inventoryBaseScale);
        SetInteractive(inventoryGroup, true);

        ApplyState(skillGroup, skillPanel, 1f, skillBaseScale);
        if (editPanelRoot != null) editPanelRoot.SetActive(false);

        RefreshSelectionVisuals();
    }

    private void Join(CanvasGroup group, RectTransform rect, float alpha, Vector3 scale, float duration)
    {
        if (group != null) transition.Join(group.DOFade(alpha, duration).SetEase(transitionEase));
        if (rect != null) transition.Join(rect.DOScale(scale, duration).SetEase(transitionEase));
    }

    private static void ApplyState(CanvasGroup group, RectTransform rect, float alpha, Vector3 scale)
    {
        if (group != null) group.alpha = alpha;
        if (rect != null) rect.localScale = scale;
    }

    private static void SetInteractive(CanvasGroup group, bool on)
    {
        if (group == null) return;
        group.blocksRaycasts = on;
        group.interactable = on;
    }

    // ================= 선택 =================
    /// <summary>스킬 목록에서 스킬을 클릭했을 때. SkillItem 이 호출한다.</summary>
    public void SelectFromList(SkillData skill)
    {
        if (!IsEditing || skill == null) return;

        if (SelectedSkill == skill)
        {
            Deselect();
            return;
        }

        SelectedSkill = skill;
        RefreshSelectionVisuals();
    }

    private void SelectFromSlot(int index)
    {
        SkillData skill = slotAssignments[index];
        if (skill == null) return;

        if (SelectedSkill == skill)
        {
            Deselect();
            return;
        }

        SelectedSkill = skill;
        RefreshSelectionVisuals();
    }

    public void Deselect()
    {
        if (SelectedSkill == null) return;

        SelectedSkill = null;
        RefreshSelectionVisuals();
    }

    private void CancelAreaClicked()
    {
        if (!IsEditing) return;
        Deselect();
    }

    // ================= 슬롯 클릭 =================
    /// <summary>슬롯을 클릭했을 때. SkillSlot 이 호출한다. 편집 모드/전투 모드에 따라 동작이 갈린다.</summary>
    public void SlotClicked(int index)
    {
        if (index < 0 || index >= slotAssignments.Length) return;

        if (!IsEditing)
        {
            UseSlot(index);
            return;
        }

        if (SelectedSkill == null)
        {
            SelectFromSlot(index);
            return;
        }

        int currentIndex = System.Array.IndexOf(slotAssignments, SelectedSkill);

        // 선택된 스킬이 원래 있던(혹은 이미 배정돼 있던) 슬롯을 다시 클릭 -> 선택 취소
        if (currentIndex == index)
        {
            Deselect();
            return;
        }

        SkillData displaced = slotAssignments[index];

        slotAssignments[index] = SelectedSkill;
        if (currentIndex >= 0) slotAssignments[currentIndex] = displaced; // 이동(비어있던 자리) 또는 교환

        Deselect();
        RefreshSlotsUI();
        Save();
    }

    /// <summary>
    /// 1, 2 ... 숫자키로 해당 슬롯의 스킬을 쓴다.
    /// 편집 중이거나 인벤토리가 열려 있으면 무시한다.
    /// (편집 중에 키를 누르면 배치하려던 스킬이 그냥 발동돼버리고,
    ///  인벤토리는 게임을 멈춰둔 상태라 그때 스킬이 나가면 안 된다)
    /// </summary>
    private void HandleSlotHotkeys()
    {
        if (IsEditing) return;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;
        if (slotKeys == null) return;

        int count = Mathf.Min(slotKeys.Length, slots.Length);

        for (int i = 0; i < count; i++)
        {
            if (slotKeys[i] == KeyCode.None) continue;
            if (!WasPressed(slotKeys[i])) continue;

            UseSlot(i);
            break; // 한 프레임에 여러 스킬이 동시에 나가지 않게
        }
    }

    /// <summary>상단 숫자키와 키패드 숫자키를 같은 것으로 취급한다.</summary>
    private bool WasPressed(KeyCode key)
    {
        if (Input.GetKeyDown(key)) return true;
        if (!alsoAcceptKeypad) return false;

        // Alpha1..Alpha9 을 대응하는 Keypad1..Keypad9 로 바꿔서 한 번 더 본다
        if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
        {
            KeyCode pad = KeyCode.Keypad1 + (key - KeyCode.Alpha1);
            return Input.GetKeyDown(pad);
        }

        return false;
    }

    private void UseSlot(int index)
    {
        SkillData skill = slotAssignments[index];
        if (skill == null || SkillExecutor.Instance == null) return;

        SkillExecutor.Instance.TryUseSkill(skill);
    }

    // ================= 갱신 =================
    private void RefreshSlotsUI()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].SetSkill(slotAssignments[i]);
        }

        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        foreach (SkillItem item in listItems)
            item.SetSelected(SelectedSkill != null && item.Skill == SelectedSkill);

        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].SetSelected(SelectedSkill != null && slotAssignments[i] == SelectedSkill);
        }

        RefreshDetail();
    }

    /// <summary>고른 스킬의 아이콘·이름·설명을 옆 설명창에 채운다. 고른 게 없으면 창을 닫는다.</summary>
    private void RefreshDetail()
    {
        bool has = SelectedSkill != null;

        if (detailRoot != null) detailRoot.SetActive(has);
        if (!has) return;

        if (detailIcon != null)
        {
            detailIcon.sprite = SelectedSkill.icon;
            detailIcon.enabled = SelectedSkill.icon != null;
            detailIcon.preserveAspect = true;
        }

        if (detailName.IsAssigned) detailName.Set(SelectedSkill.skillName);
        if (detailDescription.IsAssigned) detailDescription.Set(SelectedSkill.description);

        if (detailCooldown.IsAssigned)
        {
            detailCooldown.Set(SelectedSkill.cooldown > 0f
                ? $"재사용 대기 {SelectedSkill.cooldown:0.#}초"
                : "재사용 대기 없음");
        }
    }

    // ================= 저장 / 불러오기 =================
    private void Save()
    {
        for (int i = 0; i < slotAssignments.Length; i++)
        {
            string id = slotAssignments[i] != null ? slotAssignments[i].skillID : string.Empty;
            PlayerPrefs.SetString(SlotKey(i), id);
        }

        PlayerPrefs.SetInt(saveKeyPrefix + "_Count", slotAssignments.Length);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        int savedCount = PlayerPrefs.GetInt(saveKeyPrefix + "_Count", 0);
        int count = Mathf.Min(savedCount, slotAssignments.Length);

        for (int i = 0; i < count; i++)
        {
            string id = PlayerPrefs.GetString(SlotKey(i), string.Empty);
            slotAssignments[i] = string.IsNullOrEmpty(id) ? null : FindSkill(id);
        }
    }

    private string SlotKey(int index) => $"{saveKeyPrefix}_Slot_{index}";

    private SkillData FindSkill(string id)
    {
        foreach (SkillData skill in allSkills)
            if (skill != null && skill.skillID == id) return skill;

        return null;
    }
}
