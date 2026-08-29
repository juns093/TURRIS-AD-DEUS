using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 스킬 바의 슬롯 하나. 편집 모드에서는 배정/이동/교환 판정판으로, 전투 모드에서는 실행 버튼으로 쓰인다.
/// 캔버스에서 슬롯 이미지를 직접 배치하고 이 스크립트를 붙인 뒤 SkillManager 의 Slots 배열에 순서대로 연결한다.
///
/// 프리팹/오브젝트 구성 예시
///   SkillSlot            ... Image(Raycast Target 켤 것) + 이 스크립트
///     └ Icon             ... Image      -> Icon Image 에 연결
///     └ EmptyPlaceholder ... 빈 칸일 때만 보일 그림/텍스트 -> Empty Placeholder 에 연결
///     └ SelectedFrame    ... 선택 강조 -> Selected Highlight 에 연결
///     └ CooldownOverlay  ... Image(Filled, Radial 360) -> Cooldown Overlay 에 연결 (선택)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkillSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("연결")]
    [Tooltip("배정된 스킬 아이콘이 들어갈 Image.")]
    [SerializeField] private Image iconImage;

    [Tooltip("비어 있을 때만 보여줄 그림/텍스트. 없어도 된다.")]
    [SerializeField] private GameObject emptyPlaceholder;

    [Tooltip("선택된 스킬이 이 슬롯에 있을 때 켜질 강조 표시.")]
    [SerializeField] private GameObject selectedHighlight;

    [Tooltip("쿨타임 표시용 Image (Image Type = Filled). 비워두면 쿨타임을 표시하지 않는다.")]
    [SerializeField] private Image cooldownOverlay;

    /// <summary>SkillManager 가 배열 순번에 맞춰 자동으로 채워준다.</summary>
    public int SlotIndex { get; private set; }

    private SkillManager owner;

    public void Setup(SkillManager owner, int index)
    {
        this.owner = owner;
        SlotIndex = index;
    }

    public void SetSkill(SkillData skill)
    {
        bool has = skill != null;

        if (iconImage != null)
        {
            iconImage.sprite = has ? skill.icon : null;
            iconImage.enabled = has && skill.icon != null;
        }

        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(!has);
        if (!has) SetCooldown(0f);
    }

    public void SetSelected(bool on)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(on);
    }

    /// <summary>ratio 는 0(사용 가능)~1(방금 씀). 0 이하면 오버레이를 꺼준다.</summary>
    public void SetCooldown(float ratio)
    {
        if (cooldownOverlay == null) return;

        bool show = ratio > 0f;
        cooldownOverlay.gameObject.SetActive(show);
        if (show) cooldownOverlay.fillAmount = Mathf.Clamp01(ratio);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        owner?.SlotClicked(SlotIndex);
    }
}
