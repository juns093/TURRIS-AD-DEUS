using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 스킬 목록(팔레트)에 한 줄로 나열되는 스킬 하나. SkillManager 가 allSkills 를 돌면서
/// 이 프리팹을 스킬 개수만큼 찍어서 skillListParent 밑에 깐다.
///
/// 프리팹 구성 예시
///   SkillItem          ... Image(Raycast Target 켤 것) + 이 스크립트
///     └ Icon           ... Image      -> Icon Image 에 연결
///     └ SelectedFrame  ... 테두리/배경 오브젝트 -> Selected Highlight 에 연결 (평소엔 꺼둘 것)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkillItem : MonoBehaviour, IPointerClickHandler
{
    [Header("표시")]
    [Tooltip("스킬 아이콘이 들어갈 Image. 비워두면 이 오브젝트의 Image 를 쓴다.")]
    [SerializeField] private Image iconImage;

    [Tooltip("선택됐을 때 켜질 강조 표시(테두리/배경 등). 평소엔 꺼져 있어야 한다.")]
    [SerializeField] private GameObject selectedHighlight;

    public SkillData Skill { get; private set; }

    private SkillManager owner;

    private void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
    }

    public void Bind(SkillManager owner, SkillData skill)
    {
        this.owner = owner;
        Skill = skill;

        if (iconImage != null)
        {
            iconImage.sprite = skill != null ? skill.icon : null;
            iconImage.enabled = skill != null && skill.icon != null;
        }

        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(on);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (Skill == null || owner == null) return;

        owner.SelectFromList(Skill);
    }
}
