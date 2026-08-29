using UnityEngine;

/// <summary>스킬의 성격. 필요하면 자유롭게 늘려 쓴다.</summary>
public enum SkillType
{
    Attack,
    Defense,
    Buff,
    Utility
}

/// <summary>
/// 스킬 원본 데이터. Project 창에서 우클릭 → Create → Nibo → Skill 로 만든다.
/// ItemData 와 마찬가지로 이 자체가 "장착 상태"를 갖지 않는다.
/// 어느 슬롯에 배정됐는지는 SkillManager 가 skillID 기준으로 따로 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "Nibo/Skill", order = 1)]
public class SkillData : ScriptableObject
{
    [Header("기본")]
    [Tooltip("저장/불러오기에서 스킬을 찾는 키. 에셋 이름과 달라도 되지만 절대 겹치면 안 된다.")]
    public string skillID = "skill_id";
    public string skillName = "이름 없는 스킬";
    [TextArea(2, 4)] public string description;

    [Header("표시")]
    public Sprite icon;

    [Header("전투")]
    [Tooltip("한 번 쓰고 다시 쓸 수 있을 때까지의 시간(초). 0이면 쿨타임 없음.")]
    [Min(0f)] public float cooldown = 1f;
    public SkillType skillType = SkillType.Attack;
}
